using System.Collections.Concurrent;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace deeplynx.business;

public class LatticeExtractionBusiness : ILatticeExtractionBusiness
{
    private static readonly ConcurrentDictionary<string, string> _promptTemplateCache = new();

    private readonly DeeplynxContext _context;
    private readonly IInsightBusiness _insightBusiness;
    private readonly InsightServiceClient _insightServiceClient;
    private readonly LatticeContext _latticeContext;
    private readonly ILogger<LatticeExtractionBusiness> _logger;
    private readonly IExtractionValidation _validationBusiness;

    public LatticeExtractionBusiness(DeeplynxContext context, LatticeContext latticeContext,
        IInsightBusiness insightBusiness, InsightServiceClient insightServiceClient,
        IExtractionValidation validationBusiness,
        ILogger<LatticeExtractionBusiness> logger)
    {
        _context = context;
        _latticeContext = latticeContext;
        _insightBusiness = insightBusiness;
        _insightServiceClient = insightServiceClient;
        _validationBusiness = validationBusiness;
        _logger = logger;
    }

    private const string FailureStageTrigger = "trigger";
    private const string FailureStageInsightRequest = "insight_request";
    private const string FailureStageInsightProcessing = "insight_processing";
    private const string FailureStageCallback = "callback";
    private const string FailureStageValidation = "validation";
    private const string FailureStageStaging = "staging";
    private const int RequiredOntologyClassCount = 2;
    private const int RequiredOntologyRelationshipCount = 1;
    private static readonly string[] DefaultOntologyClassNames = { "File", "Report", "Timeseries" };

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
        if (mode != ExtractionMode.Strict && mode != ExtractionMode.Discovery)
            throw new InvalidOperationException(
                $"Extraction mode must be '{ExtractionMode.Strict}' or '{ExtractionMode.Discovery}'.");

        var record = await _context.Records
                         .Where(r =>
                             r.Id == recordId &&
                             r.ProjectId == projectId &&
                             r.OrganizationId == organizationId &&
                             !r.IsArchived)
                         .FirstOrDefaultAsync()
                     ?? throw new InvalidOperationException(
                         $"Record {recordId} not found in organization {organizationId}, project {projectId}");

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

            _logger.LogInformation(
                "Triggering Lattice extraction {ExtractionId} for organization {OrganizationId}, project {ProjectId}, record {RecordId}, model {Model}",
                extraction.Id,
                organizationId,
                projectId,
                recordId,
                latticeModel);

            var response = await _insightServiceClient.LatticeExtraction(filledPrompt, latticeModel, queryInfo);

            if (response.IsSuccessStatusCode)
            {
                extraction.Status = ExtractionStatus.Running;
            }
            else
            {
                await MarkExtractionFailedWithStage(
                    extraction,
                    FailureStageInsightRequest,
                    $"Lattice extraction request failed with HTTP status {(int)response.StatusCode} ({response.StatusCode}).");
                throw new HttpRequestException("Lattice extraction request failed");
            }

