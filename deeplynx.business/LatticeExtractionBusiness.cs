using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

public class LatticeExtractionBusiness : ILatticeExtractionBusiness
{
    private readonly DeeplynxContext _context;
    private readonly LatticeContext _latticeContext;
    private readonly IInsightBusiness _insightBusiness;
    private readonly InsightServiceClient _insightServiceClient;
    private readonly IExtractionValidation _validationBusiness;
    private readonly ILogger<LatticeExtractionBusiness> _logger;

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
            Status = ExtractionStatus.Pending,
            Mode = mode
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

            //TODO: Fix hard coded model name.
            var response =
                await _insightServiceClient.LatticeExtraction(filledPrompt, "Mistral-Small-3.2-24B-Instruct-2506",
                    queryInfo);

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
    

    public async Task<ExtractionResponseDto> ProcessInsightExtractionCallback(
        long currentUserId,
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

        var (dedupedRecords, dedupedEdges) = _validationBusiness.Deduplicate(dto);

        // Collect all class types from records AND edge subject/object types so every type
        // referenced in a relationship is normalized and gets an ExtractionClass row.
        var allClassTypes = dedupedRecords.Select(r => r.ClassType)
            .Concat(dedupedEdges.Select(e => e.SubjectType))
            .Concat(dedupedEdges.Select(e => e.ObjectType));

        var classSimilarities = await _validationBusiness.NormalizeClassTypes(allClassTypes, projectId);
        var relSimilarities = await _validationBusiness.NormalizeRelationshipTypes(dedupedEdges, projectId);
        var ontologyPatterns = await _validationBusiness.GetOntologyPatterns(projectId);

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
                CreatedBy = currentUserId,
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

    private async Task<Dictionary<string, long>> StageClasses(
        long extractionId,
        IEnumerable<string> allClassTypes,
        Dictionary<string, SimilarityResult?> classSimilarities,
        long organizationId,
        long projectId,
        string mode)
    {
        var uniqueClassTypes = allClassTypes
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

        var maxFrequency = records.Max(r => r.Frequency);

        // Batch KG lookup — inherit canonical name if the instance already exists in the graph
        var recordNames = records.Select(r => r.Name).ToList();
        var kgMatches = await _context.Records
            .Where(r => r.ProjectId == projectId && recordNames.Contains(r.Name))
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();
        var nameToKg = kgMatches.ToDictionary(r => r.Name, r => r, StringComparer.OrdinalIgnoreCase);

        var extractionRecords = records.Select(record =>
        {
            classSimilarities.TryGetValue(record.ClassType, out var classMatch);
            nameToKg.TryGetValue(record.Name, out var kgRecord);

            var embeddingPlausibility = classMatch?.Score ?? 0.0;
            var statFreq = maxFrequency > 0 ? (double)record.Frequency / maxFrequency : 0.0;

            var normalizedClassName = classMatch?.OntologyEntityName;
            var structuralConsistency = normalizedClassName != null && ontologyPatterns.Any(p =>
                string.Equals(p.OriginClassName, normalizedClassName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.DestinationClassName, normalizedClassName, StringComparison.OrdinalIgnoreCase))
                ? 1.0 : 0.0;

            return new ExtractionRecord
            {
                ExtractionId = extractionId,
                ExtractionClassId = classTypeToId[record.ClassType],
                Name = kgRecord?.Name ?? record.Name,
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
            };
        }).ToList();

        _latticeContext.ExtractionRecords.AddRange(extractionRecords);
        await _latticeContext.SaveChangesAsync();

        var nameToId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < records.Count; i++)
            nameToId[records[i].Name] = extractionRecords[i].Id;

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
        // Unique (subjectType, relType, objectType) patterns — one ExtractionRelationship per pattern
        var uniquePatterns = edges
            .GroupBy(e => (
                e.SubjectType.Trim().ToLowerInvariant(),
                e.RelationshipType.Trim().ToLowerInvariant(),
                e.ObjectType.Trim().ToLowerInvariant()))
            .Select(g => g.First())
            .ToList();

        var extractionRelationships = new List<ExtractionRelationship>();
        var patternKeys = new List<string>();

        foreach (var edge in uniquePatterns)
        {
            relSimilarities.TryGetValue(edge.RelationshipType, out var relMatch);
            classSimilarities.TryGetValue(edge.SubjectType, out var subjectMatch);
            classSimilarities.TryGetValue(edge.ObjectType, out var objectMatch);

            string validationStatus;
            if (relMatch == null)
            {
                validationStatus = ExtractionValidationStatus.InvalidSchema;
            }
            else
            {
                var normalizedSubject = subjectMatch?.OntologyEntityName ?? edge.SubjectType;
                var normalizedObject = objectMatch?.OntologyEntityName ?? edge.ObjectType;

                var patternExists = ontologyPatterns.Any(p =>
                    string.Equals(p.OriginClassName, normalizedSubject, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.RelationshipName, relMatch.OntologyEntityName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.DestinationClassName, normalizedObject, StringComparison.OrdinalIgnoreCase));

                validationStatus = patternExists
                    ? ExtractionValidationStatus.Valid
                    : mode == ExtractionMode.Discovery && subjectMatch != null && objectMatch != null
                        ? ExtractionValidationStatus.NovelDiscovery
                        : ExtractionValidationStatus.InvalidSchema;
            }

            classTypeToId.TryGetValue(edge.SubjectType, out var originClassId);
            classTypeToId.TryGetValue(edge.ObjectType, out var destinationClassId);

            patternKeys.Add($"{edge.SubjectType}|{edge.RelationshipType}|{edge.ObjectType}");

            extractionRelationships.Add(new ExtractionRelationship
            {
                ExtractionId = extractionId,
                OriginClassId = originClassId,
                DestinationClassId = destinationClassId,
                Name = relMatch?.OntologyEntityName ?? edge.RelationshipType,
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

        var maxFrequency = edges.Max(e => e.Frequency);

        // Query back relationship validation statuses to inherit and derive structural consistency
        var relationshipIds = relationshipKeyToId.Values.Distinct().ToList();
        var relValidationById = await _latticeContext.ExtractionRelationships
            .Where(r => relationshipIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.ValidationStatus);

        var extractionEdges = edges.Select(edge =>
        {
            relSimilarities.TryGetValue(edge.RelationshipType, out var relMatch);
            var patternKey = $"{edge.SubjectType}|{edge.RelationshipType}|{edge.ObjectType}";
            relationshipKeyToId.TryGetValue(patternKey, out var relId);
            instanceNameToRecordId.TryGetValue(edge.Subject, out var originRecordId);
            instanceNameToRecordId.TryGetValue(edge.Object, out var destRecordId);
            relValidationById.TryGetValue(relId, out var validationStatus);

            var embeddingPlausibility = relMatch?.Score ?? 0.0;
            var statFreq = maxFrequency > 0 ? (double)edge.Frequency / maxFrequency : 0.0;
            // Pattern exists in ontology only when the relationship is valid (not novel_discovery)
            var structuralConsistency = validationStatus == ExtractionValidationStatus.Valid ? 1.0 : 0.0;

            return new ExtractionEdge
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
            };
        }).ToList();

        _latticeContext.ExtractionEdges.AddRange(extractionEdges);
        await _latticeContext.SaveChangesAsync();

        return extractionEdges.Count;
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
    /// <param name="termType">Optional filter: "class" or "relationship". Null returns both.</param>
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
            AiModelConfigResponseDto vlmConfig;
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
    
    
    /// <summary>Throws if the project has fewer than 2 non-default classes or no relationships — the minimum needed for extraction.</summary>
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

    /// <summary>Builds the LLM extraction prompt by running a similarity search and injecting the top-ranked ontology context and document text chunks.</summary>
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
            //TODO: context_block is graph context, 2 hops from record node
            //{text}{truncation} is ["example text", "text"]
            //TODO: {truncation} is for document text chunk truncation, necessary only if it exceeds a certain character limit. Plus the "...truncated" message to the LLM
            var prompt = "";
            var strictPrompt = """
                You are a precise information extraction system for formal ontology-based knowledge graphs.
                  
                Your task is to extract classes and relationships that MATCH the provided ontology schema.
                  
                This ontology follows Common Core Ontologies (CCO) standards - a domain-neutral framework used across military, government, commercial, and academic sectors. Extract information relevant to ANY domain (defense, infrastructure, operations, organizations, facilities, equipment, personnel, etc.).
                  
                ONTOLOGY SCHEMA - Class Types (with definitions):
                  
                {class_list}
                  
                ONTOLOGY SCHEMA - Valid Relationship Patterns (domain, predicate, range):
                  
                {relationship_list}
                  
                EXTRACTION RULES (STRICT MODE - Ontology Compliance):
                  
                1. Extract ONLY classes matching the provided class types
                2. Use the type definitions to correctly classify classes
                3. Extract ONLY relationships matching the valid relationship patterns
                4. Each relationship MUST include subject_type and object_type
                5. Every class in a relationship MUST also appear in the class array
                6. Assign confidence scores (0.0 to 1.0) based on extraction certainty
                7. DO NOT create new class types - use only the types listed above
                8. Apply to ANY domain: military operations, facilities, organizations, equipment, personnel, missions, etc.
                 
                ATTRIBUTE EXTRACTION RULES:
                1. Attributes MUST be explicitly stated in the document text.
                2. Do NOT infer, speculate, or guess missing attributes.
                3. Extract up to 5 high-value attributes per entity.
                4. Prefer high-signal keys when available (e.g., manufacturer, model, role, location, dimensions, capacity, date, unit, commander).
                5. Omit uncertain attributes entirely.
                6. Keep values short and literal (no long paraphrases).
                  
                DOCUMENT TEXT:
                  
                {text}
                  
                Return ONLY valid JSON (no markdown, no explanations):
                  
                {{
                    "classes": [
                        {{"class": "RAF Mildenhall", "class_type": "Air Force Base", "confidence": 0.95, "attributes": {{"location": "United Kingdom", "unit": "100th Air Refueling Wing"}}}},
                        {{"class": "100th Air Refueling Wing", "class_type": "Military Organization", "confidence": 0.92, "attributes": {{"role": "air refueling", "commander": "Col. Johnny Galbert"}}}}
                    ],
                    "relationships": [
                        {{"subject": "100th Air Refueling Wing", "subject_type": "Military Organization",
                        "relationship_type": "located at", "object": "RAF Mildenhall", "object_type": "Air Force Base", "confidence": 0.90}}
                    ]
                }}
                  
                CRITICAL: Use EXACT class type names from the ontology schema. Be thorough - extract all relevant classes and relationships from the document.
                """;
            
            var discoveryPrompt = """
                You are a knowledge extraction system for formal ontology-based knowledge graphs.
                
                Your task is to extract classes and relationships, preferring the provided ontology schema but discovering new types when necessary.
                
                This ontology uses Common Core Ontologies (CCO) - a domain-neutral standard framework covering classes across ALL sectors: military operations, defense systems, government organizations, commercial facilities, infrastructure, personnel, missions, equipment, and more.
                
                PREFERRED CLASS TYPES (use when applicable):
                
                {class_list}
                
                PREFERRED RELATIONSHIP PATTERNS (use when applicable):
                
                {relationship_list}
                
                EXTRACTION RULES (DISCOVERY MODE - Balanced Precision/Discovery):
                
                1. PREFER classes from the ontology types above when they fit well
                2. If an entity doesn't match any provided type well:
                   - Still extract it if contextually important
                   - Use the most similar ontology type, OR
                   - Create a specific descriptive type (e.g., "TacticalOperationsCenter", "MunitionsStorageFacility")
                3. For discovered types, use confidence 0.60-0.80 (lower than ontology matches)
                4. PREFER relationships from the provided patterns
                5. If a relationship doesn't fit any pattern:
                   - Still extract if it represents important domain knowledge
                   - Use descriptive relationship names (e.g., "supports", "coordinates_with", "supervises")
                6. Each relationship MUST include subject_type and object_type
                7. Every class in a relationship MUST also appear in the classes array
                
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
                    "classes": [
                        {{"class": "RAF Mildenhall", "class_type": "Air Force Base", "confidence": 0.95, "attributes": {{"location": "United Kingdom", "unit": "100th Air Refueling Wing"}}}},
                        {{"class": "Tactical Operations Center", "class_type": "CommandControlFacility", "confidence": 0.72, "attributes": {{"role": "command and control", "location": "operations center"}}}}
                    ],
                    "relationships": [
                        {{"subject": "100th Air Refueling Wing", "subject_type": "Military Organization",
                        "relationship_type": "stationed at", "object": "RAF Mildenhall", "object_type": "Air Force Base", "confidence": 0.90}},
                        {{"subject": "Tactical Operations Center", "subject_type": "CommandControlFacility",
                        "relationship_type": "coordinates", "object": "100th Air Refueling Wing", "object_type": "Military Organization", "confidence": 0.75}}
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
                .Replace("{class_list}", entityList)
                .Replace("{relationship_list}", relationList)
                .Replace("{text}", text);
            
            return filledPrompt;
    }
    
    //TODO: Validation
    
    // type normalization 
    // similarity search against ontology 
    // anything above 0.8 will adopt what's defined in the ontology and replace what was generated by the LLM 
    // anything without a match will depend on STRICT or DISCOVERY 
    
    // Ensemble score 
    // llm score 40% - 0.9 if no score from LLM 
    // embedding plausibility 30% - from similarity search (similarity score)
    // statistical_frequency 20% - how often a record or edge appears in the response from the LLM, calculated 
    // structural_consistency 10% - relationship patterns against the existing ontology 
    // all weighed values are used to calculate the final confidence score 
    
    // Validation (STRICT vs DISCOVERY) 
    // Regardless of mode, classes and relationships must have an exact text match against original ontology or it's labeled as invalid 
    // STRICT 
    // if it exists in ontology keep it otherwise label invalid_schema 
    // DISCOVERY 
    // novel relationship patterns will be accepted but only if all individual elements exist in the original ontology
    
    // Knowledge Graph Validation 
    // Validated against knowledge graph, exact text match 
    // valid schema if class exists, instance would be new 
    
    
    // Save to extraction tables and let the user approve or deny extraction before promotion to the deeplynx schema
}