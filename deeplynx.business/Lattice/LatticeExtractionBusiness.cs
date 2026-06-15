using System.Collections.Concurrent;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

public partial class LatticeExtractionBusiness : ILatticeExtractionBusiness
{
    private static readonly ConcurrentDictionary<string, string> _promptTemplateCache = new();

    private readonly DeeplynxContext _context;
    private readonly IInsightBusiness _insightBusiness;
    private readonly InsightServiceClient _insightServiceClient;
    private readonly LatticeContext _latticeContext;
    private readonly ILogger<LatticeExtractionBusiness> _logger;

    public LatticeExtractionBusiness(DeeplynxContext context, LatticeContext latticeContext,
        IInsightBusiness insightBusiness, InsightServiceClient insightServiceClient,
        ILogger<LatticeExtractionBusiness> logger)
    {
        _context = context;
        _latticeContext = latticeContext;
        _insightBusiness = insightBusiness;
        _insightServiceClient = insightServiceClient;
        _logger = logger;
    }

    /// <summary>
    ///     Creates a Pending Extraction record, builds ontology context via similarity search,
    ///     and fires the trigger request to Insight.
    ///     Returns immediately after Insight acknowledges with 202; the extraction runs
    ///     asynchronously on the Insight side and calls back when complete.
    ///     For strict mode, record and ontology embeddings must exist — missing embeddings are
    ///     queued automatically and an exception is thrown so the caller can retry.
    /// </summary>
    /// <param name="currentUserId">ID of the user triggering the extraction. Used for the callback token and audit trail.</param>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="recordId">The ID of the document record to extract from.</param>
    /// <param name="mode">Extraction mode: "discovery" (infer schema) or "strict" (map to existing ontology).</param>
    /// <returns>The ID of the created Extraction record.</returns>
    public async Task<long> TriggerLatticeExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        string mode)
    {
        var record = await _context.Records
                         .Where(r => r.Id == recordId && r.ProjectId == projectId)
                         .FirstOrDefaultAsync()
                     ?? throw new InvalidOperationException($"Record {recordId} not found in project {projectId}");

        // Minimum ontology items necessary for extraction
        await EnsureOntologyReady(projectId);

        // Ontology embeddings must exist before triggering. If they're missing, queue
        // them automatically and fail fast so the user can retry once they're ready.
        await EnsureEmbeddingsReady(currentUserId, organizationId, projectId, recordId, record);

        var extraction = new Extraction
        {
            CreatedBy = currentUserId,
            Status = ExtractionStatus.Pending,
            Mode = mode,
            ProjectId = projectId
        };
        _context.Extractions.Add(extraction);
        await _context.SaveChangesAsync();

        try
        {
            // IDs necessary for POST back from Insight 
            var queryInfo = new
            {
                organization_id = organizationId,
                project_id = projectId,
                extraction_id = extraction.Id,
                data_source_id = record.DataSourceId
            };

            var filledPrompt = await ConstructPrompt(recordId, projectId, mode);

            var latticeModel = Environment.GetEnvironmentVariable("LATTICE_MODEL")
                               ?? "Mistral-Small-3.2-24B-Instruct-2506";
            var response =
                await _insightServiceClient.LatticeExtraction(filledPrompt, latticeModel, queryInfo);

            if (response.IsSuccessStatusCode)
            {
                extraction.Status = ExtractionStatus.Running;
            }
            else
            {
                extraction.Status = ExtractionStatus.Failed;
                throw new HttpRequestException(
                    "Lattice extraction request failed");
            }

            await _context.SaveChangesAsync();
        }
        catch
        {
            extraction.Status = ExtractionStatus.Failed;
            await _context.SaveChangesAsync();
            throw;
        }

        return extraction.Id;
    }


    /// <summary>
    ///     Process insight callback for extractions
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">
    ///     The ID of the project — only classes and relationships belonging to this project are used in
    ///     the extraction.
    /// </param>
    /// <param name="dataSourceId">The ID of the data source for the project</param>
    /// <param name="extractionId">The ID of the data source for the project</param>
    public async Task<ExtractionResponseDto> ProcessInsightCallback(
        long organizationId,
        long projectId,
        long dataSourceId,
        long extractionId,
        InsightExtractionCallbackDto dto)
    {
        var extraction = await _context.Extractions.FindAsync(extractionId)
                         ?? throw new InvalidOperationException($"Extraction {extractionId} not found.");

        var mode = extraction.Mode
                   ?? throw new InvalidOperationException($"Extraction {extractionId} has no mode set.");

        // Validation Logic
        var (dedupedRecords, dedupedEdges) = Deduplicate(dto);

        var allClassTypes = dedupedRecords.Select(r => r.ClassType)
            .Concat(dedupedEdges.Select(e => e.SubjectType))
            .Concat(dedupedEdges.Select(e => e.ObjectType));

        var classSimilarities = await NormalizeClassTypes(allClassTypes, projectId);
        var relSimilarities = await NormalizeRelationshipTypes(dedupedEdges, projectId);
        var ontologyPatterns = await GetOntologyPatterns(projectId);

        // Save to Lattice schema 
        await using var transaction = await _latticeContext.Database.BeginTransactionAsync();
        try
        {
            var classes = await StageClasses(extraction.Id, allClassTypes, classSimilarities, organizationId, projectId,
                mode);
            var records = await StageRecords(extraction.Id, dedupedRecords, classSimilarities, ontologyPatterns,
                classes, organizationId, projectId, dataSourceId, mode);
            var relationships = await StageRelationships(extraction.Id, dedupedEdges, classSimilarities,
                relSimilarities, ontologyPatterns, classes, organizationId, projectId, mode);
            var edgeCount = await StageEdges(extraction.Id, dedupedEdges, relSimilarities, ontologyPatterns, records,
                relationships, organizationId, projectId, dataSourceId, mode);

            await transaction.CommitAsync();

            extraction.Status = ExtractionStatus.Complete;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Lattice data is committed — log and continue rather than leaving status stuck as Running
                _logger.LogError(ex,
                    "Extraction {ExtractionId} staged successfully but status update to Complete failed", extractionId);
            }

            return new ExtractionResponseDto
            {
                Id = extraction.Id,
                CreatedBy = extraction.CreatedBy,
                ClassCount = classes.Count,
                RecordCount = records.Count,
                RelationshipCount = relationships.Count,
                EdgeCount = edgeCount
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            extraction.Status = ExtractionStatus.Failed;
            await _context.SaveChangesAsync();
            throw;
        }
    }

    /// <summary>
    ///     Marks an extraction as failed. Called when Lattice reports an error via its error callback.
    /// </summary>
    /// <param name="extractionId">The ID of the extraction to mark as failed.</param>
    /// <param name="errorMessage">Optional error message from Lattice, logged by the caller.</param>
    public async Task MarkExtractionFailed(long extractionId, string? errorMessage = null)
    {
        var extraction = await _context.Extractions.FindAsync(extractionId)
                         ?? throw new InvalidOperationException($"Extraction {extractionId} not found.");
        extraction.Status = ExtractionStatus.Failed;
        await _context.SaveChangesAsync();
        _logger.LogError(errorMessage);
    }

    /// <summary>
    ///     Searches for the most similar ontology terms (classes and/or relationships) in the project
    ///     by comparing a record's stored embeddings against all ontology vectors using cosine similarity.
    /// </summary>
    /// <param name="recordId">The ID of the record whose embeddings are used as the query vectors.</param>
    /// <param name="projectId">The ID of the project — only classes and relationships belonging to this project are searched.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    public async Task<List<OntologySimilarityResultDto>> SearchOntologySimilarity(
        long recordId,
        long projectId,
        long limit = 20)
    {
        return await _context.Database
            .SqlQuery<OntologySimilarityResultDto>($"""
                                                    SELECT name, class_or_relationship_id, type, description, score, text_chunk,
                                                           origin_class, destination_class
                                                    FROM (
                                                        SELECT
                                                            COALESCE(c.name, rel.name)                                      AS name,
                                                            COALESCE(ov.class_id, ov.relationship_id)                       AS class_or_relationship_id,
                                                            CASE WHEN ov.class_id IS NOT NULL THEN 'class' ELSE 'relationship' END AS type,
                                                            COALESCE(c.description, rel.description)                        AS description,
                                                            1 - (ov.vector <=> e.vector)                                    AS score,
                                                            e.text_chunk                                                    AS text_chunk,
                                                            CASE WHEN ov.relationship_id IS NOT NULL THEN origin_c.name  END AS origin_class,
                                                            CASE WHEN ov.relationship_id IS NOT NULL THEN dest_c.name    END AS destination_class,
                                                            ROW_NUMBER() OVER (
                                                                PARTITION BY e.id
                                                                ORDER BY ov.vector <=> e.vector ASC
                                                            ) AS rank
                                                        FROM dl_vector.embeddings e
                                                        JOIN dl_vector.ontology_vector ov ON TRUE
                                                        LEFT JOIN deeplynx.classes c             ON c.id = ov.class_id
                                                        LEFT JOIN deeplynx.relationships rel     ON rel.id = ov.relationship_id
                                                        LEFT JOIN deeplynx.classes origin_c      ON origin_c.id = rel.origin_id
                                                        LEFT JOIN deeplynx.classes dest_c        ON dest_c.id = rel.destination_id
                                                        WHERE e.record_id = {recordId}
                                                          AND (c.project_id = {projectId} OR rel.project_id = {projectId})
                                                    ) ranked
                                                    WHERE rank <= {limit}
                                                    ORDER BY text_chunk, rank;
                                                    """)
            .ToListAsync();
    }

    /// <summary>
    ///     List extractions for project
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="projectId">The ID of the project</param>
    public async Task<List<ExtractionListItemDto>> ListExtractionsByProject(long projectId)
    {
        var extractions = await _context.Extractions
            .Where(e => e.ProjectId == projectId)
            .OrderByDescending(e => e.Id)
            .Select(e => new ExtractionListItemDto
            {
                Id = e.Id,
                Status = e.Status,
                Mode = e.Mode,
                CreatedBy = e.CreatedBy,
                ProjectId = e.ProjectId
            })
            .ToListAsync();

        if (extractions.Count == 0) return extractions;

        // Staging items live in the lattice schema (separate context), so totals/resolved counts are
        // gathered per staging table and aggregated. "Resolved" mirrors the completeness rule: an item
        // counts once it is promoted, rejected, or (for classes/relationships) matched an existing
        // ontology entity that needs no promotion.
        var ids = extractions.Select(e => e.Id).ToList();
        var totals = ids.ToDictionary(id => id, _ => 0);
        var resolved = ids.ToDictionary(id => id, _ => 0);

        void Accumulate(IEnumerable<(long ExtractionId, int Total, int Resolved)> rows)
        {
            foreach (var row in rows)
            {
                totals[row.ExtractionId] += row.Total;
                resolved[row.ExtractionId] += row.Resolved;
            }
        }

        Accumulate((await _latticeContext.ExtractionClasses
            .Where(c => ids.Contains(c.ExtractionId))
            .GroupBy(c => c.ExtractionId)
            .Select(g => new
            {
                g.Key,
                Total = g.Count(),
                Resolved = g.Count(c => c.PromotedId != null || c.OntologyClassId != null || c.Rejected)
            })
            .ToListAsync()).Select(x => (x.Key, x.Total, x.Resolved)));

        Accumulate((await _latticeContext.ExtractionRecords
            .Where(r => ids.Contains(r.ExtractionId))
            .GroupBy(r => r.ExtractionId)
            .Select(g => new { g.Key, Total = g.Count(), Resolved = g.Count(r => r.PromotedId != null || r.Rejected) })
            .ToListAsync()).Select(x => (x.Key, x.Total, x.Resolved)));

        Accumulate((await _latticeContext.ExtractionRelationships
            .Where(r => ids.Contains(r.ExtractionId))
            .GroupBy(r => r.ExtractionId)
            .Select(g => new
            {
                g.Key,
                Total = g.Count(),
                Resolved = g.Count(r => r.PromotedId != null || r.OntologyRelationshipId != null || r.Rejected)
            })
            .ToListAsync()).Select(x => (x.Key, x.Total, x.Resolved)));

        Accumulate((await _latticeContext.ExtractionEdges
            .Where(e => ids.Contains(e.ExtractionId))
            .GroupBy(e => e.ExtractionId)
            .Select(g => new { g.Key, Total = g.Count(), Resolved = g.Count(e => e.PromotedId != null || e.Rejected) })
            .ToListAsync()).Select(x => (x.Key, x.Total, x.Resolved)));

        foreach (var item in extractions)
        {
            item.TotalCount = totals[item.Id];
            item.PromotedCount = resolved[item.Id];
        }

        return extractions;
    }

    public async Task<EmbeddingStatusResponseDto> GetEmbeddingStatus(long projectId)
    {
        var classIds = await _context.Classes
            .Where(c => c.ProjectId == projectId)
            .Select(c => c.Id)
            .ToListAsync();
        var relationshipIds = await _context.Relationships
            .Where(r => r.ProjectId == projectId)
            .Select(r => r.Id)
            .ToListAsync();

        var embeddedClassIds = await _context.OntologyVectors
            .Where(ov => ov.ClassId != null && classIds.Contains(ov.ClassId.Value))
            .Select(ov => ov.ClassId!.Value)
            .Distinct()
            .ToListAsync();
        var embeddedRelationshipIds = await _context.OntologyVectors
            .Where(ov => ov.RelationshipId != null && relationshipIds.Contains(ov.RelationshipId.Value))
            .Select(ov => ov.RelationshipId!.Value)
            .Distinct()
            .ToListAsync();

        return new EmbeddingStatusResponseDto
        {
            ClassCount = classIds.Count,
            EmbeddedClassCount = embeddedClassIds.Count,
            RelationshipCount = relationshipIds.Count,
            EmbeddedRelationshipCount = embeddedRelationshipIds.Count,
            OntologyReady = embeddedClassIds.Count > 0 || embeddedRelationshipIds.Count > 0
        };
    }

    public async Task<ExtractionStagingResponseDto> GetExtractionStaging(long extractionId)
    {
        var extraction = await _context.Extractions.FindAsync(extractionId)
                         ?? throw new InvalidOperationException($"Extraction {extractionId} not found.");

        var classes = await _latticeContext.ExtractionClasses
            .Where(c => c.ExtractionId == extractionId)
            .ToListAsync();

        var classNameMap = classes.ToDictionary(c => c.Id, c => c.Name);

        var records = await _latticeContext.ExtractionRecords
            .Where(r => r.ExtractionId == extractionId)
            .ToListAsync();

        var relationships = await _latticeContext.ExtractionRelationships
            .Where(r => r.ExtractionId == extractionId)
            .ToListAsync();

        var edges = await _latticeContext.ExtractionEdges
            .Where(e => e.ExtractionId == extractionId)
            .ToListAsync();

        // Build record name map for edge labels
        var recordNameMap = records.ToDictionary(r => r.Id, r => r.Name);

        // Build relationship name map for edge labels
        var relNameMap = relationships.ToDictionary(r => r.Id, r => r.Name);

        return new ExtractionStagingResponseDto
        {
            Id = extraction.Id,
            Status = extraction.Status,
            Mode = extraction.Mode,
            CreatedBy = extraction.CreatedBy,
            Classes = classes.Select(c => new StagedClassDto
            {
                Id = c.Id,
                Name = c.Name,
                ValidationStatus = c.ValidationStatus,
                OntologyClassId = c.OntologyClassId,
                PromotedId = c.PromotedId,
                Rejected = c.Rejected
            }).ToList(),
            Records = records.Select(r => new StagedRecordDto
            {
                Id = r.Id,
                Name = r.Name,
                ExtractionClassId = r.ExtractionClassId,
                ClassName = classNameMap.GetValueOrDefault(r.ExtractionClassId),
                Attributes = r.Attributes,
                ValidationStatus = r.ValidationStatus,
                EnsembleScore = r.EnsembleScore,
                Frequency = r.Frequency,
                DeeplynxRecordId = r.DeeplynxRecordId,
                PromotedId = r.PromotedId,
                Rejected = r.Rejected
            }).ToList(),
            Relationships = relationships.Select(r => new StagedRelationshipDto
            {
                Id = r.Id,
                Name = r.Name,
                OriginClassId = r.OriginClassId,
                DestinationClassId = r.DestinationClassId,
                OriginClassName = classNameMap.GetValueOrDefault(r.OriginClassId),
                DestinationClassName = classNameMap.GetValueOrDefault(r.DestinationClassId),
                ValidationStatus = r.ValidationStatus,
                OntologyRelationshipId = r.OntologyRelationshipId,
                PromotedId = r.PromotedId,
                Rejected = r.Rejected
            }).ToList(),
            Edges = edges.Select(e => new StagedEdgeDto
            {
                Id = e.Id,
                OriginRecordId = e.OriginRecordId,
                DestinationRecordId = e.DestinationRecordId,
                ExtractionRelationshipId = e.ExtractionRelationshipId,
                OriginRecordName = recordNameMap.GetValueOrDefault(e.OriginRecordId),
                DestinationRecordName = recordNameMap.GetValueOrDefault(e.DestinationRecordId),
                RelationshipName = relNameMap.GetValueOrDefault(e.ExtractionRelationshipId),
                ValidationStatus = e.ValidationStatus,
                EnsembleScore = e.EnsembleScore,
                Frequency = e.Frequency,
                PromotedId = e.PromotedId,
                Rejected = e.Rejected
            }).ToList()
        };
    }

    /// <summary>
    ///     Promotes a user-selected subset of an extraction's staged items into the deeplynx schema.
    ///     Selection is by explicit item ids and/or bulk approval of a validation status
    ///     (<c>valid</c> / <c>novel_discovery</c>); the two are unioned. Items not selected are left
    ///     staged for a later round. Promotion runs in dependency order
    ///     (classes → records → relationships → edges) and is strict: a selected item whose dependency
    ///     (class/record/relationship) is neither selected nor already promoted is rejected with an
    ///     error rather than silently skipped. The extraction flips to
    ///     <see cref="ExtractionStatus.Promoted" /> once every staged item is resolved, otherwise
    ///     <see cref="ExtractionStatus.PartiallyPromoted" />.
    /// </summary>
    public async Task<ExtractionResponseDto> PromoteExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long extractionId,
        PromoteExtractionRequestDto request)
    {
        var extraction = await _context.Extractions.FindAsync(extractionId)
                         ?? throw new InvalidOperationException($"Extraction {extractionId} not found.");

        if (extraction.Status != ExtractionStatus.Complete &&
            extraction.Status != ExtractionStatus.PartiallyPromoted)
            throw new InvalidOperationException(
                $"Extraction {extractionId} cannot be promoted — status is '{extraction.Status}', " +
                $"expected '{ExtractionStatus.Complete}' or '{ExtractionStatus.PartiallyPromoted}'.");

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // Load ALL staging items — needed both to resolve already-promoted dependencies of the
        // selected items and to expand status-based bulk approval into concrete ids.
        var stagingClasses = await _latticeContext.ExtractionClasses
            .Where(c => c.ExtractionId == extractionId)
            .ToListAsync();

        var stagingRecords = await _latticeContext.ExtractionRecords
            .Where(r => r.ExtractionId == extractionId)
            .ToListAsync();

        var stagingRelationships = await _latticeContext.ExtractionRelationships
            .Where(r => r.ExtractionId == extractionId)
            .ToListAsync();

        var stagingEdges = await _latticeContext.ExtractionEdges
            .Where(e => e.ExtractionId == extractionId)
            .ToListAsync();

        // ---- Resolve the selection: explicit ids ∪ status-based bulk approval ----
        var allowedBulkStatuses = new[]
            { ExtractionValidationStatus.Valid, ExtractionValidationStatus.NovelDiscovery };
        foreach (var status in request.ApproveByStatus)
            if (!allowedBulkStatuses.Contains(status))
                throw new InvalidOperationException(
                    $"Validation status '{status}' cannot be bulk-approved. Only " +
                    $"'{ExtractionValidationStatus.Valid}' and '{ExtractionValidationStatus.NovelDiscovery}' are allowed.");

        var bulkStatuses = request.ApproveByStatus.ToHashSet();
        var selectedClassIds = request.ClassIds.ToHashSet();
        var selectedRecordIds = request.RecordIds.ToHashSet();
        var selectedRelIds = request.RelationshipIds.ToHashSet();
        var selectedEdgeIds = request.EdgeIds.ToHashSet();

        if (bulkStatuses.Count > 0)
        {
            // Only items neither already promoted nor rejected are eligible for bulk approval.
            foreach (var c in stagingClasses.Where(c =>
                         !c.PromotedId.HasValue && !c.Rejected && bulkStatuses.Contains(c.ValidationStatus!)))
                selectedClassIds.Add(c.Id);
            foreach (var r in stagingRecords.Where(r =>
                         !r.PromotedId.HasValue && !r.Rejected && bulkStatuses.Contains(r.ValidationStatus!)))
                selectedRecordIds.Add(r.Id);
            foreach (var r in stagingRelationships.Where(r =>
                         !r.PromotedId.HasValue && !r.Rejected && bulkStatuses.Contains(r.ValidationStatus!)))
                selectedRelIds.Add(r.Id);
            foreach (var e in stagingEdges.Where(e =>
                         !e.PromotedId.HasValue && !e.Rejected && bulkStatuses.Contains(e.ValidationStatus!)))
                selectedEdgeIds.Add(e.Id);
        }

        if (selectedClassIds.Count == 0 && selectedRecordIds.Count == 0 &&
            selectedRelIds.Count == 0 && selectedEdgeIds.Count == 0)
            throw new InvalidOperationException("No staged items were selected for promotion.");

        // A previously rejected item can never be promoted. Surface explicit attempts rather than
        // silently skipping them.
        var rejectedSelections = stagingClasses.Where(c => c.Rejected && selectedClassIds.Contains(c.Id))
            .Select(c => $"class '{c.Name}' (id {c.Id})")
            .Concat(stagingRecords.Where(r => r.Rejected && selectedRecordIds.Contains(r.Id))
                .Select(r => $"record '{r.Name}' (id {r.Id})"))
            .Concat(stagingRelationships.Where(r => r.Rejected && selectedRelIds.Contains(r.Id))
                .Select(r => $"relationship '{r.Name}' (id {r.Id})"))
            .Concat(stagingEdges.Where(e => e.Rejected && selectedEdgeIds.Contains(e.Id))
                .Select(e => $"edge (id {e.Id})"))
            .ToList();
        if (rejectedSelections.Count > 0)
            throw new InvalidOperationException(
                "Cannot promote items that were previously rejected:\n" + string.Join("\n", rejectedSelections));

        // ---- Strict dependency validation ----
        var classById = stagingClasses.ToDictionary(c => c.Id);
        var recordById = stagingRecords.ToDictionary(r => r.Id);
        var relById = stagingRelationships.ToDictionary(r => r.Id);
        var edgeById = stagingEdges.ToDictionary(e => e.Id);

        // An ancestor is "satisfiable" if it already exists in deeplynx (matched an existing ontology
        // entity or was promoted in a prior round) or is being promoted in this same round.
        bool ClassSatisfied(long id) =>
            classById.TryGetValue(id, out var c) && !c.Rejected &&
            (c.OntologyClassId.HasValue || c.PromotedId.HasValue || selectedClassIds.Contains(id));
        bool RecordSatisfied(long id) =>
            recordById.TryGetValue(id, out var r) && !r.Rejected &&
            (r.PromotedId.HasValue || selectedRecordIds.Contains(id));
        bool RelSatisfied(long id) =>
            relById.TryGetValue(id, out var r) && !r.Rejected &&
            (r.OntologyRelationshipId.HasValue || r.PromotedId.HasValue || selectedRelIds.Contains(id));

        var errors = new List<string>();

        foreach (var id in selectedRecordIds)
            if (recordById.TryGetValue(id, out var r) && !ClassSatisfied(r.ExtractionClassId))
                errors.Add($"Record '{r.Name}' (id {id}) requires its class to be approved or already promoted.");

        foreach (var id in selectedRelIds)
            if (relById.TryGetValue(id, out var rel) &&
                (!ClassSatisfied(rel.OriginClassId) || !ClassSatisfied(rel.DestinationClassId)))
                errors.Add($"Relationship '{rel.Name}' (id {id}) requires its origin and destination " +
                           "classes to be approved or already promoted.");

        foreach (var id in selectedEdgeIds)
        {
            if (!edgeById.TryGetValue(id, out var edge)) continue;
            var missing = new List<string>();
            if (!RecordSatisfied(edge.OriginRecordId)) missing.Add("origin record");
            if (!RecordSatisfied(edge.DestinationRecordId)) missing.Add("destination record");
            if (!RelSatisfied(edge.ExtractionRelationshipId)) missing.Add("relationship");
            if (missing.Count > 0)
                errors.Add($"Edge (id {id}) requires its {string.Join(", ", missing)} " +
                           "to be approved or already promoted.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Cannot promote the selected items because some dependencies were not included:\n" +
                string.Join("\n", errors));

        // Snapshot what's already promoted so the response can report only this round's work.
        var classesPromotedBefore = stagingClasses.Where(c => c.PromotedId.HasValue).Select(c => c.Id).ToHashSet();
        var recordsPromotedBefore = stagingRecords.Where(r => r.PromotedId.HasValue).Select(r => r.Id).ToHashSet();
        var relsPromotedBefore = stagingRelationships.Where(r => r.PromotedId.HasValue).Select(r => r.Id).ToHashSet();
        var edgesPromotedBefore = stagingEdges.Where(e => e.PromotedId.HasValue).Select(e => e.Id).ToHashSet();

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var classIdMap = await PromoteClasses(stagingClasses, selectedClassIds, organizationId, projectId,
                extractionId, currentUserId, now);
            var recordIdMap = await PromoteRecords(stagingRecords, selectedRecordIds, classIdMap, organizationId,
                projectId, extractionId, currentUserId, now);
            var relIdMap = await PromoteRelationships(stagingRelationships, selectedRelIds, classIdMap, organizationId,
                projectId, extractionId, currentUserId, now);
            await PromoteEdges(stagingEdges, selectedEdgeIds, recordIdMap, relIdMap, organizationId, projectId,
                extractionId, currentUserId, now);

            await transaction.CommitAsync();

            extraction.Status = ComputeExtractionStatus(
                stagingClasses, stagingRecords, stagingRelationships, stagingEdges, extraction.Status);
            await _context.SaveChangesAsync();

            return new ExtractionResponseDto
            {
                Id = extractionId,
                CreatedBy = extraction.CreatedBy,
                ClassCount = stagingClasses.Count(c => c.PromotedId.HasValue && !classesPromotedBefore.Contains(c.Id)),
                RecordCount = stagingRecords.Count(r => r.PromotedId.HasValue && !recordsPromotedBefore.Contains(r.Id)),
                RelationshipCount =
                    stagingRelationships.Count(r => r.PromotedId.HasValue && !relsPromotedBefore.Contains(r.Id)),
                EdgeCount = stagingEdges.Count(e => e.PromotedId.HasValue && !edgesPromotedBefore.Contains(e.Id))
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    ///     Rejects a user-selected subset of an extraction's staged items (by explicit id and/or by
    ///     validation status), or — when <see cref="RejectExtractionRequestDto.RejectAllRemaining" /> is set
    ///     — every still-pending item. Rejection is strict and mirrors promotion: rejecting an item whose
    ///     pending dependents (records/relationships/edges that rely on it) are not also in the selection
    ///     fails with an error listing them. Rejected items are flagged in the staging tables, never
    ///     promoted, and no deeplynx rows are written. Items already promoted in a prior round are
    ///     untouched. The extraction's status is recomputed from the resulting state.
    /// </summary>
    public async Task<ExtractionResponseDto> RejectExtraction(long extractionId, RejectExtractionRequestDto request)
    {
        var extraction = await _context.Extractions.FindAsync(extractionId)
                         ?? throw new InvalidOperationException($"Extraction {extractionId} not found.");

        if (extraction.Status != ExtractionStatus.Complete &&
            extraction.Status != ExtractionStatus.PartiallyPromoted)
            throw new InvalidOperationException(
                $"Extraction {extractionId} cannot be rejected — status is '{extraction.Status}', " +
                $"expected '{ExtractionStatus.Complete}' or '{ExtractionStatus.PartiallyPromoted}'.");

        var stagingClasses = await _latticeContext.ExtractionClasses
            .Where(c => c.ExtractionId == extractionId).ToListAsync();
        var stagingRecords = await _latticeContext.ExtractionRecords
            .Where(r => r.ExtractionId == extractionId).ToListAsync();
        var stagingRelationships = await _latticeContext.ExtractionRelationships
            .Where(r => r.ExtractionId == extractionId).ToListAsync();
        var stagingEdges = await _latticeContext.ExtractionEdges
            .Where(e => e.ExtractionId == extractionId).ToListAsync();

        // An item is pending (and therefore rejectable) if it is neither promoted nor already rejected.
        bool ClassPending(ExtractionClass c) => !c.PromotedId.HasValue && !c.Rejected;
        bool RecordPending(ExtractionRecord r) => !r.PromotedId.HasValue && !r.Rejected;
        bool RelPending(ExtractionRelationship r) => !r.PromotedId.HasValue && !r.Rejected;
        bool EdgePending(ExtractionEdge e) => !e.PromotedId.HasValue && !e.Rejected;

        HashSet<long> rejectClassIds, rejectRecordIds, rejectRelIds, rejectEdgeIds;

        if (request.RejectAllRemaining)
        {
            // "Reject the rest" — every still-pending item.
            rejectClassIds = stagingClasses.Where(ClassPending).Select(c => c.Id).ToHashSet();
            rejectRecordIds = stagingRecords.Where(RecordPending).Select(r => r.Id).ToHashSet();
            rejectRelIds = stagingRelationships.Where(RelPending).Select(r => r.Id).ToHashSet();
            rejectEdgeIds = stagingEdges.Where(EdgePending).Select(e => e.Id).ToHashSet();
        }
        else
        {
            var byStatus = request.RejectByStatus.ToHashSet();
            rejectClassIds = request.ClassIds.ToHashSet();
            rejectRecordIds = request.RecordIds.ToHashSet();
            rejectRelIds = request.RelationshipIds.ToHashSet();
            rejectEdgeIds = request.EdgeIds.ToHashSet();

            if (byStatus.Count > 0)
            {
                foreach (var c in stagingClasses.Where(c => ClassPending(c) && byStatus.Contains(c.ValidationStatus!)))
                    rejectClassIds.Add(c.Id);
                foreach (var r in stagingRecords.Where(r => RecordPending(r) && byStatus.Contains(r.ValidationStatus!)))
                    rejectRecordIds.Add(r.Id);
                foreach (var r in stagingRelationships.Where(r =>
                             RelPending(r) && byStatus.Contains(r.ValidationStatus!)))
                    rejectRelIds.Add(r.Id);
                foreach (var e in stagingEdges.Where(e => EdgePending(e) && byStatus.Contains(e.ValidationStatus!)))
                    rejectEdgeIds.Add(e.Id);
            }

            if (rejectClassIds.Count == 0 && rejectRecordIds.Count == 0 &&
                rejectRelIds.Count == 0 && rejectEdgeIds.Count == 0)
                throw new InvalidOperationException("No staged items were selected for rejection.");

            // ---- Strict dependent-closure validation ----
            // Rejecting an item strands anything that depends on it, so its pending dependents must also be
            // in the selection. Compute the full required closure and report whatever the caller omitted.
            var reqRecords = new HashSet<long>(rejectRecordIds);
            var reqRels = new HashSet<long>(rejectRelIds);
            var reqEdges = new HashSet<long>(rejectEdgeIds);

            foreach (var r in stagingRecords.Where(RecordPending))
                if (rejectClassIds.Contains(r.ExtractionClassId)) reqRecords.Add(r.Id);
            foreach (var rel in stagingRelationships.Where(RelPending))
                if (rejectClassIds.Contains(rel.OriginClassId) || rejectClassIds.Contains(rel.DestinationClassId))
                    reqRels.Add(rel.Id);
            foreach (var e in stagingEdges.Where(EdgePending))
                if (reqRecords.Contains(e.OriginRecordId) || reqRecords.Contains(e.DestinationRecordId) ||
                    reqRels.Contains(e.ExtractionRelationshipId))
                    reqEdges.Add(e.Id);

            var recordById = stagingRecords.ToDictionary(r => r.Id);
            var relById = stagingRelationships.ToDictionary(r => r.Id);
            var missing = reqRecords.Where(id => !rejectRecordIds.Contains(id))
                .Select(id => $"record '{recordById[id].Name}' (id {id})")
                .Concat(reqRels.Where(id => !rejectRelIds.Contains(id))
                    .Select(id => $"relationship '{relById[id].Name}' (id {id})"))
                .Concat(reqEdges.Where(id => !rejectEdgeIds.Contains(id))
                    .Select(id => $"edge (id {id})"))
                .ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "Cannot reject the selected items because items that depend on them were not included:\n" +
                    string.Join("\n", missing));
        }

        foreach (var c in stagingClasses.Where(c => rejectClassIds.Contains(c.Id))) c.Rejected = true;
        foreach (var r in stagingRecords.Where(r => rejectRecordIds.Contains(r.Id))) r.Rejected = true;
        foreach (var r in stagingRelationships.Where(r => rejectRelIds.Contains(r.Id))) r.Rejected = true;
        foreach (var e in stagingEdges.Where(e => rejectEdgeIds.Contains(e.Id))) e.Rejected = true;
        await _latticeContext.SaveChangesAsync();

        extraction.Status = ComputeExtractionStatus(
            stagingClasses, stagingRecords, stagingRelationships, stagingEdges, extraction.Status);
        await _context.SaveChangesAsync();

        return new ExtractionResponseDto
        {
            Id = extractionId,
            CreatedBy = extraction.CreatedBy,
            ClassCount = rejectClassIds.Count,
            RecordCount = rejectRecordIds.Count,
            RelationshipCount = rejectRelIds.Count,
            EdgeCount = rejectEdgeIds.Count
        };
    }

    /// <summary>
    ///     Derives an extraction's status from its staged items. An item is "settled" when it has been
    ///     promoted, rejected, or (for classes/relationships) matched an existing ontology entity that
    ///     needs no promotion. Fully settled ⇒ <see cref="ExtractionStatus.Promoted" /> when anything was
    ///     promoted, else <see cref="ExtractionStatus.Rejected" />. Otherwise the extraction is still in
    ///     progress: <see cref="ExtractionStatus.PartiallyPromoted" /> once any item has been acted on,
    ///     falling back to its current status when nothing has been touched.
    /// </summary>
    private static string ComputeExtractionStatus(
        List<ExtractionClass> classes,
        List<ExtractionRecord> records,
        List<ExtractionRelationship> relationships,
        List<ExtractionEdge> edges,
        string currentStatus)
    {
        var allSettled =
            classes.All(c => c.Rejected || c.PromotedId.HasValue || c.OntologyClassId.HasValue) &&
            records.All(r => r.Rejected || r.PromotedId.HasValue) &&
            relationships.All(r => r.Rejected || r.PromotedId.HasValue || r.OntologyRelationshipId.HasValue) &&
            edges.All(e => e.Rejected || e.PromotedId.HasValue);

        var anyPromoted =
            classes.Any(c => c.PromotedId.HasValue) || records.Any(r => r.PromotedId.HasValue) ||
            relationships.Any(r => r.PromotedId.HasValue) || edges.Any(e => e.PromotedId.HasValue);
        var anyRejected =
            classes.Any(c => c.Rejected) || records.Any(r => r.Rejected) ||
            relationships.Any(r => r.Rejected) || edges.Any(e => e.Rejected);

        if (allSettled)
            return anyPromoted || !anyRejected ? ExtractionStatus.Promoted : ExtractionStatus.Rejected;

        if (anyPromoted || anyRejected) return ExtractionStatus.PartiallyPromoted;
        return currentStatus;
    }

    /// <summary>
    ///     Checks whether the document record and project ontology are embedded.
    ///     Any missing embeddings are queued automatically.
    ///     Throws <see cref="InvalidOperationException" /> if either is not yet ready,
    ///     so the caller can retry once embedding completes.
    /// </summary>
    private async Task EnsureEmbeddingsReady(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        Record record)
    {
        var recordEmbedded = await _context.Embeddings.AnyAsync(e => e.RecordId == recordId);

        // Split into two queries — correlated subqueries across schemas don't translate reliably in EF Core
        var projectClassIds = await _context.Classes
            .Where(c => c.ProjectId == projectId)
            .Select(c => c.Id)
            .ToListAsync();
        var projectRelationshipIds = await _context.Relationships
            .Where(r => r.ProjectId == projectId)
            .Select(r => r.Id)
            .ToListAsync();
        var ontologyEmbedded = await _context.OntologyVectors
            .AnyAsync(ov =>
                (ov.ClassId != null && projectClassIds.Contains(ov.ClassId.Value)) ||
                (ov.RelationshipId != null && projectRelationshipIds.Contains(ov.RelationshipId.Value)));

        _logger.LogInformation(
            "Embedding readiness for project {ProjectId}, record {RecordId}: " +
            "recordEmbedded={RecordEmbedded}, ontologyEmbedded={OntologyEmbedded} " +
            "({ClassCount} classes, {RelationshipCount} relationships in project)",
            projectId, recordId, recordEmbedded, ontologyEmbedded,
            projectClassIds.Count, projectRelationshipIds.Count);

        if (recordEmbedded && ontologyEmbedded) return;

        if (!recordEmbedded)
        {
            AiModelConfigResponseDto.WithToken vlmConfig;
            try
            {
                vlmConfig = await _insightBusiness.ResolveModelConfig(
                    currentUserId, organizationId, projectId, null, "vlm");
            }
            catch (KeyNotFoundException)
            {
                vlmConfig = await _insightBusiness.ResolveModelConfig(
                    currentUserId, organizationId, projectId, null, "llm");
            }

            var embeddingConfig = await _insightBusiness.ResolveModelConfig(
                currentUserId, organizationId, projectId, null, "embedding");
            _insightBusiness.TriggerEmbedding(projectId, recordId, record.Uri!, vlmConfig, embeddingConfig);
        }

        if (!ontologyEmbedded)
            await _insightBusiness.QueueInsightEmbedStrings(currentUserId, organizationId, projectId, null);

        throw new InvalidOperationException(
            "Embeddings are being generated for this extraction." +
            "Please retry the extraction in a few minutes.");
    }

    /// <summary>
    ///     Throws if the project has fewer than 2 non-default classes or no relationships — the minimum needed for
    ///     extraction.
    /// </summary>
    private async Task EnsureOntologyReady(long projectId)
    {
        // Default classes don't count
        var defaultClassNames = new[] { "Timeseries", "File" };

        var currentClasses = await _context.Classes
            .Where(c => c.ProjectId == projectId && !defaultClassNames.Contains(c.Name))
            .ToListAsync();

        var currentRelationships = await _context.Relationships
            .Where(r => r.ProjectId == projectId)
            .ToListAsync();

        if (currentClasses.Count < 2 || currentRelationships.Count < 1)
            throw new InvalidOperationException(
                $"Project {projectId} does not have sufficient ontology defined. " +
                "At least 2 non-default classes and 1 relationship are required.");
    }

    /// <summary>
    ///     Builds the LLM extraction prompt by running a similarity search and injecting the top-ranked ontology context
    ///     and document text chunks.
    /// </summary>
    private async Task<string> ConstructPrompt(long recordId, long projectId, string mode)
    {
        var results = await SearchOntologySimilarity(recordId, projectId);

        var classes = results
            .Where(r => r.Type == "class")
            .DistinctBy(r => r.ClassRelationshipId)
            .Select(r => $"{r.Name}: {r.Description}");

        var relationships = results
            .Where(r => r.Type == "relationship" && r.RelationshipPattern != null)
            .DistinctBy(r => r.ClassRelationshipId)
            .Select(r =>
                $"({r.RelationshipPattern!.OriginClassName}) -{r.RelationshipPattern.RelationshipName}-> ({r.RelationshipPattern.DestinationClassName})");

        var textChunks = results
            .Where(r => !string.IsNullOrEmpty(r.TextChunk))
            .DistinctBy(r => r.TextChunk)
            .Select(r => r.TextChunk!);

        //TODO: context_block is graph context, 2 hops from record node
        //TODO: {truncation} is for document text chunk truncation, necessary only if it exceeds a certain character limit. Plus the "...truncated" message to the LLM
        var values = new Dictionary<string, string>
        {
            ["class_list"] = string.Join("\n", classes),
            ["relationship_list"] = string.Join("\n", relationships),
            ["text"] = string.Join("\n\n", textChunks)
        };

        var templateName = mode == ExtractionMode.Strict ? "lattice_strict.md" : "lattice_discovery.md";
        return LoadPrompt(templateName, values);
    }

    /// <summary>
    ///     Loads an embedded prompt template by file name (e.g., "lattice_strict.md") and substitutes
    ///     <c>{key}</c> placeholders with the provided values. Templates are cached after first load.
    /// </summary>
    private static string LoadPrompt(string templateName, Dictionary<string, string> values)
    {
        var template = _promptTemplateCache.GetOrAdd(templateName, name =>
        {
            var resourceName = $"deeplynx.business.Prompts.{name}";
            using var stream = typeof(LatticeExtractionBusiness).Assembly.GetManifestResourceStream(resourceName)
                               ?? throw new InvalidOperationException(
                                   $"Prompt template '{name}' not found as embedded resource '{resourceName}'.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });

        foreach (var (key, value) in values)
            template = template.Replace($"{{{key}}}", value);

        return template;
    }

   
}