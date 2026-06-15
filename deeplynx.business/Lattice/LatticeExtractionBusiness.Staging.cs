using System.Collections.Concurrent;
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
                ? 1.0
                : 0.0;

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
                EnsembleScore = CalculateEnsembleScore(
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
                    string.Equals(p.RelationshipName, relMatch.OntologyEntityName,
                        StringComparison.OrdinalIgnoreCase) &&
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

        var relationshipIds = relationshipKeyToId.Values.Distinct().ToList();
        var relValidationById = await _latticeContext.ExtractionRelationships
            .Where(r => relationshipIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.ValidationStatus);

        var extractionEdges = new List<ExtractionEdge>();
        foreach (var edge in edges)
        {
            // Skip edges whose subject or object wasn't staged as a record — this can happen when
            // the LLM references an entity in a relationship that it didn't include in the classes array
            if (!instanceNameToRecordId.TryGetValue(edge.Subject, out var originRecordId)) continue;
            if (!instanceNameToRecordId.TryGetValue(edge.Object, out var destRecordId)) continue;

            relSimilarities.TryGetValue(edge.RelationshipType, out var relMatch);
            var patternKey = $"{edge.SubjectType}|{edge.RelationshipType}|{edge.ObjectType}";
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
}