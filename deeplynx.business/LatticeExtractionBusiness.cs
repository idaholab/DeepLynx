using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class LatticeExtractionBusiness : ILatticeExtractionBusiness
{
    private readonly DeeplynxContext _context;
    private readonly StagingContext _stagingContext;
    private readonly IInsightBusiness _insightBusiness;

    public LatticeExtractionBusiness(DeeplynxContext context, StagingContext stagingContext,
        ITokenBusiness tokenBusiness, IInsightBusiness insightBusiness)
    {
        _context = context;
        _stagingContext = stagingContext;
        _insightBusiness = insightBusiness;
    }

    /// <summary>
    ///     Stage extractions from Lattice. Inserts staged records, classes, edges, and relationships.
    ///     When <paramref name="extractionId" /> is supplied (Lattice callback flow), the existing
    ///     Extraction record is updated to Complete on success or Failed on error, rather than creating
    ///     a new one. When omitted (manual staging flow), a new Extraction is created and immediately
    ///     marked Complete.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">
    ///     The ID of the organization to which the staged classes, records, edges and relationships
    ///     belong
    /// </param>
    /// <param name="projectId">The ID of the project to which the staged classes, records, edges and relationships belong</param>
    /// <param name="dataSourceId">The ID of the datasource to which the staged records and edges belong</param>
    /// <param name="dto">
    ///     CreateExtractionRequestDTO that contains the CreateDTOs for Classes, Records, Edges, and
    ///     Relationships as well as extraction configurations
    /// </param>
    /// <param name="extractionId">
    ///     When set, ties this payload to an existing Extraction created during trigger.
    ///     Lattice passes this as a query param on its success callback.
    /// </param>
    /// <returns>ExtractionResponseDto which contains counts of staged entities</returns>
    /// <exception cref="Exception">Returned if error occurs during extraction transaction</exception>
    public async Task<ExtractionResponseDto> LatticeEntityStaging(
        long currentUserId,
        long organizationId,
        long projectId,
        long dataSourceId,
        CreateStagingRequestDto dto,
        long? extractionId = null)
    {
        // When extractionId is supplied, Lattice is calling back after completing an extraction
        // we already created. Update that record rather than creating a new one.
        var isCallback = extractionId.HasValue;
        Extraction extraction;

        if (isCallback)
        {
            extraction = await _context.Extractions.FindAsync(extractionId!.Value)
                         ?? throw new InvalidOperationException($"Extraction {extractionId} not found.");
            extraction.Properties = dto.Properties?.ToJsonString();
        }
        else
        {
            extraction = new Extraction
            {
                Properties = dto.Properties?.ToJsonString(),
                CreatedBy = currentUserId,
                Status = ExtractionStatus.Complete
            };
            _context.Extractions.Add(extraction);
            await _context.SaveChangesAsync();
        }

        await using var stagingTransaction = await _stagingContext.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            // Maps used to resolve cross-references within this payload
            var classNameToStagingId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var relationshipNameToStagingId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var originalIdToStagingRecordId = new Dictionary<string, long>();

            foreach (var classDto in dto.Classes ?? [])
            {
                var stagingClass = new Class
                {
                    Name = classDto.Name,
                    Description = classDto.Description,
                    Properties = classDto.Properties?.ToJsonString(),
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    LastUpdatedAt = now,
                    LastUpdatedBy = currentUserId,
                    ExtractionId = extraction.Id
                };
                _stagingContext.Classes.Add(stagingClass);
                await _stagingContext.SaveChangesAsync();
                classNameToStagingId[stagingClass.Name] = stagingClass.Id;
            }

            // OriginId/DestinationId are class IDs — resolve from this payload's staging classes only.
            // If the class only exists in deeplynx, leave the ID null and store the name as a shadow
            // property so promotion can resolve it by name.
            foreach (var relDto in dto.Relationships ?? [])
            {
                var originId = relDto.OriginId ?? ResolveClassId(relDto.OriginName, classNameToStagingId);
                var destinationId =
                    relDto.DestinationId ?? ResolveClassId(relDto.DestinationName, classNameToStagingId);

                var stagingRelationship = new Relationship
                {
                    Name = relDto.Name,
                    Description = relDto.Description,
                    Properties = relDto.Properties?.ToJsonString(),
                    OriginId = originId,
                    DestinationId = destinationId,
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    LastUpdatedAt = now,
                    LastUpdatedBy = currentUserId,
                    ExtractionId = extraction.Id
                };
                _stagingContext.Relationships.Add(stagingRelationship);

                // Store unresolved class names as shadow properties for promotion
                if (originId == null && relDto.OriginName != null)
                    _stagingContext.Entry(stagingRelationship).Property("OriginName").CurrentValue = relDto.OriginName;
                if (destinationId == null && relDto.DestinationName != null)
                    _stagingContext.Entry(stagingRelationship).Property("DestinationName").CurrentValue =
                        relDto.DestinationName;

                await _stagingContext.SaveChangesAsync();
                relationshipNameToStagingId[stagingRelationship.Name] = stagingRelationship.Id;
            }

            // ClassId is resolved from this payload's staging classes only.
            // If the class only exists in deeplynx, ClassId stays null and the class name is stored
            // so promotion can resolve it by name.
            foreach (var recordDto in dto.Records ?? [])
            {
                var classId = recordDto.ClassId;
                if (classId == null && recordDto.ClassName != null
                                    && classNameToStagingId.TryGetValue(recordDto.ClassName, out var stagingClassId))
                    classId = stagingClassId;

                var stagingRecord = new Record
                {
                    Name = recordDto.Name,
                    OriginalId = recordDto.OriginalId,
                    Properties = recordDto.Properties.ToJsonString(),
                    Description = recordDto.Description ?? string.Empty,
                    Uri = recordDto.Uri,
                    FileType = recordDto.FileType,
                    ObjectStorageId = recordDto.ObjectStorageId,
                    ClassId = classId,
                    DataSourceId = dataSourceId,
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    LastUpdatedAt = now,
                    LastUpdatedBy = currentUserId,
                    ExtractionId = extraction.Id
                };
                _stagingContext.Records.Add(stagingRecord);

                // Store unresolved class name as shadow property for promotion
                if (classId == null && recordDto.ClassName != null)
                    _stagingContext.Entry(stagingRecord).Property("ClassName").CurrentValue = recordDto.ClassName;

                await _stagingContext.SaveChangesAsync();
                originalIdToStagingRecordId[stagingRecord.OriginalId] = stagingRecord.Id;
            }

            // Edges where both endpoints resolve to staging records go into staging.edges (intra-schema FKs enforced).
            // Edges where either endpoint is a deeplynx-only record go into staging.cross_schema_edges,
            // which stores original_id strings resolved at promotion time.
            foreach (var edgeDto in dto.Edges ?? [])
            {
                var originId = ResolveRecordId(edgeDto.OriginOriginalId, originalIdToStagingRecordId)
                               ?? edgeDto.OriginId;
                var destinationId = ResolveRecordId(edgeDto.DestinationOriginalId, originalIdToStagingRecordId)
                                    ?? edgeDto.DestinationId;
                var relationshipId = ResolveRelationshipId(edgeDto.RelationshipName, relationshipNameToStagingId)
                                     ?? edgeDto.RelationshipId;

                var hasCrossSchemaRef = edgeDto.DeeplynxOriginOriginalId != null
                                        || edgeDto.DeeplynxDestinationOriginalId != null
                                        || edgeDto.DeeplynxRelationshipName != null;

                if (originId != null && destinationId != null && !hasCrossSchemaRef)
                    // Both endpoints resolved to staging records — standard staging edge
                    _stagingContext.Edges.Add(new Edge
                    {
                        OriginId = originId.Value,
                        DestinationId = destinationId.Value,
                        RelationshipId = relationshipId,
                        DataSourceId = dataSourceId,
                        OrganizationId = organizationId,
                        ProjectId = projectId,
                        Properties = edgeDto.Properties?.ToJsonString(),
                        LastUpdatedAt = now,
                        LastUpdatedBy = currentUserId,
                        ExtractionId = extraction.Id
                    });
                else if (hasCrossSchemaRef || originId != null || destinationId != null)
                    // At least one endpoint or relationship references a deeplynx entity — cross-schema edge
                    _stagingContext.CrossSchemaEdges.Add(new CrossSchemaEdge
                    {
                        ExtractionId = extraction.Id,
                        DataSourceId = dataSourceId,
                        OrganizationId = organizationId,
                        ProjectId = projectId,
                        Properties = edgeDto.Properties?.ToJsonString(),
                        LastUpdatedAt = now,
                        LastUpdatedBy = currentUserId,
                        OriginOriginalId = edgeDto.OriginOriginalId,
                        DeeplynxOriginOriginalId = edgeDto.DeeplynxOriginOriginalId,
                        DestinationOriginalId = edgeDto.DestinationOriginalId,
                        DeeplynxDestinationOriginalId = edgeDto.DeeplynxDestinationOriginalId,
                        RelationshipName = edgeDto.RelationshipName,
                        DeeplynxRelationshipName = edgeDto.DeeplynxRelationshipName
                    });
                // no origin or destination
            }

            await _stagingContext.SaveChangesAsync();
            await stagingTransaction.CommitAsync();

            if (isCallback)
            {
                extraction.Status = ExtractionStatus.Complete;
                await _context.SaveChangesAsync();
            }

            return new ExtractionResponseDto
            {
                Id = extraction.Id,
                Properties = extraction.Properties,
                CreatedBy = currentUserId,
                ClassCount = dto.Classes?.Count ?? 0,
                RelationshipCount = dto.Relationships?.Count ?? 0,
                RecordCount = dto.Records?.Count ?? 0,
                EdgeCount = dto.Edges?.Count ?? 0
            };
        }
        catch
        {
            await stagingTransaction.RollbackAsync();

            if (isCallback)
                extraction.Status = ExtractionStatus.Failed;
            else
                _context.Extractions.Remove(extraction);

            await _context.SaveChangesAsync();
            throw;
        }
    }

    /// <summary>
    ///     Creates a Pending Extraction record, builds ontology context via similarity search,
    ///     generates a short-lived callback token, and fires the trigger request to Lattice.
    ///     Returns immediately after Lattice acknowledges with 202; the extraction runs
    ///     asynchronously on the Lattice side and calls back when complete.
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

        // For strict mode, embeddings must exist before triggering. If they're missing, queue
        // them automatically and fail fast so the user can retry once they're ready.
        if (mode == ExtractionMode.Strict)
            await EnsureEmbeddingsReady(currentUserId, organizationId, projectId, recordId, record);

        var extraction = new Extraction
        {
            CreatedBy = currentUserId,
            Status = ExtractionStatus.Pending
        };
        _context.Extractions.Add(extraction);
        await _context.SaveChangesAsync();

        try
        {
            var ontologyContext = await SearchOntologySimilarity(
                recordId, projectId);

            // await TriggerLatticeExtraction(new LatticeExtractionTriggerRequestDto
            // {
            //     ExtractionId = extraction.Id,
            //     RecordId = recordId,
            //     DocumentUri = record.Uri,
            //     FileType = record.FileType,
            //     Mode = mode,
            //     OntologyContext = ontologyContext,
            //     NexusConfig = new LatticeNexusConfigDto
            //     {
            //         OrgId = organizationId,
            //         ProjectId = projectId,
            //         DataSourceId = dataSourceId,
            //         BaseUrl = _nexusBaseUrl,
            //         Token = callbackToken
            //     }
            // });

            extraction.Status = ExtractionStatus.Running;
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
    }

    /// <summary>
    ///     Searches for the most similar ontology terms (classes and/or relationships) in the project
    ///     by comparing a record's stored embeddings against all ontology vectors using cosine similarity.
    /// </summary>
    /// <param name="recordId">The ID of the record whose embeddings are used as the query vectors.</param>
    /// <param name="projectId">The ID of the project — only classes and relationships belonging to this project are searched.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="termType">Optional filter: "class" or "relationship". Null returns both.</param>
    public async Task<List<OntologySimilarityResultDto>> SearchOntologySimilarity(
        long recordId,
        long projectId,
        int limit = 20,
        string? termType = null)
    {
        return await _context.Database
            .SqlQuery<OntologySimilarityResultDto>($"""
                                                    SELECT name, technical_id, type, description, score, text_chunk
                                                    FROM (
                                                        SELECT 
                                                            COALESCE(c.name, rel.name)                                      AS name,
                                                            COALESCE(ov.class_id, ov.relationship_id)                       AS technical_id,
                                                            CASE WHEN ov.class_id IS NOT NULL THEN 'entity' ELSE 'relation' END AS type,
                                                            COALESCE(c.description, rel.description)                        AS description,
                                                            1 - (ov.vector <=> e.vector)                                    AS score,
                                                            e.text_chunk,
                                                        ROW_NUMBER() OVER (
                                                               PARTITION BY e.id
                                                               ORDER BY ov.vector <=> e.vector ASC
                                                           ) AS rank
                                                        FROM dl_vector.embeddings e
                                                        JOIN dl_vector.ontology_vector ov ON TRUE
                                                        LEFT JOIN deeplynx.classes c   ON c.id = ov.class_id
                                                        LEFT JOIN deeplynx.relationships rel ON rel.id = ov.relationship_id
                                                        WHERE e.record_id = {recordId}
                                                        AND (c.project_id = {projectId} OR rel.project_id = {projectId})
                                                          AND ({termType}::text IS NULL OR
                                                               CASE WHEN ov.class_id IS NOT NULL THEN 'entity' ELSE 'relation' END = {termType}::text)
                                                    ) ranked
                                                    WHERE rank <= {limit}
                                                    ORDER BY text_chunk, rank;
                                                    """)
            .ToListAsync();
    }

    //TODO: Re-embed classes and relationships if Lattice makes and update to existing items
    // private async Task EnsureEmbeddingsReady();
    
    
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
        var ontologyEmbedded = await _context.OntologyVectors
            .AnyAsync(ov =>
                _context.Classes.Any(c => c.Id == ov.ClassId && c.ProjectId == projectId) ||
                _context.Relationships.Any(r => r.Id == ov.RelationshipId && r.ProjectId == projectId));
    
        if (recordEmbedded && ontologyEmbedded) return;
    
        if (!recordEmbedded)
        {
            var vlmConfig = await _insightBusiness.ResolveModelConfig(
                currentUserId, organizationId, projectId, null, "vlm");
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


    private static long? ResolveClassId(string? name, Dictionary<string, long> map)
    {
        return name != null && map.TryGetValue(name, out var id) ? id : null;
    }

    private static long? ResolveRecordId(string? originalId, Dictionary<string, long> map)
    {
        return originalId != null && map.TryGetValue(originalId, out var id) ? id : null;
    }

    private static long? ResolveRelationshipId(string? name, Dictionary<string, long> map)
    {
        return name != null && map.TryGetValue(name, out var id) ? id : null;
    }
}