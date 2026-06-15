using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

public partial class LatticeExtractionBusiness : ILatticeExtractionBusiness
{
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
                        c.OntologyClassId == null && c.PromotedId == null && !c.Rejected &&
                        selectedClassIds.Contains(c.Id))
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
                     c.PromotedId == null &&
                     !c.Rejected &&
                     selectedClassIds.Contains(c.Id)))
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
    private async Task<Dictionary<long, long>> PromoteRecords(
        List<ExtractionRecord> stagingRecords,
        HashSet<long> selectedRecordIds,
        Dictionary<long, long?> classIdMap,
        long organizationId, long projectId, long extractionId,
        long currentUserId, DateTime now)
    {
        foreach (var sr in stagingRecords)
        {
            // Skip items already promoted in a prior round (idempotency), rejected items, and items not
            // selected this round.
            if (sr.PromotedId.HasValue || sr.Rejected) continue;
            if (!selectedRecordIds.Contains(sr.Id)) continue;

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
        }

        await _latticeContext.SaveChangesAsync();

        return stagingRecords
            .Where(r => r.PromotedId.HasValue)
            .ToDictionary(r => r.Id, r => r.PromotedId!.Value);
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
                        r.OntologyRelationshipId == null && r.PromotedId == null && !r.Rejected &&
                        selectedRelIds.Contains(r.Id))
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
                     r.PromotedId == null &&
                     !r.Rejected &&
                     selectedRelIds.Contains(r.Id)))
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
        HashSet<long> selectedEdgeIds,
        Dictionary<long, long> recordIdMap,
        Dictionary<long, long?> relIdMap,
        long organizationId, long projectId, long extractionId,
        long currentUserId, DateTime now)
    {
        var edgePairs = new List<(ExtractionEdge Staging, Edge Promoted)>();

        foreach (var se in stagingEdges)
        {
            // Skip edges already promoted in a prior round (idempotency), rejected edges, and edges not
            // selected this round.
            if (se.PromotedId.HasValue || se.Rejected) continue;
            if (!selectedEdgeIds.Contains(se.Id)) continue;

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
}