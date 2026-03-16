using deeplynx.models;

namespace deeplynx.interfaces;

public interface IRecordBusiness
{
    Task<List<RecordResponseDto>> GetAllRecords(
        long currentUserId, long organizationId, long projectId, long? dataSourceId, bool hideArchived, string? fileType);

    Task<List<RecordResponseDto>> GetRecordsByTags(
        long currentUserId, long organizationId, long projectId, long[] tagIds, bool hideArchived);

    Task<RecordResponseDto> GetRecord(
        long currentUserId, long organizationId, long projectId, long recordId, bool hideArchived);

    Task<int> GetRecordsCountByDataSource(
        long organizationId, long projectId, long dataSourceId, bool hideArchived);

    Task<RecordResponseDto> CreateRecord(
        long currentUserId, long organizationId, long projectId, long dataSourceId, CreateRecordRequestDto dto, 
        List<long>? sensitivityLabelIds = null, bool? embedded = false);

    Task<List<RecordResponseDto>> BulkCreateRecords(
        long currentUserId, long organizationId, long projectId, long dataSourceId, List<CreateRecordRequestDto> dtos, List<long>? sensitivityLabelIds = null);

    Task<RecordResponseDto> UpdateRecord(
        long currentUserId, long organizationId, long projectId, long recordId, UpdateRecordRequestDto dto, bool? embedded = false);

    Task<bool> DeleteRecord(long currentUserId, long organizationId, long projectId, long recordId);
    Task<bool> ArchiveRecord(long currentUserId, long organizationId, long projectId, long recordId);
    Task<bool> UnarchiveRecord(long currentUserId, long organizationId, long projectId, long recordId);
    Task<bool> AttachTag(long currentUserId, long organizationId, long projectId, long recordId, long tagId);
    Task<bool> AttachLabel(long currentUserId, long organizationId, long projectId, long recordId, long labelId);
    Task<bool> UnattachTag(long currentUserId, long organizationId, long projectId, long recordId, long tagId);
    Task<bool> UnattachLabel(long currentUserId, long organizationId, long projectId, long recordId, long labelId);
    Task<bool> BulkAttachTags(List<RecordTagLinkDto> dtos);
    Task<bool> BulkAttachLabels(
        long currentUserId, long organizationId, long projectId, List<long> recordIds, List<long> sensitiityLabelIds);
    Task<List<RecordResponseDto>> GetRecordsByOriginalId(long currentUserId, long organizationId, long projectId, List<string> originalIds);
    Task<List<LatticeRecordDto>> GetLatticeRecords(long organizationId, long projectId);
}