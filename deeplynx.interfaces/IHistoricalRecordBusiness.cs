using deeplynx.models;

namespace deeplynx.interfaces;

public interface IHistoricalRecordBusiness
{
    Task<IEnumerable<HistoricalRecordResponseDto>> GetAllHistoricalRecords(
        long currentUserId, long projectId, long organizationalId, long? dataSourceId, DateTime? pointInTime, bool hideArchived);

    Task<IEnumerable<HistoricalRecordResponseDto>> GetHistoryForRecord(
        long currentUserId, long recordId, long organizationId);

    Task<HistoricalRecordResponseDto> GetHistoricalRecord(
        long currentUserId, long recordId, long organizationId, DateTime? pointInTime, bool hideArchived);
}