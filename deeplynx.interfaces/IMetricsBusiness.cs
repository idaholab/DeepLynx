namespace deeplynx.interfaces;

public interface IMetricsBusiness
{
    // File Storage (GB)
    Task<long> GetObjectStorageSize(long organizationId, long? projectId, long objectStorageId);
    Task<Dictionary<long, long>> GetProjectStorageSize(long organizationId, long projectId);
    Task<Dictionary<long, long>> GetOrganizationStorageSize(long organizationId);
    Task<Dictionary<long, long>> GetSystemStorageSize();
}