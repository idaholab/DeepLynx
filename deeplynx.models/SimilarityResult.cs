namespace deeplynx.models;

public class SimilarityResult
{
    public long OntologyEntityId { get; init; }
    public string OntologyEntityName { get; init; } = null!;
    public double Score { get; init; }
}
