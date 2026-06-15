using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public partial class LatticeExtractionBusiness : ILatticeExtractionBusiness
{
   
    private const double DefaultConfidence = 0.9;
    private const double SimilarityThreshold = 0.8;

    private (List<DedupedRecord> Records, List<DedupedEdge> Edges) Deduplicate(InsightExtractionCallbackDto dto)
    {
        var records = dto.Classes
            .GroupBy(c => (c.Class.Trim().ToLowerInvariant(), c.ClassType.Trim().ToLowerInvariant()))
            .Select(g =>
            {
                var first = g.First();
                return new DedupedRecord(
                    first.Class.Trim(),
                    first.ClassType.Trim(),
                    g.Average(c => c.Confidence == 0 ? DefaultConfidence : c.Confidence),
                    MergeAttributes(g.Select(c => c.Attributes)),
                    g.Count());
            })
            .ToList();

        var edges = dto.Relationships
            .GroupBy(r => (
                r.Subject.Trim().ToLowerInvariant(),
                r.RelationshipType.Trim().ToLowerInvariant(),
                r.Object.Trim().ToLowerInvariant()))
            .Select(g =>
            {
                var first = g.First();
                return new DedupedEdge(
                    first.Subject.Trim(),
                    first.SubjectType.Trim(),
                    first.RelationshipType.Trim(),
                    first.Object.Trim(),
                    first.ObjectType.Trim(),
                    g.Average(r => r.Confidence == 0 ? DefaultConfidence : r.Confidence),
                    g.Count());
            })
            .ToList();

        return (records, edges);
    }

    // For each unique class type in the payload, finds the best-matching ontology class by
    // trigram similarity. Matches at or above SimilarityThreshold adopt the ontology name/ID;
    // below threshold the entry is null (caller applies STRICT/DISCOVERY validation logic).
    // Swap this implementation for pg_trgm or embedding similarity when ready to improve.
    private async Task<Dictionary<string, SimilarityResult?>> NormalizeClassTypes(
        IEnumerable<string> classTypes, long projectId)
    {
        var ontologyClasses = await _context.Classes
            .Where(c => c.ProjectId == projectId)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        var result = new Dictionary<string, SimilarityResult?>(StringComparer.OrdinalIgnoreCase);

        foreach (var classType in classTypes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SimilarityResult? best = null;
            foreach (var ontClass in ontologyClasses)
            {
                var score = TrigramSimilarity(classType, ontClass.Name);
                if (score >= SimilarityThreshold && (best == null || score > best.Score))
                    best = new SimilarityResult { OntologyEntityId = ontClass.Id, OntologyEntityName = ontClass.Name, Score = score };
            }
            result[classType] = best;
        }

        return result;
    }

    // Same as NormalizeClassTypes but for relationship types.
    private async Task<Dictionary<string, SimilarityResult?>> NormalizeRelationshipTypes(
        List<DedupedEdge> edges, long projectId)
    {
        var ontologyRelationships = await _context.Relationships
            .Where(r => r.ProjectId == projectId)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();

        var result = new Dictionary<string, SimilarityResult?>(StringComparer.OrdinalIgnoreCase);

        foreach (var relType in edges.Select(e => e.RelationshipType).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SimilarityResult? best = null;
            foreach (var ontRel in ontologyRelationships)
            {
                var score = TrigramSimilarity(relType, ontRel.Name);
                if (score >= SimilarityThreshold && (best == null || score > best.Score))
                    best = new SimilarityResult { OntologyEntityId = ontRel.Id, OntologyEntityName = ontRel.Name, Score = score };
            }
            result[relType] = best;
        }

        return result;
    }

    // Loads all ontology relationship patterns for the project as (originClass, relationship, destinationClass) triples.
    // Used by stage methods to compute structural_consistency for records and edges.
    private async Task<HashSet<OntologyPattern>> GetOntologyPatterns(long projectId)
    {
        var patterns = await _context.Relationships
            .Where(r => r.ProjectId == projectId)
            .Select(r => new
            {
                OriginClassName = r.Origin!.Name,
                r.Name,
                DestinationClassName = r.Destination!.Name
            })
            .ToListAsync();

        return patterns
            .Select(p => new OntologyPattern(p.OriginClassName, p.Name, p.DestinationClassName))
            .ToHashSet();
    }

    // ensemble_score = llm(40%) + embedding(30%) + frequency(20%) + structural(10%)
    // statistical_frequency must be pre-normalized to [0,1] by the caller (frequency / max_frequency).
    private double CalculateEnsembleScore(
        double llmScore,
        double embeddingPlausibility,
        double statisticalFrequency,
        double structuralConsistency)
        => (llmScore * 0.4) + (embeddingPlausibility * 0.3) + (statisticalFrequency * 0.2) + (structuralConsistency * 0.1);

    private static double TrigramSimilarity(string s1, string s2)
    {
        var t1 = GetTrigrams(s1);
        var t2 = GetTrigrams(s2);
        var intersection = t1.Intersect(t2).Count();
        var union = t1.Count + t2.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static HashSet<string> GetTrigrams(string s)
    {
        var padded = $"  {s.ToLowerInvariant().Replace(" ", "")} ";
        var trigrams = new HashSet<string>();
        for (var i = 0; i < padded.Length - 2; i++)
            trigrams.Add(padded.Substring(i, 3));
        return trigrams;
    }

    private static JsonObject? MergeAttributes(IEnumerable<JsonObject?> attributeSets)
    {
        JsonObject? merged = null;
        foreach (var attrs in attributeSets.Where(a => a is not null))
        {
            merged ??= new JsonObject();
            foreach (var (key, value) in attrs!)
                merged[key] = value?.DeepClone();
        }
        return merged;
    }
}
