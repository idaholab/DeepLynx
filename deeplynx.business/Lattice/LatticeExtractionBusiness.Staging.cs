using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

public partial class LatticeExtractionBusiness : ILatticeExtractionBusiness
{
  private async Task<Dictionary<string, long>> StageClasses(
        long extractionId,
        IEnumerable<string> allClassTypes,
        Dictionary<string, SimilarityResult?> classSimilarities,
        long organizationId,
        long projectId)
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

        var classTypeToId = uniqueClassTypes
            .Zip(extractionClasses, (type, cls) => (type, cls.Id))
            .ToDictionary(x => x.type, x => x.Id, StringComparer.OrdinalIgnoreCase);

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
        long dataSourceId)
    {

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
        var stagedRecordClasses = new List<string>();

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
                EnsembleScore = CalculateEnsembleScore(
                    record.Confidence, embeddingPlausibility, statFreq, structuralConsistency)
            });
            stagedRecordNames.Add(recordName);
            stagedRecordClasses.Add(classType);
        }
        
        _latticeContext.ExtractionRecords.AddRange(extractionRecords);
        await _latticeContext.SaveChangesAsync();

        var nameToId = stagedRecordNames
            .Zip(stagedRecordClasses, (name, cls) => (name, cls))
            .Zip(extractionRecords, (nc, rec) => (nc.name, nc.cls, rec.Id))
            .ToDictionary(
                x => MakeRecordKey(x.cls, x.name), 
                x => x.Id);

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
        var keyToId = patternKeys
            .Zip(extractionRelationships, (key, rel) => (key, rel.Id))
            .ToDictionary(x => x.key, x => x.Id, StringComparer.OrdinalIgnoreCase);
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
        long dataSourceId)
    {
        if (!edges.Any()) return 0;

        var maxFrequency = edges.Max(e => e.Frequency);

        var relValidationById = await _latticeContext.ExtractionRelationships
            .Where(r => relationshipKeyToId.Values.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.ValidationStatus);

        var extractionEdges = new List<ExtractionEdge>();
        foreach (var edge in edges)
        {
            // Skip edges whose subject or object wasn't staged as a record — this can happen when
            // the LLM references an entity in a relationship that it didn't include in the classes array
            if (!instanceNameToRecordId.TryGetValue(MakeRecordKey(edge.SubjectType, edge.Subject), out var originRecordId)) continue;
            if (!instanceNameToRecordId.TryGetValue(MakeRecordKey(edge.ObjectType, edge.Object), out var destRecordId)) continue;

            relSimilarities.TryGetValue(edge.RelationshipType, out var relMatch);
            var patternKey = RelationshipPatternKey(edge.SubjectType, edge.RelationshipType, edge.ObjectType);
            relationshipKeyToId.TryGetValue(patternKey, out var relId);
            relValidationById.TryGetValue(relId, out var validationStatus);
            
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
                EnsembleScore = CalculateEnsembleScore(
                    edge.Confidence, embeddingPlausibility, statFreq, structuralConsistency)
            });
        }

        _latticeContext.ExtractionEdges.AddRange(extractionEdges);
        await _latticeContext.SaveChangesAsync();

        return extractionEdges.Count;
    }
    
    private static string MakeRecordKey(string classType, string name) =>
        $"{classType.Trim().ToLowerInvariant()}::{name.Trim().ToLowerInvariant()}";
    
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
}