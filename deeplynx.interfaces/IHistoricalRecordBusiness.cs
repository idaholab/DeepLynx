using deeplynx.models;

namespace deeplynx.interfaces;

public interface IHistoricalRecordBusiness
{
    [Obsolete("V1-only. Used by deprecated v1 historical record endpoints. Superseded by GetAllHistoricalRecordsPaginated. " +
              "Remove once v1 historical record endpoints are sunset.", error: false)]
    Task<IEnumerable<HistoricalRecordResponseDto>> GetAllHistoricalRecords(
        long currentUserId,
        long projectId,
        long organizationalId,
        long? dataSourceId,
        DateTime? pointInTime,
        bool hideArchived,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false);

    Task<PaginatedResponse<HistoricalRecordResponseDto>> GetAllHistoricalRecordsPaginated(
        long currentUserId,
        long projectId,
        long organizationalId,
        PaginatedRequestDto paginatedRequestDto,
        long? dataSourceId,
        DateTime? pointInTime,
        bool hideArchived,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false);

    Task<IEnumerable<HistoricalRecordResponseDto>> GetHistoryForRecord(
        long currentUserId,
        long recordId,
        long organizationId,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false);

    Task<HistoricalRecordResponseDto> GetHistoricalRecord(
        long currentUserId,
        long recordId,
        long organizationId,
        DateTime? pointInTime,
        bool hideArchived,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false);
}