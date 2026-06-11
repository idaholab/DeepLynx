using deeplynx.models;

namespace deeplynx.interfaces;

public interface IQueryBusiness
{
    Task<IEnumerable<QueryRecordViewResponseDto>> Search(long currentUserId, string query, long organizationId, long[] projectIds,
        bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<IEnumerable<QueryRecordViewResponseDto>> QueryBuilder(long currentUserId, CustomQueryDtos.CustomQueryRequestDto[] request,
        long organizationId, long[] projectIds, string? textSearch, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<IEnumerable<QueryRecordViewResponseDto>> GetRecentlyAddedRecords(long currentUserId, long organizationId,
        long[] projectId, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<PaginatedResponse<QueryRecordViewResponseDto>> GetRecordsPaginated(long currentUserId, long organizationId, string sortBy,
        PaginatedRequestDto paginated, long[] projectId, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<IEnumerable<QueryRecordViewResponseDto>> GetMultiProjectRecords(long currentUserId, long organizationId, long[] projects,
        bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);
}