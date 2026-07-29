using deeplynx.models;

namespace deeplynx.interfaces;

public interface IObjectStorageBusiness
{
    Task<List<ObjectStorageResponseDto>> GetAllObjectStorages(
        long organizationId, long? projectId, bool hideArchived);

    Task<ObjectStorageResponseDto> GetObjectStorage(
        long organizationId, long? projectId, long objectStorageId, bool hideArchived);

    Task<ObjectStorageResponseDto> CreateObjectStorage(
        long currentUserId, long organizationId, long? projectId,
        CreateObjectStorageRequestDto dto);

    Task<ObjectStorageResponseDto> UpdateObjectStorage(
        long currentUserId, long organizationId, long? projectId,
        long objectStorageId, UpdateObjectStorageRequestDto dto);

    Task<bool> DeleteObjectStorage(
        long currentUserId, long organizationId, long? projectId, long objectStorageId);

    Task<bool> ArchiveObjectStorage(
        long currentUserId, long organizationId, long? projectId, long objectStorageId);

    Task<bool> UnarchiveObjectStorage(
        long currentUserId, long organizationId, long? projectId, long objectStorageId);

    Task<ObjectStorageResponseDto> SetDefaultObjectStorage(
        long currentUserId, long organizationId, long? projectId, long objectStorageId);

    Task<ObjectStorageResponseDto> GetDefaultObjectStorage(
        long organizationId, long? projectId);

    Task<ObjectStorageDecryptedDto> GetDecryptedObjectStorage(long objectStorageId);

    Task<List<ObjectStorageDecryptedDto>> GetDecryptedObjectStorages(
        long? organizationId,
        long? projectId,
        List<long>? objectStorageIds);
}