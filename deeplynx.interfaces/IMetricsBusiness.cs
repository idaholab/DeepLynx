using deeplynx.models.MetricsDTOs;

namespace deeplynx.interfaces;

public interface IMetricsBusiness
{
    // File Storage (GB)
    Task<StorageSizeDto> GetObjectStorageSize(long organizationId, long? projectId, long objectStorageId);
    Task<StorageSizeDto> GetProjectStorageSize(long organizationId, long projectId);
    Task<StorageSizeDto> GetOrganizationStorageSize(long organizationId);
    Task<StorageSizeDto> GetSystemStorageSize();
    Task<int> GetProjectDataSourceCount(long projectId, bool hideArchived = true);
    Task<int> GetOrganizationDataSourceCount(long organizationId, long[]? projectIds, bool hideArchived = true);
    Task<int> GetSystemDataSourceCount(bool hideArchived = true);
}