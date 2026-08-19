using deeplynx.models;

namespace deeplynx.interfaces;

public interface IQueryBusiness
{
    [Obsolete("V1-only. Used by deprecated v1 query endpoints. Superseded by SearchPaginated. " +
              "Remove once v1 query endpoints are sunset.", error: false)]
    Task<IEnumerable<QueryRecordViewResponseDto>> Search(long currentUserId, string query, long organizationId, long[] projectIds,
        bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<PaginatedResponse<QueryRecordViewResponseDto>> SearchPaginated(long currentUserId, string query, long organizationId, long[] projectIds,
        PaginatedRequestDto paginatedRequestDto, bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<IEnumerable<QueryRecordViewResponseDto>> QueryBuilder(long currentUserId, CustomQueryDtos.CustomQueryRequestDto[] request,
        long organizationId, long[] projectIds, string? textSearch, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<PaginatedResponse<QueryRecordViewResponseDto>> QueryBuilderPaginated(long currentUserId, CustomQueryDtos.CustomQueryRequestDto[] request,
        long organizationId, long[] projectIds, PaginatedRequestDto paginated, string? textSearch, bool isSysAdmin = false,
        bool isOrgAdmin = false);

    Task<IEnumerable<QueryRecordViewResponseDto>> GetRecentlyAddedRecords(long currentUserId, long organizationId,
        long[] projectId, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<PaginatedResponse<QueryRecordViewResponseDto>> GetRecordsPaginated(long currentUserId, long organizationId, SortRecordsRequestDto sortBy,
        PaginatedRequestDto paginated, long[] projectId, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<IEnumerable<QueryRecordViewResponseDto>> GetMultiProjectRecords(long currentUserId, long organizationId, long[] projects,
        bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);
}