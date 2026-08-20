using deeplynx.datalayer.Models;
using deeplynx.models;

namespace deeplynx.interfaces;

public interface IRecordCollectionBusiness
{
    [Obsolete("V1-only. Used by deprecated v1 record collection endpoints. Superseded by " +
              "GetAllRecordCollectionsPaginated. Remove once v1 record collection endpoints are sunset.", error: false)]
    Task<PaginatedResponse<RecordCollectionResponseDto>> GetAllRecordCollections(
        long currentUserId, long organizationId, long projectId, RecordCollectionQueryRequestDto dto,
        bool hideArchived, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<PaginatedResponse<RecordCollectionResponseDto>> GetAllRecordCollectionsPaginated(
        long currentUserId, long organizationId, long projectId,
        string? search, long[]? sensitivityLabelIds, long[]? tagIds, string? sort,
        PaginatedRequestDto paginatedRequestDto,
        bool hideArchived = true, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    [Obsolete("V1-only. Used by deprecated v1 record collection endpoints. Superseded by GetRecordsInRecordCollectionPaginated. " +
              "Remove once v1 record collection endpoints are sunset.", error: false)]
    Task<List<RecordResponseDto>> GetRecordsInRecordCollection(
        long currentUserId, long organizationId, long projectId, long recordCollectionId, bool hideArchived,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);
    Task<PaginatedResponse<RecordResponseDto>> GetRecordsInRecordCollectionPaginated(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordCollectionId,
        bool hideArchived,
        PaginatedRequestDto paginatedRequestDto,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false);

    [Obsolete("V1-only. Used by deprecated v1 record collection endpoints. Superseded by " +
              "GetRecordCollectionsForRecordPaginated. Remove once v1 record collection endpoints are sunset.", error: false)]
    Task<PaginatedResponse<RecordCollectionResponseDto>> GetRecordCollectionsForRecord(
        long currentUserId, long organizationId, long projectId, long recordId, bool hideArchived,
        RecordCollectionQueryRequestDto dto, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<PaginatedResponse<RecordCollectionResponseDto>> GetRecordCollectionsForRecordPaginated(
        long currentUserId, long organizationId, long projectId, long recordId, bool hideArchived,
        PaginatedRequestDto paginatedRequestDto, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

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
