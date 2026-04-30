using deeplynx.models;

namespace deeplynx.interfaces;

public interface IDataSourceBusiness
{
    Task<List<DataSourceResponseDto>>
        GetAllDataSources(long organizationId, long[]? projectIds, bool hideArchived = true);

    Task<DataSourceResponseDto> GetDataSource(long organizationId, long? projectId, long dataSourceId,
        bool hideArchived = true);

    Task<DataSourceResponseDto> GetDefaultDataSource(long organizationId, long? projectId);

    Task<DataSourceResponseDto> SetDefaultDataSource(long organizationId, long? projectId, long currentUserId,
        long dataSourceId);

    Task<DataSourceResponseDto> CreateDataSource(long organizationId, long? projectId, long currentUserId,
        CreateDataSourceRequestDto dto);

    Task<DataSourceResponseDto> UpdateDataSource(long organizationId, long? projectId, long currentUserId,
        long dataSourceId, UpdateDataSourceRequestDto dto);

    Task<bool> DeleteDataSource(long organizationId, long? projectId, long dataSourceId);
    Task<bool> ArchiveDataSource(long organizationId, long? projectId, long currentUserId, long dataSourceId);
    Task<bool> UnarchiveDataSource(long organizationId, long? projectId, long currentUserId, long dataSourceId);
}