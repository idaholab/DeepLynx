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
    private readonly InsightServiceClient _insightServiceClient;

    public LatticeExtractionBusiness(DeeplynxContext context, StagingContext stagingContext,
        IInsightBusiness insightBusiness, InsightServiceClient insightServiceClient)
    {
        _context = context;
        _stagingContext = stagingContext;
        _insightBusiness = insightBusiness;
        _insightServiceClient = insightServiceClient;
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

        // Minimum ontology items necessary for extraction
        await EnsureOntologyReady(projectId);

        // Ontology embeddings must exist before triggering. If they're missing, queue
        // them automatically and fail fast so the user can retry once they're ready.
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
            var filledPrompt = await ConstructPrompt(recordId, projectId, mode);

            //TODO: Fix hard coded model name. 
            var response =
                await _insightServiceClient.LatticeExtraction(filledPrompt, "Mistral-Small-3.2-24B-Instruct-2506");

            if (response.IsSuccessStatusCode)
            {
                extraction.Status = ExtractionStatus.Running;
            }
            else
            {
                extraction.Status = ExtractionStatus.Failed;
                throw new HttpRequestException(
                    $"Lattice extraction request failed");
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
        long limit = 20)
    {
        return await _context.Database
            .SqlQuery<OntologySimilarityResultDto>($$"""
                                                     SELECT name, class_or_relationship_id, type, description, score, text_chunk,
                                                            origin_class, destination_class
                                                     FROM (
                                                         SELECT 
                                                             COALESCE(c.name, rel.name)                                      AS name,
                                                             COALESCE(ov.class_id, ov.relationship_id)                       AS class_or_relationship_id,
                                                             CASE WHEN ov.class_id IS NOT NULL THEN 'class' ELSE 'relationship' END AS type,
                                                             COALESCE(c.description, rel.description)                        AS description,
                                                             1 - (ov.vector <=> e.vector)                                    AS score,
                                                             e.text_chunk,
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

    //TODO: Re-embed classes and relationships if Lattice makes and update to existing items
    
    //TODO: Validation Logic 
    
    //TODO: Staging promotion logic
    
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
    
    
    // Extraction needs at least 2 classes and 1 relationship (user made) to proceed
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
        {
            throw new InvalidOperationException(
                $"Project {projectId} does not have sufficient ontology defined. " +
                "At least 2 non-default classes and 1 relationship are required.");
        }
    }

    private async Task<string> ConstructPrompt(long recordId, long projectId, string mode)
    {
            //Cosine similarity search to retrieve similar ontology data with respect to document text chunk
            //Specific formatting for LLM prompt construction 
            var results = await SearchOntologySimilarity(
                recordId, projectId);

            var classes = results
                .Where(r => r.Type == "class")
                .DistinctBy(r => r.ClassRelationshipId)
                .Select(r => $"{r.Name}: {r.Description}");

            var relationships = results
                .Where(r => r.Type == "relationship" && r.RelationshipPattern != null)
                .DistinctBy(r => r.ClassRelationshipId)
                .Select(r => $"({r.RelationshipPattern!.OriginClassName}) -{r.RelationshipPattern.RelationshipName}-> ({r.RelationshipPattern.DestinationClassName})");

            var textChunks = results
                .Where(r => !string.IsNullOrEmpty(r.TextChunk))
                .DistinctBy(r => r.TextChunk)
                .Select(r => r.TextChunk!);

            var entityList   = string.Join("\n", classes);
            var relationList = string.Join("\n", relationships);
            var text         = string.Join("\n\n", textChunks);
            
            //entity list is [(class name, description)]
            //relation_list is [(origin class name, relationship name, destination class name)] 
            //TODO: context_block is graph context, 2 hops 
            //{text}{truncation} is ["example text", "text"]
            //TODO: {truncation} is for document text chunk truncation, necessary only if it exceeds a certain character limit. Plus the "...truncated" message to the LLM
            var prompt = "";
            var strictPrompt = """
                You are a precise information extraction system for formal ontology-based knowledge graphs.
                  
                Your task is to extract entities and relationships that MATCH the provided ontology schema.
                  
                This ontology follows Common Core Ontologies (CCO) standards - a domain-neutral framework used across military, government, commercial, and academic sectors. Extract information relevant to ANY domain (defense, infrastructure, operations, organizations, facilities, equipment, personnel, etc.).
                  
                ONTOLOGY SCHEMA - Entity Types (with definitions):
                  
                {entity_list}
                  
                ONTOLOGY SCHEMA - Valid Relation Patterns (domain, predicate, range):
                  
                {relation_list}
                  
                EXTRACTION RULES (STRICT MODE - Ontology Compliance):
                  
                1. Extract ONLY entities matching the provided entity types
                2. Use the type definitions to correctly classify entities
                3. Extract ONLY relations matching the valid relation patterns
                4. Each relation MUST include subject_type and object_type
                5. Every entity in a relation MUST also appear in the entities array
                6. Assign confidence scores (0.0 to 1.0) based on extraction certainty
                7. DO NOT create new entity types - use only the types listed above
                8. Apply to ANY domain: military operations, facilities, organizations, equipment, personnel, missions, etc.
                 
                ATTRIBUTE EXTRACTION RULES:
                1. Attributes MUST be explicitly stated in the document text.
                2. Do NOT infer, speculate, or guess missing attributes.
                3. Extract up to 5 high-value attributes per entity.
                4. Prefer high-signal keys when available (e.g., manufacturer, model, role, location, dimensions, capacity, date, unit, commander).
                5. Omit uncertain attributes entirely.
                6. Keep values short and literal (no long paraphrases).
                  
                DOCUMENT TEXT:
                  
                {text}{truncation}
                  
                Return ONLY valid JSON (no markdown, no explanations):
                  
                {{
                    "entities": [
                        {{"entity": "RAF Mildenhall", "entity_type": "Air Force Base", "confidence": 0.95, "attributes": {{"location": "United Kingdom", "unit": "100th Air Refueling Wing"}}}},
                        {{"entity": "100th Air Refueling Wing", "entity_type": "Military Organization", "confidence": 0.92, "attributes": {{"role": "air refueling", "commander": "Col. Johnny Galbert"}}}}
                    ],
                    "relations": [
                        {{"subject": "100th Air Refueling Wing", "subject_type": "Military Organization",
                        "relation_type": "located at", "object": "RAF Mildenhall", "object_type": "Air Force Base", "confidence": 0.90}}
                    ]
                }}
                  
                CRITICAL: Use EXACT entity type names from the ontology schema. Be thorough - extract all relevant entities and relationships from the document.
                """;
            
            var discoveryPrompt = """
                You are a knowledge extraction system for formal ontology-based knowledge graphs.
                
                Your task is to extract entities and relationships, preferring the provided ontology schema but discovering new types when necessary.
                
                This ontology uses Common Core Ontologies (CCO) - a domain-neutral standard framework covering entities across ALL sectors: military operations, defense systems, government organizations, commercial facilities, infrastructure, personnel, missions, equipment, and more.
                
                PREFERRED ENTITY TYPES (use when applicable):
                
                {entity_list}
                
                PREFERRED RELATION PATTERNS (use when applicable):
                
                {relation_list}
                
                EXTRACTION RULES (DISCOVERY MODE - Balanced Precision/Discovery):
                
                1. PREFER entities from the ontology types above when they fit well
                2. If an entity doesn't match any provided type well:
                   - Still extract it if contextually important
                   - Use the most similar ontology type, OR
                   - Create a specific descriptive type (e.g., "TacticalOperationsCenter", "MunitionsStorageFacility")
                3. For discovered types, use confidence 0.60-0.80 (lower than ontology matches)
                4. PREFER relations from the provided patterns
                5. If a relationship doesn't fit any pattern:
                   - Still extract if it represents important domain knowledge
                   - Use descriptive relation names (e.g., "supports", "coordinates_with", "supervises")
                6. Each relation MUST include subject_type and object_type
                7. Every entity in a relation MUST also appear in the entities array
                
                ATTRIBUTE EXTRACTION RULES:
                1. Attributes MUST be explicitly stated in the document text.
                2. Do NOT infer, speculate, or guess missing attributes.
                3. Extract up to 5 high-value attributes per entity.
                4. Prefer high-signal keys when available (e.g., manufacturer, model, role, location, dimensions, capacity, date, unit, commander).
                5. Omit uncertain attributes entirely.
                6. Keep values short and literal (no long paraphrases).
                
                DISCOVERY GUIDELINES:
                
                - Ontology matches: confidence 0.85-0.95
                - Similar ontology types: confidence 0.75-0.85
                - New discovered types: confidence 0.60-0.75
                - Type names: Clear, specific, CamelCase (e.g., "AirTrafficControlTower", "SecureCommandFacility")
                - Apply to ANY domain: extract military units, facilities, operations, personnel roles, equipment, missions, etc.
                
                DOCUMENT TEXT:
                
                {text}
                
                Return ONLY valid JSON (no markdown, no explanations):
                
                {{
                    "entities": [
                        {{"entity": "RAF Mildenhall", "entity_type": "Air Force Base", "confidence": 0.95, "attributes": {{"location": "United Kingdom", "unit": "100th Air Refueling Wing"}}}},
                        {{"entity": "Tactical Operations Center", "entity_type": "CommandControlFacility", "confidence": 0.72, "attributes": {{"role": "command and control", "location": "operations center"}}}}
                    ],
                    "relations": [
                        {{"subject": "100th Air Refueling Wing", "subject_type": "Military Organization",
                        "relation_type": "stationed at", "object": "RAF Mildenhall", "object_type": "Air Force Base", "confidence": 0.90}},
                        {{"subject": "Tactical Operations Center", "subject_type": "CommandControlFacility",
                        "relation_type": "coordinates", "object": "100th Air Refueling Wing", "object_type": "Military Organization", "confidence": 0.75}}
                    ]
                }}
                
                IMPORTANT: Balance ontology compliance with discovery. Extract comprehensively across all domains while preferring standard types when applicable.
                """;
            
            if (mode == ExtractionMode.Strict)
            {
                prompt = strictPrompt; 
            }
            else
            {
                prompt = discoveryPrompt;
            }

            var filledPrompt = prompt
                .Replace("{entity_list}", entityList)
                .Replace("{relation_list}", relationList)
                .Replace("{text}", text);
            
            return filledPrompt;
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