            await _context.SaveChangesAsync();
        }
        catch (InsightServiceException ex)
        {
            _logger.LogError(
                ex,
                "Lattice extraction {ExtractionId} failed while calling Insight for project {ProjectId}, record {RecordId}, model {Model}. HTTP status: {StatusCode}. Response body: {ResponseBody}",
                extraction.Id,
                projectId,
                recordId,
                Environment.GetEnvironmentVariable("LATTICE_MODEL") ?? "Mistral-Small-3.2-24B-Instruct-2506",
                ex.StatusCode,
                ex.ResponseBody);
            await MarkExtractionFailedWithStage(
                extraction,
                FailureStageInsightRequest,
                ex.Message,
                ex);
            throw;
        }
        catch (Exception ex)
        {
            if (extraction.Status != ExtractionStatus.Failed)
            {
                await MarkExtractionFailedWithStage(
                    extraction,
                    FailureStageTrigger,
                    ex.Message,
                    ex);
            }

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
        EnsureExtractionInProject(extraction, projectId);

        var failureStage = FailureStageCallback;
        try
        {
            var mode = extraction.Mode
                       ?? throw new InvalidOperationException($"Extraction {extractionId} has no mode set.");

            failureStage = FailureStageValidation;
            var (dedupedRecords, dedupedEdges) = _validationBusiness.Deduplicate(dto);

            var allClassTypes = dedupedRecords.Select(r => r.ClassType)
                .Concat(dedupedEdges.Select(e => e.SubjectType))
                .Concat(dedupedEdges.Select(e => e.ObjectType))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();

            var classSimilarities = await _validationBusiness.NormalizeClassTypes(allClassTypes, projectId);
            var relSimilarities = await _validationBusiness.NormalizeRelationshipTypes(dedupedEdges, projectId);
            var ontologyPatterns = await _validationBusiness.GetOntologyPatterns(projectId);

            failureStage = FailureStageStaging;
            await using var transaction = await _latticeContext.Database.BeginTransactionAsync();
            try
            {
                var classes = await StageClasses(extraction.Id, allClassTypes, classSimilarities, organizationId, projectId, mode);
                var records = await StageRecords(extraction.Id, dedupedRecords, classSimilarities, ontologyPatterns, classes, organizationId, projectId, dataSourceId, mode);
                var relationships = await StageRelationships(extraction.Id, dedupedEdges, classSimilarities, relSimilarities, ontologyPatterns, classes, organizationId, projectId, mode);
                var edgeCount = await StageEdges(extraction.Id, dedupedEdges, relSimilarities, ontologyPatterns, records, relationships, organizationId, projectId, dataSourceId, mode);

                await transaction.CommitAsync();

                extraction.Status = ExtractionStatus.Complete;
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Lattice data is committed — log and continue rather than leaving status stuck as Running
                    _logger.LogError(ex, "Extraction {ExtractionId} staged successfully but status update to Complete failed", extractionId);
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
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await MarkExtractionFailedWithStage(
                    extraction,
                    FailureStageStaging,
                    ex.Message,
                    ex);
                throw;
            }
        }
        catch (Exception ex) when (extraction.Status != ExtractionStatus.Failed)
        {
            await MarkExtractionFailedWithStage(
                extraction,
                failureStage,
                ex.Message,
                ex);
            throw;
        }
    }

    /// <summary>
    ///     Marks an extraction as failed. Called when Lattice reports an error via its error callback.
    /// </summary>
    /// <param name="extractionId">The ID of the extraction to mark as failed.</param>
    /// <param name="organizationId">The ID of the organization from the callback route.</param>
    /// <param name="projectId">The ID of the project from the callback route.</param>
    /// <param name="errorMessage">Optional error message from Lattice, logged by the caller.</param>
    public async Task MarkExtractionFailed(
        long extractionId,
        long organizationId,
        long projectId,
        string? errorMessage = null)
    {
        var extraction = await _context.Extractions.FindAsync(extractionId)
                         ?? throw new InvalidOperationException($"Extraction {extractionId} not found.");
        EnsureExtractionInProject(extraction, projectId);

        await MarkExtractionFailed(extraction, errorMessage);
    }

    private async Task MarkExtractionFailed(Extraction extraction, string? errorMessage = null)
    {
        var failureMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "Insight reported that extraction failed, but did not include a failure message."
            : errorMessage.Trim();
        var failureStage = failureMessage.Contains("could not be parsed", StringComparison.OrdinalIgnoreCase)
            ? FailureStageCallback
            : FailureStageInsightProcessing;

        extraction.Status = ExtractionStatus.Failed;
        SetExtractionFailureProperties(extraction, failureStage, failureMessage);
        await _context.SaveChangesAsync();

        _logger.LogError(
            "Lattice extraction {ExtractionId} failed asynchronously at stage {FailureStage}. Message: {ErrorMessage}",
            extraction.Id,
            failureStage,
            failureMessage);
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
            .Select(e => new
            {
                e.Id,
                e.Status,
                e.Mode,
                e.CreatedBy,
                e.ProjectId,
                e.Properties
            })
            .ToListAsync();

        return extractions
            .Select(e => new ExtractionListItemDto
            {
                Id = e.Id,
                Status = e.Status,
                Mode = e.Mode,
                CreatedBy = e.CreatedBy,
                ProjectId = e.ProjectId,
                FailureMessage = GetExtractionFailureMessage(e.Properties)
            })
            .ToList();
    }


    public async Task<ExtractionStagingResponseDto> GetExtractionStaging(long extractionId)
    {
        var extraction = await _context.Extractions.FindAsync(extractionId)
                         ?? throw new InvalidOperationException($"Extraction {extractionId} not found.");

        return await GetExtractionStaging(extraction);
    }

    public async Task<ExtractionStagingResponseDto> GetExtractionStaging(
        long extractionId,
        long organizationId,
        long projectId)
    {
        var extraction = await _context.Extractions.FindAsync(extractionId)
                         ?? throw new InvalidOperationException($"Extraction {extractionId} not found.");
        EnsureExtractionInProject(extraction, projectId);

        return await GetExtractionStaging(extraction);
    }

    private async Task<ExtractionStagingResponseDto> GetExtractionStaging(Extraction extraction)
    {
        var extractionId = extraction.Id;

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
            FailureMessage = GetExtractionFailureMessage(extraction.Properties),
            Classes = classes.Select(c => new StagedClassDto
            {
                Id = c.Id,
                Name = c.Name,
                ValidationStatus = c.ValidationStatus,
                OntologyClassId = c.OntologyClassId,
                PromotedId = c.PromotedId
            }).ToList(),
            Records = records.Select(r => new StagedRecordDto
            {
                Id = r.Id,
                Name = r.Name,
                ClassName = classNameMap.GetValueOrDefault(r.ExtractionClassId),
                Attributes = r.Attributes,
                ValidationStatus = r.ValidationStatus,
                EnsembleScore = r.EnsembleScore,
                Frequency = r.Frequency,
                DeeplynxRecordId = r.DeeplynxRecordId,
                PromotedId = r.PromotedId
            }).ToList(),
            Relationships = relationships.Select(r => new StagedRelationshipDto
            {
                Id = r.Id,
                Name = r.Name,
                OriginClassName = classNameMap.GetValueOrDefault(r.OriginClassId),
                DestinationClassName = classNameMap.GetValueOrDefault(r.DestinationClassId),
                ValidationStatus = r.ValidationStatus,
                OntologyRelationshipId = r.OntologyRelationshipId,
                PromotedId = r.PromotedId
            }).ToList(),
            Edges = edges.Select(e => new StagedEdgeDto
            {
                Id = e.Id,
                OriginRecordName = recordNameMap.GetValueOrDefault(e.OriginRecordId),
                DestinationRecordName = recordNameMap.GetValueOrDefault(e.DestinationRecordId),
                RelationshipName = relNameMap.GetValueOrDefault(e.ExtractionRelationshipId),
                ValidationStatus = e.ValidationStatus,
                EnsembleScore = e.EnsembleScore,
                Frequency = e.Frequency,
                PromotedId = e.PromotedId
            }).ToList()
        };
    }

    /// <summary>
    ///     Approves or rejects a completed extraction.
    ///     On approval, all staged items are promoted into the deeplynx schema regardless of
    ///     validation status, in dependency order: classes → records → relationships → edges.
    ///     On rejection, the extraction is marked rejected and no deeplynx rows are written.
    /// </summary>
    public async Task<ExtractionResponseDto> PromoteExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long extractionId,
        bool approve)
    {
        var extraction = await _context.Extractions.FindAsync(extractionId)
                         ?? throw new InvalidOperationException($"Extraction {extractionId} not found.");
        EnsureExtractionInProject(extraction, projectId);

        if (extraction.Status != ExtractionStatus.Complete)
            throw new InvalidOperationException(
                $"Extraction {extractionId} cannot be promoted — status is '{extraction.Status}', expected 'complete'.");

        if (!approve)
        {
            extraction.Status = ExtractionStatus.Rejected;
            await _context.SaveChangesAsync();
            return new ExtractionResponseDto { Id = extractionId, CreatedBy = extraction.CreatedBy };
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

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

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var classIdMap = await PromoteClasses(stagingClasses, organizationId, projectId, extractionId,
                currentUserId, now);
            var (recordIdMap, newRecordCount) = await PromoteRecords(stagingRecords, classIdMap, organizationId,
                projectId, extractionId, currentUserId, now);
            var relIdMap = await PromoteRelationships(stagingRelationships, classIdMap, organizationId, projectId,
                extractionId, currentUserId, now);
            var edgeCount = await PromoteEdges(stagingEdges, recordIdMap, relIdMap, organizationId, projectId,
                extractionId, currentUserId, now);

            await transaction.CommitAsync();

            extraction.Status = ExtractionStatus.Promoted;
            await _context.SaveChangesAsync();

            return new ExtractionResponseDto
            {
                Id = extractionId,
                CreatedBy = extraction.CreatedBy,
                ClassCount = stagingClasses.Count(c => c.PromotedId.HasValue && c.OntologyClassId == null),
                RecordCount = newRecordCount + stagingRecords.Count(r => r.DeeplynxRecordId.HasValue),
                RelationshipCount =
                    stagingRelationships.Count(r => r.PromotedId.HasValue && r.OntologyRelationshipId == null),
                EdgeCount = edgeCount
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<Dictionary<string, long>> StageClasses(
        long extractionId,
        IEnumerable<string> allClassTypes,
        Dictionary<string, SimilarityResult?> classSimilarities,
        long organizationId,
        long projectId,
        string mode)
    {
        var uniqueClassTypes = allClassTypes
            .Where(classType => !string.IsNullOrWhiteSpace(classType))
            .Select(classType => classType.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var extractionClasses = uniqueClassTypes.Select(classType =>
        {
            classSimilarities.TryGetValue(classType, out var match);
            return new ExtractionClass
            {
                ExtractionId = extractionId,
                Name = match?.OntologyEntityName ?? classType,
                OntologyClassId = match?.OntologyEntityId,
                ValidationStatus = match != null
                    ? ExtractionValidationStatus.Valid
                    : ExtractionValidationStatus.InvalidSchema,
                OrganizationId = organizationId,
                ProjectId = projectId
            };
        }).ToList();

        _latticeContext.ExtractionClasses.AddRange(extractionClasses);
        await _latticeContext.SaveChangesAsync();

        var classTypeToId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < uniqueClassTypes.Count; i++)
            classTypeToId[uniqueClassTypes[i]] = extractionClasses[i].Id;

        return classTypeToId;
    }

    private async Task<Dictionary<string, long>> StageRecords(
        long extractionId,
        List<DedupedRecord> records,
        Dictionary<string, SimilarityResult?> classSimilarities,
        HashSet<OntologyPattern> ontologyPatterns,
        Dictionary<string, long> classTypeToId,
        long organizationId,
        long projectId,
        long dataSourceId,
        string mode)
    {
        if (!records.Any()) return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var validRecords = records
            .Where(record =>
                !string.IsNullOrWhiteSpace(record.Name) &&
                !string.IsNullOrWhiteSpace(record.ClassType))
            .ToList();

        var malformedCount = records.Count - validRecords.Count;
        if (malformedCount > 0)
        {
            _logger.LogWarning(
                "Skipping {MalformedCount} malformed Lattice records for extraction {ExtractionId}",
                malformedCount,
                extractionId);
        }

        if (!validRecords.Any()) return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var maxFrequency = validRecords.Max(r => r.Frequency);

        // Batch KG lookup — inherit canonical name if the instance already exists in the graph
        var recordNames = validRecords.Select(r => r.Name.Trim()).ToList();
        var kgMatches = await _context.Records
            .Where(r => r.ProjectId == projectId && recordNames.Contains(r.Name))
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();
        var nameToKg = kgMatches.ToDictionary(r => r.Name, r => r, StringComparer.OrdinalIgnoreCase);

        var extractionRecords = new List<ExtractionRecord>();
        var stagedRecordNames = new List<string>();

        foreach (var record in validRecords)
        {
            var recordName = record.Name.Trim();
            var classType = record.ClassType.Trim();

            if (!classTypeToId.TryGetValue(classType, out var extractionClassId))
            {
                _logger.LogWarning(
                    "Skipping staged record {RecordName} because class type {ClassType} was not staged for extraction {ExtractionId}",
                    recordName,
                    classType,
                    extractionId);
                continue;
            }

            classSimilarities.TryGetValue(classType, out var classMatch);
            nameToKg.TryGetValue(recordName, out var kgRecord);

            var embeddingPlausibility = classMatch?.Score ?? 0.0;
            var statFreq = maxFrequency > 0 ? (double)record.Frequency / maxFrequency : 0.0;

            var normalizedClassName = classMatch?.OntologyEntityName;
            var structuralConsistency = normalizedClassName != null && ontologyPatterns.Any(p =>
                string.Equals(p.OriginClassName, normalizedClassName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.DestinationClassName, normalizedClassName, StringComparison.OrdinalIgnoreCase))
                ? 1.0
                : 0.0;

            extractionRecords.Add(new ExtractionRecord
            {
                ExtractionId = extractionId,
                ExtractionClassId = extractionClassId,
                Name = kgRecord?.Name ?? recordName,
                Attributes = record.Attributes?.ToJsonString(),
                OrganizationId = organizationId,
                ProjectId = projectId,
                DataSourceId = dataSourceId,
                DeeplynxRecordId = kgRecord?.Id,
                ValidationStatus = classMatch != null
                    ? ExtractionValidationStatus.Valid
                    : ExtractionValidationStatus.InvalidSchema,
                Frequency = record.Frequency,
                LlmScore = record.Confidence,
                EmbeddingPlausibility = embeddingPlausibility,
                StatisticalFrequency = statFreq,
                StructuralConsistency = structuralConsistency,
                EnsembleScore = _validationBusiness.CalculateEnsembleScore(
                    record.Confidence, embeddingPlausibility, statFreq, structuralConsistency)
            });
            stagedRecordNames.Add(recordName);
        }

        if (!extractionRecords.Any()) return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        _latticeContext.ExtractionRecords.AddRange(extractionRecords);
        await _latticeContext.SaveChangesAsync();

        var nameToId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < stagedRecordNames.Count; i++)
            nameToId[stagedRecordNames[i]] = extractionRecords[i].Id;

        return nameToId;
    }

    private async Task<Dictionary<string, long>> StageRelationships(
        long extractionId,
        List<DedupedEdge> edges,
        Dictionary<string, SimilarityResult?> classSimilarities,
        Dictionary<string, SimilarityResult?> relSimilarities,
        HashSet<OntologyPattern> ontologyPatterns,
        Dictionary<string, long> classTypeToId,
        long organizationId,
        long projectId,
        string mode)
    {
        var validEdges = edges
            .Where(e =>
                !string.IsNullOrWhiteSpace(e.SubjectType) &&
                !string.IsNullOrWhiteSpace(e.RelationshipType) &&
                !string.IsNullOrWhiteSpace(e.ObjectType))
            .ToList();

        var malformedCount = edges.Count - validEdges.Count;
        if (malformedCount > 0)
        {
            _logger.LogWarning(
                "Skipping {MalformedCount} malformed Lattice relationships for extraction {ExtractionId}",
                malformedCount,
                extractionId);
        }

        if (!validEdges.Any()) return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        // Unique (subjectType, relType, objectType) patterns — one ExtractionRelationship per pattern
        var uniquePatterns = validEdges
            .GroupBy(e => RelationshipPatternKey(
                e.SubjectType,
                e.RelationshipType,
                e.ObjectType))
            .Select(g => g.First())
            .ToList();

        var extractionRelationships = new List<ExtractionRelationship>();
        var patternKeys = new List<string>();

        foreach (var edge in uniquePatterns)
        {
            var subjectType = edge.SubjectType.Trim();
            var relationshipType = edge.RelationshipType.Trim();
            var objectType = edge.ObjectType.Trim();

            relSimilarities.TryGetValue(relationshipType, out var relMatch);
            classSimilarities.TryGetValue(subjectType, out var subjectMatch);
            classSimilarities.TryGetValue(objectType, out var objectMatch);

            string validationStatus;
            if (relMatch == null)
            {
                validationStatus = ExtractionValidationStatus.InvalidSchema;
            }
            else
            {
                var normalizedSubject = subjectMatch?.OntologyEntityName ?? subjectType;
                var normalizedObject = objectMatch?.OntologyEntityName ?? objectType;

                var patternExists = ontologyPatterns.Any(p =>
                    string.Equals(p.OriginClassName, normalizedSubject, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.RelationshipName, relMatch.OntologyEntityName,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.DestinationClassName, normalizedObject, StringComparison.OrdinalIgnoreCase));

                validationStatus = patternExists
                    ? ExtractionValidationStatus.Valid
                    : mode == ExtractionMode.Discovery && subjectMatch != null && objectMatch != null
                        ? ExtractionValidationStatus.NovelDiscovery
                        : ExtractionValidationStatus.InvalidSchema;
            }

            if (!classTypeToId.TryGetValue(subjectType, out var originClassId) ||
                !classTypeToId.TryGetValue(objectType, out var destinationClassId))
            {
                _logger.LogWarning(
                    "Skipping relationship pattern {SubjectType} - {RelationshipType} -> {ObjectType} because one or both classes were not staged for extraction {ExtractionId}",
                    subjectType,
                    relationshipType,
                    objectType,
                    extractionId);
                continue;
            }

            patternKeys.Add(RelationshipPatternKey(subjectType, relationshipType, objectType));

            extractionRelationships.Add(new ExtractionRelationship
            {
                ExtractionId = extractionId,
                OriginClassId = originClassId,
                DestinationClassId = destinationClassId,
                Name = relMatch?.OntologyEntityName ?? relationshipType,
                OntologyRelationshipId = relMatch?.OntologyEntityId,
                ValidationStatus = validationStatus,
                OrganizationId = organizationId,
                ProjectId = projectId
            });
        }

        _latticeContext.ExtractionRelationships.AddRange(extractionRelationships);
        await _latticeContext.SaveChangesAsync();

        var keyToId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < patternKeys.Count; i++)
            keyToId[patternKeys[i]] = extractionRelationships[i].Id;

        return keyToId;
    }

    private async Task<int> StageEdges(
        long extractionId,
        List<DedupedEdge> edges,
        Dictionary<string, SimilarityResult?> relSimilarities,
        HashSet<OntologyPattern> ontologyPatterns,
        Dictionary<string, long> instanceNameToRecordId,
        Dictionary<string, long> relationshipKeyToId,
        long organizationId,
        long projectId,
        long dataSourceId,
        string mode)
    {
        if (!edges.Any()) return 0;

        var validEdges = edges
            .Where(e =>
                !string.IsNullOrWhiteSpace(e.Subject) &&
                !string.IsNullOrWhiteSpace(e.SubjectType) &&
                !string.IsNullOrWhiteSpace(e.RelationshipType) &&
                !string.IsNullOrWhiteSpace(e.Object) &&
                !string.IsNullOrWhiteSpace(e.ObjectType))
            .ToList();

        var malformedCount = edges.Count - validEdges.Count;
        if (malformedCount > 0)
        {
            _logger.LogWarning(
                "Skipping {MalformedCount} malformed Lattice edges for extraction {ExtractionId}",
                malformedCount,
                extractionId);
        }

        if (!validEdges.Any()) return 0;

        var maxFrequency = validEdges.Max(e => e.Frequency);

        var relationshipIds = relationshipKeyToId.Values.Distinct().ToList();
        var relValidationById = await _latticeContext.ExtractionRelationships
            .Where(r => relationshipIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.ValidationStatus);

        var extractionEdges = new List<ExtractionEdge>();
        foreach (var edge in validEdges)
        {
            var subject = edge.Subject.Trim();
            var relationshipType = edge.RelationshipType.Trim();
            var obj = edge.Object.Trim();
            var subjectType = edge.SubjectType.Trim();
            var objectType = edge.ObjectType.Trim();

            // Skip edges whose subject or object wasn't staged as a record — this can happen when
            // the LLM references an entity in a relationship that it didn't include in the classes array
            if (!instanceNameToRecordId.TryGetValue(subject, out var originRecordId))
            {
                _logger.LogWarning(
                    "Skipping edge {Subject} - {RelationshipType} -> {Object} because subject record was not staged for extraction {ExtractionId}",
                    subject,
                    relationshipType,
                    obj,
                    extractionId);
                continue;
            }

            if (!instanceNameToRecordId.TryGetValue(obj, out var destRecordId))
            {
                _logger.LogWarning(
                    "Skipping edge {Subject} - {RelationshipType} -> {Object} because object record was not staged for extraction {ExtractionId}",
                    subject,
                    relationshipType,
                    obj,
                    extractionId);
                continue;
            }

            relSimilarities.TryGetValue(relationshipType, out var relMatch);
            var patternKey = RelationshipPatternKey(subjectType, relationshipType, objectType);
            if (!relationshipKeyToId.TryGetValue(patternKey, out var relId))
            {
                _logger.LogWarning(
                    "Skipping edge {Subject} - {RelationshipType} -> {Object} because relationship pattern was not staged for extraction {ExtractionId}",
                    subject,
                    relationshipType,
                    obj,
                    extractionId);
                continue;
            }

            if (!relValidationById.TryGetValue(relId, out var validationStatus))
            {
                validationStatus = ExtractionValidationStatus.InvalidSchema;
            }

            var embeddingPlausibility = relMatch?.Score ?? 0.0;
            var statFreq = maxFrequency > 0 ? (double)edge.Frequency / maxFrequency : 0.0;
            var structuralConsistency = validationStatus == ExtractionValidationStatus.Valid ? 1.0 : 0.0;

            extractionEdges.Add(new ExtractionEdge
            {
                ExtractionId = extractionId,
                ExtractionRelationshipId = relId,
                OriginRecordId = originRecordId,
                DestinationRecordId = destRecordId,
                OrganizationId = organizationId,
                ProjectId = projectId,
                DataSourceId = dataSourceId,
                ValidationStatus = validationStatus,
                Frequency = edge.Frequency,
                LlmScore = edge.Confidence,
                EmbeddingPlausibility = embeddingPlausibility,
                StatisticalFrequency = statFreq,
                StructuralConsistency = structuralConsistency,
                EnsembleScore = _validationBusiness.CalculateEnsembleScore(
                    edge.Confidence, embeddingPlausibility, statFreq, structuralConsistency)
            });
        }

        _latticeContext.ExtractionEdges.AddRange(extractionEdges);
        await _latticeContext.SaveChangesAsync();

        return extractionEdges.Count;
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
            .Where(c =>
                c.ProjectId == projectId &&
                !c.IsArchived &&
                !DefaultOntologyClassNames.Contains(c.Name))
            .Select(c => c.Id)
            .ToListAsync();
        var projectRelationshipIds = await _context.Relationships
            .Where(r => r.ProjectId == projectId && !r.IsArchived)
            .Select(r => r.Id)
            .ToListAsync();
        var embeddedClassCount = await _context.OntologyVectors
            .Where(ov => ov.ClassId != null && projectClassIds.Contains(ov.ClassId.Value))
            .Select(ov => ov.ClassId!.Value)
            .Distinct()
            .CountAsync();
        var embeddedRelationshipCount = await _context.OntologyVectors
            .Where(ov => ov.RelationshipId != null && projectRelationshipIds.Contains(ov.RelationshipId.Value))
            .Select(ov => ov.RelationshipId!.Value)
            .Distinct()
            .CountAsync();
        var ontologyEmbedded =
            projectClassIds.Count >= RequiredOntologyClassCount &&
            projectRelationshipIds.Count >= RequiredOntologyRelationshipCount &&
            embeddedClassCount >= RequiredOntologyClassCount &&
            embeddedRelationshipCount >= RequiredOntologyRelationshipCount;

        _logger.LogInformation(
            "Embedding readiness for project {ProjectId}, record {RecordId}: " +
            "recordEmbedded={RecordEmbedded}, ontologyEmbedded={OntologyEmbedded} " +
            "({ClassCount} non-default classes, {RelationshipCount} relationships in project; " +
            "{EmbeddedClassCount} classes embedded, {EmbeddedRelationshipCount} relationships embedded)",
            projectId, recordId, recordEmbedded, ontologyEmbedded,
            projectClassIds.Count, projectRelationshipIds.Count,
            embeddedClassCount, embeddedRelationshipCount);

        if (recordEmbedded && ontologyEmbedded) return;

        if (!recordEmbedded)
        {
            if (string.IsNullOrWhiteSpace(record.Uri))
                throw new InvalidOperationException(
                    $"Record {recordId} does not have a file URI available for Insight embedding.");

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

    public async Task<EmbeddingStatusResponseDto> GetEmbeddingStatus(long projectId)
    {
        var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            return new EmbeddingStatusResponseDto
            {
                ClassCount = 0,
                EmbeddedClassCount = 0,
                RelationshipCount = 0,
                EmbeddedRelationshipCount = 0,
                OntologyReady = false
            };
        }

        var classIds = await _context.Classes
            .Where(c =>
                c.ProjectId == projectId &&
                !c.IsArchived &&
                !DefaultOntologyClassNames.Contains(c.Name))
            .Select(c => c.Id)
            .ToListAsync();
        var relationshipIds = await _context.Relationships
            .Where(r => r.ProjectId == projectId && !r.IsArchived)
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

        var embeddedClassCount = Math.Min(embeddedClassIds.Count, RequiredOntologyClassCount);
        var embeddedRelationshipCount = Math.Min(embeddedRelationshipIds.Count, RequiredOntologyRelationshipCount);

        return new EmbeddingStatusResponseDto
        {
            ClassCount = RequiredOntologyClassCount,
            EmbeddedClassCount = embeddedClassCount,
            RelationshipCount = RequiredOntologyRelationshipCount,
            EmbeddedRelationshipCount = embeddedRelationshipCount,
            OntologyReady =
                classIds.Count >= RequiredOntologyClassCount &&
                relationshipIds.Count >= RequiredOntologyRelationshipCount &&
                embeddedClassCount >= RequiredOntologyClassCount &&
                embeddedRelationshipCount >= RequiredOntologyRelationshipCount
        };
    }

    /// <summary>
    ///     Throws if the project has fewer than 2 non-default classes or no relationships — the minimum needed for
    ///     extraction.
    /// </summary>
    private async Task EnsureOntologyReady(long projectId)
    {
        var currentClassCount = await _context.Classes
            .CountAsync(c =>
                c.ProjectId == projectId &&
                !c.IsArchived &&
                !DefaultOntologyClassNames.Contains(c.Name));

        var currentRelationshipCount = await _context.Relationships
            .CountAsync(r => r.ProjectId == projectId && !r.IsArchived);

        if (currentClassCount < RequiredOntologyClassCount ||
            currentRelationshipCount < RequiredOntologyRelationshipCount)
        {
            throw new InvalidOperationException(
                $"Project {projectId} does not have sufficient ontology defined. " +
                "At least 2 non-default classes and 1 relationship are required.");
        }
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

    /// <summary>
    ///     Promotes novel_discovery and invalid_schema classes that have no existing ontology match into deeplynx.classes.
    ///     Valid classes already exist in the ontology and are not re-created.
    ///     Returns a map of ExtractionClass.Id → deeplynx Class id for use in downstream steps.
    /// </summary>
    private async Task<Dictionary<long, long?>> PromoteClasses(
        List<ExtractionClass> stagingClasses,
        long organizationId, long projectId, long extractionId,
        long currentUserId, DateTime now)
    {
        // PromotedId is persisted to the lattice DB outside the deeplynx transaction, so a prior
        // rolled-back attempt can leave stale references. Clear any that no longer exist in deeplynx.
        var pendingIds = stagingClasses
            .Where(c => c.PromotedId.HasValue && c.OntologyClassId == null)
            .Select(c => c.PromotedId!.Value)
            .ToList();
        if (pendingIds.Count > 0)
        {
            var validIds = (await _context.Classes
                .Where(c => pendingIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync()).ToHashSet();
            foreach (var sc in stagingClasses.Where(c =>
                         c.PromotedId.HasValue && !validIds.Contains(c.PromotedId!.Value)))
                sc.PromotedId = null;
        }

        // Pre-load any classes that already exist in the project with the same name,
        // so we reuse them rather than hitting unique_class_name on create.
        var namesToCreate = stagingClasses
            .Where(c => (c.ValidationStatus == ExtractionValidationStatus.NovelDiscovery ||
                         c.ValidationStatus == ExtractionValidationStatus.InvalidSchema) &&
                        c.OntologyClassId == null && c.PromotedId == null)
            .Select(c => c.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingClassByName = namesToCreate.Count > 0
            ? (await _context.Classes
                .Where(c => c.ProjectId == projectId && namesToCreate.Contains(c.Name))
                .Select(c => new { c.Id, c.Name })
                .ToListAsync())
            .ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        // Deduplicate within the batch so two staging entries with the same name create one class.
        var nameToNewClassId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var sc in stagingClasses.Where(c =>
                     (c.ValidationStatus == ExtractionValidationStatus.NovelDiscovery ||
                      c.ValidationStatus == ExtractionValidationStatus.InvalidSchema) &&
                     c.OntologyClassId == null &&
                     c.PromotedId == null))
        {
            if (existingClassByName.TryGetValue(sc.Name, out var existingId))
            {
                sc.PromotedId = existingId;
                continue;
            }

            if (nameToNewClassId.TryGetValue(sc.Name, out var batchId))
            {
                sc.PromotedId = batchId;
                continue;
            }

            var newClass = new Class
            {
                Name = sc.Name,
                OrganizationId = organizationId,
                ProjectId = projectId,
                IsArchived = false,
                LastUpdatedAt = now,
                LastUpdatedBy = currentUserId,
                ExtractionId = extractionId
            };
            _context.Classes.Add(newClass);
            await _context.SaveChangesAsync();
            sc.PromotedId = newClass.Id;
            nameToNewClassId[sc.Name] = newClass.Id;
        }

        await _latticeContext.SaveChangesAsync();

        // Valid classes resolve to their matched ontology class; novel/invalid to the newly created class
        return stagingClasses.ToDictionary(c => c.Id, c => c.OntologyClassId ?? c.PromotedId);
    }

    /// <summary>
    ///     Promotes extraction records into deeplynx.records.
    ///     Records already matched to a KG entity (deeplynx_record_id set) are linked rather than re-created.
    ///     Returns a map of ExtractionRecord.Id → deeplynx Record id, and the count of newly created records.
    /// </summary>
    private async Task<(Dictionary<long, long> RecordIdMap, int NewRecordCount)> PromoteRecords(
        List<ExtractionRecord> stagingRecords,
        Dictionary<long, long?> classIdMap,
        long organizationId, long projectId, long extractionId,
        long currentUserId, DateTime now)
    {
        var newRecordCount = 0;

        foreach (var sr in stagingRecords)
        {
            if (sr.DeeplynxRecordId.HasValue)
            {
                // Record already exists in the KG — link promoted_id without creating a duplicate
                sr.PromotedId = sr.DeeplynxRecordId.Value;
                continue;
            }

            classIdMap.TryGetValue(sr.ExtractionClassId, out var resolvedClassId);

            var newRecord = new Record
            {
                Name = sr.Name,
                OriginalId = Guid.NewGuid().ToString(),
                Description = string.Empty,
                Properties = sr.Attributes ?? "{}",
                ClassId = resolvedClassId,
                DataSourceId = sr.DataSourceId,
                ProjectId = projectId,
                OrganizationId = organizationId,
                IsArchived = false,
                Embedded = false,
                LastUpdatedAt = now,
                LastUpdatedBy = currentUserId,
                ExtractionId = extractionId
            };
            _context.Records.Add(newRecord);
            await _context.SaveChangesAsync();
            sr.PromotedId = newRecord.Id;
            newRecordCount++;
        }

        await _latticeContext.SaveChangesAsync();

        var recordIdMap = stagingRecords
            .Where(r => r.PromotedId.HasValue)
            .ToDictionary(r => r.Id, r => r.PromotedId!.Value);

        return (recordIdMap, newRecordCount);
    }

    /// <summary>
    ///     Promotes novel_discovery and invalid_schema relationships that have no existing ontology match into
    ///     deeplynx.relationships.
    ///     Valid relationships already exist in the ontology and are not re-created.
    ///     Returns a map of ExtractionRelationship.Id → deeplynx Relationship id for use in edge promotion.
    /// </summary>
    private async Task<Dictionary<long, long?>> PromoteRelationships(
        List<ExtractionRelationship> stagingRelationships,
        Dictionary<long, long?> classIdMap,
        long organizationId, long projectId, long extractionId,
        long currentUserId, DateTime now)
    {
        // Same stale-reference guard as PromoteClasses
        var pendingRelIds = stagingRelationships
            .Where(r => r.PromotedId.HasValue && r.OntologyRelationshipId == null)
            .Select(r => r.PromotedId!.Value)
            .ToList();
        if (pendingRelIds.Count > 0)
        {
            var validRelIds = (await _context.Relationships
                .Where(r => pendingRelIds.Contains(r.Id))
                .Select(r => r.Id)
                .ToListAsync()).ToHashSet();
            foreach (var sr in stagingRelationships.Where(r =>
                         r.PromotedId.HasValue && !validRelIds.Contains(r.PromotedId!.Value)))
                sr.PromotedId = null;
        }

        // Multiple staging relationships can share the same name (same rel type, different class pairs).
        // Also, the name may already exist in the project from a prior approved extraction.
        // In both cases reuse the existing relationship rather than hitting unique_relationship_name.
        var relNamesToCreate = stagingRelationships
            .Where(r => (r.ValidationStatus == ExtractionValidationStatus.NovelDiscovery ||
                         r.ValidationStatus == ExtractionValidationStatus.InvalidSchema) &&
                        r.OntologyRelationshipId == null && r.PromotedId == null)
            .Select(r => r.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingRelByName = relNamesToCreate.Count > 0
            ? (await _context.Relationships
                .Where(r => r.ProjectId == projectId && relNamesToCreate.Contains(r.Name))
                .Select(r => new { r.Id, r.Name })
                .ToListAsync())
            .ToDictionary(r => r.Name, r => r.Id, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var nameToPromotedRelId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var sr in stagingRelationships.Where(r =>
                     (r.ValidationStatus == ExtractionValidationStatus.NovelDiscovery ||
                      r.ValidationStatus == ExtractionValidationStatus.InvalidSchema) &&
                     r.OntologyRelationshipId == null &&
                     r.PromotedId == null))
        {
            if (existingRelByName.TryGetValue(sr.Name, out var existingId) ||
                nameToPromotedRelId.TryGetValue(sr.Name, out existingId))
            {
                sr.PromotedId = existingId;
                continue;
            }

            classIdMap.TryGetValue(sr.OriginClassId, out var originClassId);
            classIdMap.TryGetValue(sr.DestinationClassId, out var destClassId);

            var newRel = new Relationship
            {
                Name = sr.Name,
                OriginId = originClassId,
                DestinationId = destClassId,
                OrganizationId = organizationId,
                ProjectId = projectId,
                IsArchived = false,
                LastUpdatedAt = now,
                LastUpdatedBy = currentUserId,
                ExtractionId = extractionId
            };
            _context.Relationships.Add(newRel);
            await _context.SaveChangesAsync();
            sr.PromotedId = newRel.Id;
            nameToPromotedRelId[sr.Name] = newRel.Id;
        }

        await _latticeContext.SaveChangesAsync();

        // Valid relationships resolve to their ontology match; novel_discovery to the newly created relationship
        return stagingRelationships.ToDictionary(r => r.Id, r => r.OntologyRelationshipId ?? r.PromotedId);
    }

    /// <summary>
    ///     Promotes extraction edges into deeplynx.edges.
    ///     Edges whose origin or destination record was excluded (invalid_schema) are skipped.
    ///     Returns the count of edges created.
    /// </summary>
    private async Task<int> PromoteEdges(
        List<ExtractionEdge> stagingEdges,
        Dictionary<long, long> recordIdMap,
        Dictionary<long, long?> relIdMap,
        long organizationId, long projectId, long extractionId,
        long currentUserId, DateTime now)
    {
        var edgePairs = new List<(ExtractionEdge Staging, Edge Promoted)>();

        foreach (var se in stagingEdges)
        {
            // Skip edges where either endpoint was not promoted (e.g. invalid_schema record)
            if (!recordIdMap.TryGetValue(se.OriginRecordId, out var originRecordId)) continue;
            if (!recordIdMap.TryGetValue(se.DestinationRecordId, out var destRecordId)) continue;
            relIdMap.TryGetValue(se.ExtractionRelationshipId, out var relId);

            var newEdge = new Edge
            {
                OriginId = originRecordId,
                DestinationId = destRecordId,
                RelationshipId = relId,
                DataSourceId = se.DataSourceId,
                ProjectId = projectId,
                OrganizationId = organizationId,
                IsArchived = false,
                LastUpdatedAt = now,
                LastUpdatedBy = currentUserId,
                ExtractionId = extractionId
            };
            _context.Edges.Add(newEdge);
            edgePairs.Add((se, newEdge));
        }

        // Save all edges first so EF Core populates their IDs, then write promoted_id back to staging
        await _context.SaveChangesAsync();
        foreach (var (se, newEdge) in edgePairs)
            se.PromotedId = newEdge.Id;
        await _latticeContext.SaveChangesAsync();

        return edgePairs.Count;
    }

    /// <summary>
    ///     Builds a stable key that uniquely identifies a relationship pattern by combining
    ///     the subject type, relationship type, and object type.
    ///     Leading and trailing whitespace is removed from each component before the key is
    ///     created.
    /// </summary>
    /// <param name="subjectType">The ontology/entity type for the relationship subject.</param>
    /// <param name="relationshipType">The type or name of the relationship between the subject and object.</param>
    /// <param name="objectType">The ontology/entity type for the relationship object.</param>
    /// <returns>
    ///     A pipe-delimited relationship pattern key in the format
    ///     <c>subjectType|relationshipType|objectType</c>.
    /// </returns>
    private static string RelationshipPatternKey(
        string subjectType,
        string relationshipType,
        string objectType) =>
        $"{subjectType.Trim()}|{relationshipType.Trim()}|{objectType.Trim()}";

    /// <summary>
    ///     Parses the serialized extraction properties JSON into a mutable JSON object.
    ///     Returns an empty object when the input is null, empty, whitespace, invalid JSON,
    ///     or does not parse to a JSON object.
    /// </summary>
    /// <param name="properties">Serialized extraction properties JSON.</param>
    /// <returns>
    ///     A mutable <see cref="JsonObject"/> containing the parsed extraction properties,
    ///     or an empty object when no valid properties are available.
    /// </returns>
    private static JsonObject GetExtractionProperties(string? properties)
    {
        if (string.IsNullOrWhiteSpace(properties))
            return new JsonObject();

        try
        {
            return JsonNode.Parse(properties)?.AsObject() ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    /// <summary>
    ///     Records failure details on an extraction by updating its serialized properties.
    ///     Existing properties are preserved, and the failure stage, failure message, and
    ///     UTC failure timestamp are added or overwritten.
    /// </summary>
    /// <param name="extraction">The extraction record to update with failure metadata.</param>
    /// <param name="stage">The processing stage where the failure occurred.</param>
    /// <param name="message">The failure message to store for diagnostics and display.</param>
    private static void SetExtractionFailureProperties(
        Extraction extraction,
        string stage,
        string message)
    {
        var properties = GetExtractionProperties(extraction.Properties);
        properties["failure_stage"] = stage;
        properties["failure_message"] = message;
        properties["failed_at"] = DateTimeOffset.UtcNow.ToString("O");
        extraction.Properties = properties.ToJsonString();
    }

    /// <summary>
    ///     Reads the stored failure message from serialized extraction properties.
    ///     Returns null when the properties are missing, invalid, or do not contain a
    ///     failure message.
    /// </summary>
    /// <param name="properties">Serialized extraction properties JSON.</param>
    /// <returns>
    ///     The stored failure message, or null when no failure message is available.
    /// </returns>
    private static string? GetExtractionFailureMessage(string? properties)
    {
        var extractionProperties = GetExtractionProperties(properties);
        return extractionProperties.TryGetPropertyValue("failure_message", out var messageNode)
            ? messageNode?.GetValue<string>()
            : null;
    }

    /// <summary>
    ///     Verifies that an extraction belongs to the expected project.
    ///     Throws when the extraction is outside the requested project scope.
    /// </summary>
    /// <param name="extraction">The extraction record to validate.</param>
    /// <param name="projectId">The expected project ID.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the extraction does not belong to the specified project.
    /// </exception>
    private static void EnsureExtractionInProject(Extraction extraction, long projectId)
    {
        if (extraction.ProjectId != projectId)
            throw new InvalidOperationException($"Extraction {extraction.Id} not found in project {projectId}.");
    }

    /// <summary>
    ///     Persists a failed extraction state for errors raised inside the trigger or callback
    ///     workflow, storing the processing stage and diagnostic message for display and logging.
    /// </summary>
    /// <param name="extraction">The extraction record to mark as failed.</param>
    /// <param name="stage">The processing stage where the failure occurred.</param>
    /// <param name="message">The failure message generated by the current workflow.</param>
    /// <param name="exception">
    ///     Optional exception associated with the failure. When provided, it is included in the
    ///     structured error log.
    /// </param>
    private async Task MarkExtractionFailedWithStage(
        Extraction extraction,
        string stage,
        string message,
        Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            message = "Insight reported that extraction failed, but did not include a failure message.";

        extraction.Status = ExtractionStatus.Failed;
        SetExtractionFailureProperties(extraction, stage, message);

        await _context.SaveChangesAsync();

        if (exception != null)
        {
            _logger.LogError(
                exception,
                "Lattice extraction {ExtractionId} failed at stage {FailureStage}: {FailureMessage}",
                extraction.Id,
                stage,
                message);
        }
        else
        {
            _logger.LogError(
                "Lattice extraction {ExtractionId} failed at stage {FailureStage}: {FailureMessage}",
                extraction.Id,
                stage,
                message);
        }
    }
}
