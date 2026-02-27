using deeplynx.models;

namespace deeplynx.interfaces;

public interface IQueryBusiness
{
    Task<IEnumerable<HistoricalRecordResponseDto>> Search(long currentUserId, string query, long organizationId, long[] projectIds);

    Task<IEnumerable<HistoricalRecordResponseDto>> QueryBuilder(long currentUserId, CustomQueryDtos.CustomQueryRequestDto[] request,
        long organizationId, long[] projectIds, string? textSearch);

    Task<IEnumerable<HistoricalRecordResponseDto>> GetRecentlyAddedRecords(long currentUserId, long organizationId,
        long[] projectId);

    Task<IEnumerable<HistoricalRecordResponseDto>> GetMultiProjectRecords(long currentUserId, long organizationId, long[] projects,
        bool hideArchived);
}