using deeplynx.models;

namespace deeplynx.interfaces;

public interface IRecordBusiness
{
    Task<List<RecordResponseDto>> GetAllRecords(
        long currentUserId, long organizationId, long projectId, long? dataSourceId, bool hideArchived, string? fileType,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<PaginatedResponse<RecordResponseDto>> GetAllRecordsPaginated(
    long currentUserId, long organizationId, long projectId, bool hideArchived,
    RecordQueryRequestDto? queryDto,
    bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<List<RecordResponseDto>> GetRecordsByTags(
        long currentUserId, long organizationId, long projectId, long[] tagIds, bool hideArchived,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<RecordResponseDto> GetRecord(
        long currentUserId, long organizationId, long projectId, long recordId, bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false,
        bool isProjectAdmin = false);

    Task<int> GetRecordsCountByDataSource(
        long organizationId, long projectId, long dataSourceId, bool hideArchived);

    Task<RecordResponseDto> CreateRecord(
        long currentUserId, long organizationId, long projectId, long dataSourceId, CreateRecordRequestDto dto, 
        List<long>? sensitivityLabelIds = null, bool embedded = false,  bool isSysAdmin = false, bool isOrgAdmin = false,
        bool isProjectAdmin = false);

    Task<List<RecordResponseDto>> BulkCreateRecords(
        long currentUserId, long organizationId, long projectId, long dataSourceId, List<CreateRecordRequestDto> dtos, List<long>? sensitivityLabelIds = null,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<RecordResponseDto> UpdateRecord(
        long currentUserId, long organizationId, long projectId, long recordId, UpdateRecordRequestDto dto, bool isSysAdmin = false, bool isOrgAdmin = false,
        bool isProjectAdmin = false);

    Task<bool> DeleteRecord(long currentUserId, long organizationId, long projectId, long recordId);
    Task<bool> ArchiveRecord(long currentUserId, long organizationId, long projectId, long recordId);
    Task<bool> UnarchiveRecord(long currentUserId, long organizationId, long projectId, long recordId);
    Task<bool> AttachTag(long currentUserId, long organizationId, long projectId, long recordId, long tagId);
    Task<bool> AttachLabel(long currentUserId, long organizationId, long projectId, long recordId, long labelId);
    Task<bool> UnattachTag(long currentUserId, long organizationId, long projectId, long recordId, long tagId);
    Task<bool> UnattachLabel(long currentUserId, long organizationId, long projectId, long recordId, long labelId);
    Task<bool> BulkInsertRecordTagLinks(List<RecordTagLinkDto> dtos);
    Task<bool> BulkDeleteRecordTagLinks(List<RecordTagLinkDto> dtos);
    Task<bool> BulkAttachLabels(
        long currentUserId, long organizationId, long projectId, List<long> recordIds, List<long> sensitiityLabelIds);
    Task<List<RecordResponseDto>> GetRecordsByOriginalId(long currentUserId, long organizationId, long projectId, long dataSourceId, List<string> originalIds, 
        bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);
    Task<List<LatticeRecordDto>> GetLatticeRecords(long organizationId, long projectId);
    Task<bool> BulkAttachTags(long currentUserId, long organizationId, long projectId,
        List<RecordTagLinkDto> dtos);
    Task<bool> BulkUnattachTags(long currentUserId, long organizationId, long projectId,
        List<RecordTagLinkDto> dtos);
}