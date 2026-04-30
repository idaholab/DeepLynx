using deeplynx.models.MetricsDTOs;

namespace deeplynx.interfaces;

public interface IMetricsBusiness
{
    // File Storage (GB)
    Task<StorageSizeDto> GetObjectStorageSize(long organizationId, long? projectId, long objectStorageId);
    Task<StorageSizeDto> GetProjectStorageSize(long organizationId, long projectId);
    Task<StorageSizeDto> GetOrganizationStorageSize(long organizationId);
    Task<StorageSizeDto> GetSystemStorageSize();
    Task<int> GetDataSourceCount(bool hideArchived = true);
}