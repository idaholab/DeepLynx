using deeplynx.datalayer.Models;
using deeplynx.models;

namespace deeplynx.interfaces;

public interface IRecordCollectionBusiness
{
    Task<PaginatedResponse<RecordCollectionResponseDto>> GetAllRecordCollections(
        long currentUserId, long organizationId, long projectId, RecordCollectionQueryRequestDto dto,
        bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<List<RecordResponseDto>> GetRecordsInRecordCollection(
        long currentUserId, long organizationId, long projectId, long recordCollectionId, bool hideArchived,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<List<RecordCollectionResponseDto>> GetRecordCollectionsByTags(
        long currentUserId, long organizationId, long projectId, long[] tagIds, bool hideArchived,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<bool> AddRecordsToRecordCollection(
        long currentUserID, long organizationId, long projectId, long recordCollectionId,
        long[] recordIds, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<bool> RemoveRecordsFromRecordCollection(
        long currentUserID, long organizationId, long projectId, long recordCollectionId,
        long[] recordIds, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<RecordCollectionResponseDto> CreateRecordCollection(
        long currentUserId, long organizationId, long projectId, List<long>? sensitivityLabelIds, CreateRecordCollectionRequestDto dto);

    Task<RecordCollectionResponseDto> UpdateRecordCollection(
        long currentUserId, long organizationId, long projectId, long recordCollectionId, UpdateRecordCollectionRequestDto dto);

    Task<bool> DeleteRecordCollection(
        long currentUserId, long organizationId, long projectId, long recordCollectionId);
    Task<bool> ArchiveRecordCollection(long currentUserId, long organizationId, long projectId, long recordCollectionId);
    Task<bool> UnarchiveRecordCollection(long currentUserId, long organizationId, long projectId, long recordCollectionId);
    Task<bool> AttachTag(long organizationId, long projectId, long recordCollectionId, long tagId);
    Task<bool> AttachLabel(long organizationId, long projectId, long recordCollectionId, long labelId);
    Task<bool> UnattachTag(long organizationId, long projectId, long recordCollectionId, long tagId);
    Task<bool> UnattachLabel(long organizationId, long projectId, long recordCollectionId, long labelId);
    Task<List<SensitivityLabel>> GetSensitivityLabelsForRecordCollection(long organizationId, long projectId,
        long recordCollectionId);
}
