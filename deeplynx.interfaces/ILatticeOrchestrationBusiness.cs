namespace deeplynx.interfaces;

public interface ILatticeOrchestrationBusiness
{
    Task<long> TriggerLatticeExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long dataSourceId,
        long recordId,
        string mode);
}
