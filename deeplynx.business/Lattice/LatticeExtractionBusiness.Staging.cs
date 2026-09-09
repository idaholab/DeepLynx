using System.Text.Json;
using System.Text.Json.Nodes;
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
            _logger.LogWarning(
                "Skipping {MalformedCount} malformed Lattice records for extraction {ExtractionId}",
                malformedCount,
                extractionId);

        var maxFrequency = validRecords.Any()
            ? validRecords.Max(r => r.Frequency)
            : 0;

        // Batch KG lookup — inherit canonical name if the instance already exists in the graph
        var recordNames = validRecords.Select(r => r.Name.Trim()).ToList();
        var kgMatches = await _context.Records
            .Where(r => r.ProjectId == projectId && recordNames.Contains(r.Name))
            .Select(r => new { r.Id, r.Name, r.Properties })
            .ToListAsync();
        var nameToKg = kgMatches
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Id).First(),
                StringComparer.OrdinalIgnoreCase);

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

            stagedRecordNames.Add(recordName);
            stagedRecordClasses.Add(classType);

            classSimilarities.TryGetValue(classType, out var classMatch);
            var normalizedClassName = classMatch?.OntologyEntityName;

            var matchingKgRecords = kgMatches
                .Where(r => string.Equals(r.Name, recordName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            bool matchedKgRecord = false;

            foreach (var kgRecord in matchingKgRecords)
            {
                var recordTags = await _context.Database
                    .SqlQueryRaw<string>(
                        @"SELECT t.name
                  FROM deeplynx.record_tags rt
                  JOIN deeplynx.tags t ON rt.tag_id = t.id
                  WHERE rt.record_id = {0}", kgRecord.Id)
                    .ToListAsync();

                var kgRecordAttributesNode = JsonNode.Parse(kgRecord.Properties)?.AsObject();
                if (recordTags.Count != 0)
                {
                    kgRecordAttributesNode?["tags"] = new JsonArray(recordTags.Select(t => JsonValue.Create(t)).ToArray());
                }

                kgRecordAttributesNode?.Remove("originId");
                kgRecordAttributesNode?.Remove("source_page");
                record.Attributes.Remove("originId");
                record.Attributes.Remove("source_page");

                var attributesMatch = JsonNodesDeepEquals(record.Attributes, kgRecordAttributesNode, "/");

                kgRecordAttributesNode?.Remove("tags");

                if (attributesMatch)
                {
                    var structuralConsistency = normalizedClassName != null && ontologyPatterns.Any(p =>
                        string.Equals(p.OriginClassName, normalizedClassName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.DestinationClassName, normalizedClassName, StringComparison.OrdinalIgnoreCase))
                        ? 1.0
                        : 0.0;

                    var embeddingPlausibility = classMatch?.Score ?? 0.0;
                    var statFreq = maxFrequency > 0 ? (double)record.Frequency / maxFrequency : 0.0;

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
                        SourceRecordId = record.RecordId,
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

                    matchedKgRecord = true;
                    break;
                }
            }

            if (!matchedKgRecord)
            {
                var structuralConsistency = normalizedClassName != null && ontologyPatterns.Any(p =>
                    string.Equals(p.OriginClassName, normalizedClassName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.DestinationClassName, normalizedClassName, StringComparison.OrdinalIgnoreCase))
                    ? 1.0
                    : 0.0;
                var embeddingPlausibility = classMatch?.Score ?? 0.0;
                var statFreq = maxFrequency > 0 ? (double)record.Frequency / maxFrequency : 0.0;

                extractionRecords.Add(new ExtractionRecord
                {
                    ExtractionId = extractionId,
                    ExtractionClassId = extractionClassId,
                    Name = recordName,
                    Attributes = record.Attributes?.ToJsonString(),
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    DataSourceId = dataSourceId,
                    DeeplynxRecordId = null,
                    SourceRecordId = record.RecordId,
                    ValidationStatus = classMatch != null
                        ? ExtractionValidationStatus.Valid
                        : ExtractionValidationStatus.InvalidSchema,
                    Frequency = record.Frequency,
                    LlmScore = record.Confidence,
                    EmbeddingPlausibility = embeddingPlausibility,
                    StatisticalFrequency = statFreq,
                    StructuralConsistency = structuralConsistency,
                    EnsembleScore = CalculateEnsembleScore(record.Confidence, embeddingPlausibility, statFreq, structuralConsistency)
                });
            }
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
    private static bool JsonNodesDeepEquals(JsonNode? node1, JsonNode? node2, string path = "")
    {
        if (node1 == null && node2 == null)
            return true;
        if (node1 == null || node2 == null)
        {
            return false;
        }

        if (node1.GetType() != node2.GetType())
        {
            if (node1 is JsonValue val1 && node2 is JsonValue val2)
            {
                var v1 = val1.GetValue<object>();
                var v2 = val2.GetValue<object>();

                string? ExtractString(object? val)
                {
                    if (val == null)
                        return null;

                    if (val is JsonElement je)
                    {
                        if (je.ValueKind == JsonValueKind.String)
                            return je.GetString();
                        else
                            return je.ToString();
                    }

                    return val.ToString();
                }
                bool EqualsJsonValues(object? val1, object? val2)
                {
                    string? s1 = ExtractString(val1);
                    string? s2 = ExtractString(val2);

                    if (s1 != null && s2 != null)
                        return string.Equals(s1.Trim(), s2.Trim(), StringComparison.OrdinalIgnoreCase);

                    return Equals(val1, val2);
                }

                return EqualsJsonValues(v1, v2);
            }

            return false;
        }

        switch (node1)
        {
            case JsonObject obj1 when node2 is JsonObject obj2:
                var keys1 = obj1.Select(kv => kv.Key).ToList();
                var keys2 = obj2.Select(kv => kv.Key).ToList();

                if (keys1.Count != keys2.Count)
                {
                    return false;
                }

                foreach (var key1 in keys1)
                {
                    var matchKey = keys2.FirstOrDefault(k => string.Equals(k, key1, StringComparison.OrdinalIgnoreCase));
                    if (matchKey == null)
                    {
                        return false;
                    }

                    if (!JsonNodesDeepEquals(obj1[key1], obj2[matchKey], $"{path}/{key1}"))
                    {
                        return false;
                    }
                }
                return true;

            case JsonArray arr1 when node2 is JsonArray arr2:
                if (arr1.Count != arr2.Count)
                {
                    return false;
                }

                var matchedIndices = new bool[arr2.Count];

                foreach (var item1 in arr1)
                {
                    bool foundMatch = false;
                    for (int i = 0; i < arr2.Count; i++)
                    {
                        if (matchedIndices[i])
                            continue;

                        if (JsonNodesDeepEquals(item1, arr2[i], $"{path}[{i}]"))
                        {
                            matchedIndices[i] = true;
                            foundMatch = true;
                            break;
                        }
                    }
                    if (!foundMatch)
                    {
                        return false;
                    }
                }
                return true;

            case JsonValue val1 when node2 is JsonValue val2:
                var v1 = val1.GetValue<object>();
                var v2 = val2.GetValue<object>();

                if (v1 == null && v2 == null)
                    return true;
                if (v1 == null || v2 == null)
                {
                    return false;
                }

                var str1 = v1?.ToString()?.Trim();
                var str2 = v2?.ToString()?.Trim();

                if (!string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;

            default:
                var deepEquals = JsonNode.DeepEquals(node1, node2);
                return deepEquals;
        }
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
            _logger.LogWarning(
                "Skipping {MalformedCount} malformed Lattice relationships for extraction {ExtractionId}",
                malformedCount,
                extractionId);

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

            var normalizedSubject = subjectMatch?.OntologyEntityName ?? subjectType;
            var normalizedObject = objectMatch?.OntologyEntityName ?? objectType;

            var patternExists = relMatch != null &&
                                ontologyPatterns.Any(p =>
                                    string.Equals(p.OriginClassName, normalizedSubject,
                                        StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(p.RelationshipName, relMatch.OntologyEntityName,
                                        StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(p.DestinationClassName, normalizedObject,
                                        StringComparison.OrdinalIgnoreCase));

            string validationStatus;
            if (patternExists)
                validationStatus = ExtractionValidationStatus.Valid;
            else if (mode == ExtractionMode.Discovery &&
                     relMatch != null &&
                     subjectMatch != null &&
                     objectMatch != null)
                validationStatus = ExtractionValidationStatus.NovelDiscovery;
            else
                validationStatus = ExtractionValidationStatus.InvalidSchema;

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
                OntologyRelationshipId = patternExists
                    ? relMatch?.OntologyEntityId
                    : null,
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
            if (!instanceNameToRecordId.TryGetValue(MakeRecordKey(edge.SubjectType, edge.Subject),
                    out var originRecordId)) continue;
            if (!instanceNameToRecordId.TryGetValue(MakeRecordKey(edge.ObjectType, edge.Object), out var destRecordId))
                continue;

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
                SourceRecordId = edge.RecordId,
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

    private static string MakeRecordKey(string classType, string name)
    {
        return $"{classType.Trim().ToLowerInvariant()}::{name.Trim().ToLowerInvariant()}";
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
        string objectType)
    {
        return $"{subjectType.Trim()}|{relationshipType.Trim()}|{objectType.Trim()}";
    }
}