using deeplynx.models;

namespace deeplynx.interfaces;

public interface IQueryBusiness
{
    Task<IEnumerable<HistoricalRecordResponseDto>> Search(long currentUserId, string query, long organizationId, long[] projectIds,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<IEnumerable<HistoricalRecordResponseDto>> QueryBuilder(long currentUserId, CustomQueryDtos.CustomQueryRequestDto[] request,
        long organizationId, long[] projectIds, string? textSearch, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<IEnumerable<HistoricalRecordResponseDto>> GetRecentlyAddedRecords(long currentUserId, long organizationId,
        long[] projectId, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<IEnumerable<HistoricalRecordResponseDto>> GetMultiProjectRecords(long currentUserId, long organizationId, long[] projects,
        bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);
}