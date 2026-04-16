namespace deeplynx.interfaces;

public interface ILatticeOrchestrationBusiness
{
    /// <summary>
    ///     Creates a pending Extraction, builds ontology context via similarity search, triggers
    ///     Lattice asynchronously, and returns the extraction ID. Lattice calls back to
    ///     IExtractionBusiness.LatticeEntityStaging when processing is complete.
    /// </summary>
    Task<long> TriggerLatticeExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long dataSourceId,
        long recordId,
        string mode,
        int similarityLimit);
}
