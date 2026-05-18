using deeplynx.models;

namespace deeplynx.interfaces;

public interface IExtractionValidation
{
    (List<DedupedRecord> Records, List<DedupedEdge> Edges) Deduplicate(InsightExtractionCallbackDto dto);

    Task<Dictionary<string, SimilarityResult?>> NormalizeClassTypes(IEnumerable<string> classTypes, long projectId);

    Task<Dictionary<string, SimilarityResult?>> NormalizeRelationshipTypes(List<DedupedEdge> edges, long projectId);

    Task<HashSet<OntologyPattern>> GetOntologyPatterns(long projectId);

    double CalculateEnsembleScore(double llmScore, double embeddingPlausibility, double statisticalFrequency, double structuralConsistency);
}
