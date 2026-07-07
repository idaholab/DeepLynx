using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public partial class LatticeExtractionBusiness : ILatticeExtractionBusiness
{
    /// <summary>
    ///     Builds the four rejection ID sets from the request. Handles all three selection modes:
    ///     reject-all-remaining, by-status, and explicit IDs (which may be combined).
    /// </summary>
    private static (HashSet<long> ClassIds, HashSet<long> RecordIds, HashSet<long> RelIds, HashSet<long> EdgeIds)
        ResolveRejectionIds(
            RejectExtractionRequestDto request,
            List<ExtractionClass> stagingClasses,
            List<ExtractionRecord> stagingRecords,
            List<ExtractionRelationship> stagingRelationships,
            List<ExtractionEdge> stagingEdges)
    {
        if (request.RejectAllRemaining)
            return (
                stagingClasses.Where(ClassPending).Select(c => c.Id).ToHashSet(),
                stagingRecords.Where(RecordPending).Select(r => r.Id).ToHashSet(),
                stagingRelationships.Where(RelPending).Select(r => r.Id).ToHashSet(),
                stagingEdges.Where(EdgePending).Select(e => e.Id).ToHashSet()
            );

        var recordIds = (request.RecordIds ?? []).ToHashSet();
        var classIds = (request.ClassIds ?? []).ToHashSet();
        var relIds = (request.RelationshipIds ?? []).ToHashSet();
        var edgeIds = (request.EdgeIds ?? []).ToHashSet();
        var byStatus = (request.RejectByStatus ?? []).ToHashSet();

        if (byStatus.Count > 0)
        {
            foreach (var c in stagingClasses.Where(c => ClassPending(c) && byStatus.Contains(c.ValidationStatus!)))
                classIds.Add(c.Id);
            foreach (var r in stagingRecords.Where(r => RecordPending(r) && byStatus.Contains(r.ValidationStatus!)))
                recordIds.Add(r.Id);
            foreach (var r in stagingRelationships.Where(r => RelPending(r) && byStatus.Contains(r.ValidationStatus!)))
                relIds.Add(r.Id);
            foreach (var e in stagingEdges.Where(e => EdgePending(e) && byStatus.Contains(e.ValidationStatus!)))
                edgeIds.Add(e.Id);
        }

        if (classIds.Count == 0 && recordIds.Count == 0 && relIds.Count == 0 && edgeIds.Count == 0)
            throw new InvalidOperationException("No staged items were selected for rejection.");

        return (classIds, recordIds, relIds, edgeIds);
    }

    /// <summary>
    ///     Throws if rejecting the selected items would strand pending dependents that were not also selected.
    ///     Computes the full required closure and reports anything the caller omitted.
    /// </summary>
    private static void ValidateRejectionClosure(
        List<ExtractionRecord> stagingRecords,
        List<ExtractionRelationship> stagingRelationships,
        List<ExtractionEdge> stagingEdges,
        HashSet<long> rejectClassIds,
        HashSet<long> rejectRecordIds,
        HashSet<long> rejectRelIds,
        HashSet<long> rejectEdgeIds)
    {
        var reqRecords = new HashSet<long>(rejectRecordIds);
        var reqRels = new HashSet<long>(rejectRelIds);
        var reqEdges = new HashSet<long>(rejectEdgeIds);

        foreach (var r in stagingRecords.Where(RecordPending))
            if (rejectClassIds.Contains(r.ExtractionClassId))
                reqRecords.Add(r.Id);
        foreach (var r in stagingRelationships.Where(RelPending))
            if (rejectClassIds.Contains(r.OriginClassId) || rejectClassIds.Contains(r.DestinationClassId))
                reqRels.Add(r.Id);
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

    /// <summary>
    ///     Expands status-based bulk approval into concrete IDs. Only items that are neither
    ///     already promoted nor rejected are eligible.
    /// </summary>
    private static void ExpandBulkSelections(
        IEnumerable<string> approveByStatus,
        List<ExtractionClass> stagingClasses,
        List<ExtractionRecord> stagingRecords,
        List<ExtractionRelationship> stagingRelationships,
        List<ExtractionEdge> stagingEdges,
        HashSet<long> selectedClassIds,
        HashSet<long> selectedRecordIds,
        HashSet<long> selectedRelIds,
        HashSet<long> selectedEdgeIds)
    {
        var bulkStatuses = approveByStatus.ToHashSet();
        if (bulkStatuses.Count == 0) return;

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

    /// <summary>
    ///     Throws if any explicitly selected item was previously rejected.
    ///     A rejected item can never be promoted.
    /// </summary>
    private static void ValidateRejectedNotSelected(
        List<ExtractionClass> stagingClasses,
        List<ExtractionRecord> stagingRecords,
        List<ExtractionRelationship> stagingRelationships,
        List<ExtractionEdge> stagingEdges,
        HashSet<long> selectedClassIds,
        HashSet<long> selectedRecordIds,
        HashSet<long> selectedRelIds,
        HashSet<long> selectedEdgeIds)
    {
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
    }

    /// <summary>
    ///     Throws if any selected item's required ancestor (class/record/relationship) is neither
    ///     already satisfied in nexus nor included in the current selection.
    ///     An ancestor is "satisfied" if it matched an existing ontology entity, was promoted in a
    ///     prior round, or is being promoted in this same round.
    /// </summary>
    private static void ValidateDependencies(
        List<ExtractionClass> stagingClasses,
        List<ExtractionRecord> stagingRecords,
        List<ExtractionRelationship> stagingRelationships,
        List<ExtractionEdge> stagingEdges,
        HashSet<long> selectedClassIds,
        HashSet<long> selectedRecordIds,
        HashSet<long> selectedRelIds,
        HashSet<long> selectedEdgeIds)
    {
        var classById = stagingClasses.ToDictionary(c => c.Id);
        var recordById = stagingRecords.ToDictionary(r => r.Id);
        var relById = stagingRelationships.ToDictionary(r => r.Id);
        var edgeById = stagingEdges.ToDictionary(e => e.Id);

        bool ClassSatisfied(long id)
        {
            return classById.TryGetValue(id, out var c) && !c.Rejected &&
                   (c.OntologyClassId.HasValue || c.PromotedId.HasValue || selectedClassIds.Contains(id));
        }

        bool RecordSatisfied(long id)
        {
            return recordById.TryGetValue(id, out var r) && !r.Rejected &&
                   (r.PromotedId.HasValue || selectedRecordIds.Contains(id));
        }

        bool RelSatisfied(long id)
        {
            return relById.TryGetValue(id, out var r) && !r.Rejected &&
                   (r.OntologyRelationshipId.HasValue || r.PromotedId.HasValue || selectedRelIds.Contains(id));
        }

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
                errors.Add(
                    $"Edge (id {id}) requires its {string.Join(", ", missing)} to be approved or already promoted.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Cannot promote the selected items because some dependencies were not included:\n" +
                string.Join("\n", errors));
    }

    /// <summary>
    ///     Promotes novel_discovery and invalid_schema classes that have no existing ontology match into deeplynx.classes.
    ///     Valid classes already exist in the ontology and are not re-created.
    ///     Returns a map of ExtractionClass.Id → deeplynx Class id for use in downstream steps.
    /// </summary>
    private async Task<Dictionary<long, long?>> PromoteClasses(
        List<ExtractionClass> stagingClasses,
        HashSet<long> selectedClassIds,
        long organizationId, long projectId, long extractionId,
        long currentUserId, DateTime now)
    {
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

        var toProcess = stagingClasses.Where(c =>
            selectedClassIds.Contains(c.Id) &&
            (c.ValidationStatus == ExtractionValidationStatus.NovelDiscovery ||
             c.ValidationStatus == ExtractionValidationStatus.InvalidSchema) &&
            c.OntologyClassId == null &&
            c.PromotedId == null).ToList();

        // Assign IDs for classes that already exist in the project
        foreach (var sc in toProcess.Where(sc => existingClassByName.ContainsKey(sc.Name)))
            sc.PromotedId = existingClassByName[sc.Name];

        // Batch-create remaining new classes, deduplicated by name
        var newClasses = toProcess
            .Where(sc => !sc.PromotedId.HasValue)
            .GroupBy(sc => sc.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Class
            {
                Name = g.Key,
                OrganizationId = organizationId,
                ProjectId = projectId,
                IsArchived = false,
                LastUpdatedAt = now,
                LastUpdatedBy = currentUserId,
                ExtractionId = extractionId
            })
            .ToList();

        if (newClasses.Count > 0)
        {
            _context.Classes.AddRange(newClasses);
            await _context.SaveChangesAsync();

            var nameToNewClassId = newClasses.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var sc in toProcess.Where(sc => !sc.PromotedId.HasValue))
                sc.PromotedId = nameToNewClassId[sc.Name];
        }

        await _latticeContext.SaveChangesAsync();

        return stagingClasses.ToDictionary(c => c.Id, c => c.OntologyClassId ?? c.PromotedId);
    }

    /// <summary>
    ///     Promotes extraction records into deeplynx.records.
    ///     Records already matched to a KG entity (deeplynx_record_id set) are linked rather than re-created.
    ///     When SourceRecordId is available, it is injected into the promoted record's Properties JSONB
    ///     as "originId" for provenance tracking. This value is authoritative and overwrites any
    ///     LLM-produced "originId" key.
    ///     Returns a map of ExtractionRecord.Id → deeplynx Record id, and the count of newly created records.
    /// </summary>
    private async Task<(Dictionary<long, long> RecordIdMap, int NewRecordCount)> PromoteRecords(
        List<ExtractionRecord> stagingRecords,
        HashSet<long> selectedRecordIds,
        Dictionary<long, long?> classIdMap,
        long organizationId, long projectId, long extractionId,
        long currentUserId, DateTime now)
    {
        var selected = stagingRecords.Where(r => selectedRecordIds.Contains(r.Id)).ToList();

        // Link records that already exist in the KG — no creation needed
        foreach (var sr in selected.Where(r => r.DeeplynxRecordId.HasValue))
            sr.PromotedId = sr.DeeplynxRecordId!.Value;

        // Batch-create new records
        var toCreate = selected.Where(r => !r.DeeplynxRecordId.HasValue).ToList();
        if (toCreate.Count > 0)
        {
            var newRecords = toCreate.Select(sr =>
            {
                classIdMap.TryGetValue(sr.ExtractionClassId, out var resolvedClassId);

                // Inject originId into Properties for provenance tracking.
                // Parse the LLM-produced attributes into a mutable object, set originId
                // (overwriting any LLM-produced value — ours is authoritative), then serialize back.
                var sProperties = sr.Attributes;
                if (sr.SourceRecordId.HasValue)
                {
                    var jsonObj = string.IsNullOrWhiteSpace(sProperties)
                        ? new JsonObject()
                        : JsonNode.Parse(sProperties)?.AsObject() ?? new JsonObject();
                    jsonObj["originId"] = sr.SourceRecordId.Value;
                    sProperties = jsonObj.ToJsonString();
                }
                sProperties ??= "{}";

                return (sr, new Record
                {
                    Name = sr.Name,
                    OriginalId = Guid.NewGuid().ToString(),
                    Description = string.Empty,
                    Properties = sProperties,
                    ClassId = resolvedClassId,
                    DataSourceId = sr.DataSourceId,
                    ProjectId = projectId,
                    OrganizationId = organizationId,
                    IsArchived = false,
                    Embedded = false,
                    LastUpdatedAt = now,
                    LastUpdatedBy = currentUserId,
                    ExtractionId = extractionId
                });
            }).ToList();

            _context.Records.AddRange(newRecords.Select(x => x.Item2));
            await _context.SaveChangesAsync();

            foreach (var (sr, newRecord) in newRecords)
                sr.PromotedId = newRecord.Id;
        }

        await _latticeContext.SaveChangesAsync();

        var recordIdMap = stagingRecords
            .Where(r => r.PromotedId.HasValue)
            .ToDictionary(r => r.Id, r => r.PromotedId!.Value);

        return (recordIdMap, toCreate.Count);
    }

    /// <summary>
    ///     Promotes novel_discovery and invalid_schema relationships that have no existing ontology match into
    ///     deeplynx.relationships.
    ///     Valid relationships already exist in the ontology and are not re-created.
    ///     Returns a map of ExtractionRelationship.Id → deeplynx Relationship id for use in edge promotion.
    /// </summary>
    private async Task<Dictionary<long, long?>> PromoteRelationships(
        List<ExtractionRelationship> stagingRelationships,
        HashSet<long> selectedRelIds,
        Dictionary<long, long?> classIdMap,
        long organizationId, long projectId, long extractionId,
        long currentUserId, DateTime now)
    {
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

        var toProcessRels = stagingRelationships.Where(r =>
            selectedRelIds.Contains(r.Id) &&
            (r.ValidationStatus == ExtractionValidationStatus.NovelDiscovery ||
             r.ValidationStatus == ExtractionValidationStatus.InvalidSchema) &&
            r.OntologyRelationshipId == null &&
            r.PromotedId == null).ToList();

        // Assign IDs for relationships that already exist in the project
        foreach (var sr in toProcessRels.Where(sr => existingRelByName.ContainsKey(sr.Name)))
            sr.PromotedId = existingRelByName[sr.Name];

        // Batch-create remaining new relationships, deduplicated by name.
        // First occurrence of each name determines the origin/destination classes.
        var newRels = toProcessRels
            .Where(sr => !sr.PromotedId.HasValue)
            .GroupBy(sr => sr.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                classIdMap.TryGetValue(first.OriginClassId, out var originClassId);
                classIdMap.TryGetValue(first.DestinationClassId, out var destClassId);
                return new Relationship
                {
                    Name = g.Key,
                    OriginId = originClassId,
                    DestinationId = destClassId,
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    IsArchived = false,
                    LastUpdatedAt = now,
                    LastUpdatedBy = currentUserId,
                    ExtractionId = extractionId
                };
            })
            .ToList();

        if (newRels.Count > 0)
        {
            _context.Relationships.AddRange(newRels);
            await _context.SaveChangesAsync();

            var nameToNewRelId = newRels.ToDictionary(r => r.Name, r => r.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var sr in toProcessRels.Where(sr => !sr.PromotedId.HasValue))
                sr.PromotedId = nameToNewRelId[sr.Name];
        }

        await _latticeContext.SaveChangesAsync();

        return stagingRelationships.ToDictionary(r => r.Id, r => r.OntologyRelationshipId ?? r.PromotedId);
    }

    /// <summary>
    ///     Promotes extraction edges into deeplynx.edges.
    ///     Edges whose origin or destination record was excluded (invalid_schema) are skipped.
    ///     Returns the count of edges created.
    /// </summary>
    private async Task<int> PromoteEdges(
        List<ExtractionEdge> stagingEdges,
        HashSet<long> selectedEdgeIds,
        Dictionary<long, long> recordIdMap,
        Dictionary<long, long?> relIdMap,
        long organizationId, long projectId, long extractionId,
        long currentUserId, DateTime now)
    {
        var edgePairs = new List<(ExtractionEdge Staging, Edge Promoted)>();

        foreach (var se in stagingEdges.Where(e => selectedEdgeIds.Contains(e.Id)))
        {
            // Skip edges where either endpoint was not promoted
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

        await _context.SaveChangesAsync();
        foreach (var (se, newEdge) in edgePairs)
            se.PromotedId = newEdge.Id;
        await _latticeContext.SaveChangesAsync();

        return edgePairs.Count;
    }

    private static bool ClassPending(ExtractionClass c)
    {
        return !c.PromotedId.HasValue && !c.Rejected;
    }

    private static bool RecordPending(ExtractionRecord r)
    {
        return !r.PromotedId.HasValue && !r.Rejected;
    }

    private static bool RelPending(ExtractionRelationship r)
    {
        return !r.PromotedId.HasValue && !r.Rejected;
    }

    private static bool EdgePending(ExtractionEdge e)
    {
        return !e.PromotedId.HasValue && !e.Rejected;
    }
}