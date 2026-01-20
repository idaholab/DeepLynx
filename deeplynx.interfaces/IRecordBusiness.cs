using deeplynx.models;

namespace deeplynx.interfaces;

public interface IRecordBusiness
{
    Task<List<RecordResponseDto>> GetAllRecords(
        long organizationId, long projectId, long? dataSourceId, bool hideArchived, string? fileType);

    Task<List<RecordResponseDto>> GetRecordsByTags(
        long organizationId, long projectId, long[] tagIds, bool hideArchived);

    Task<RecordResponseDto> GetRecord(
        long organizationId, long projectId, long recordId, bool hideArchived);

    Task<int> GetRecordsCountByDataSource(
        long organizationId, long projectId, long dataSourceId, bool hideArchived);

    Task<RecordResponseDto> CreateRecord(
        long currentUserId, long organizationId, long projectId, long dataSourceId, CreateRecordRequestDto dto);

    Task<List<RecordResponseDto>> BulkCreateRecords(
        long currentUserId, long organizationId, long projectId, long dataSourceId, List<CreateRecordRequestDto> dtos);

    Task<RecordResponseDto> UpdateRecord(
        long currentUserId, long organizationId, long projectId, long recordId, UpdateRecordRequestDto dto);

    Task<bool> DeleteRecord(long currentUserId, long organizationId, long projectId, long recordId);
    Task<bool> ArchiveRecord(long currentUserId, long organizationId, long projectId, long recordId);
    Task<bool> UnarchiveRecord(long currentUserId, long organizationId, long projectId, long recordId);
    Task<bool> AttachTag(long organizationId, long projectId, long recordId, long tagId);
    Task<bool> UnattachTag(long organizationId, long projectId, long recordId, long tagId);
    Task<bool> BulkAttachTags(List<RecordTagLinkDto> dtos);
    Task<List<RecordResponseDto>> GetRecordsByOriginalId(long organizationId, long projectId, List<string> originalIds);
    Task<List<LatticeRecordDto>> GetLatticeRecords(long organizationId, long projectId);
}