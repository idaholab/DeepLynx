using deeplynx.models;

namespace deeplynx.interfaces;

public interface IClassBusiness
{
    Task<List<ClassResponseDto>> GetAllClasses(long organizationId, long[]? projectIds, bool hideArchived);
    Task<ClassResponseDto> GetClass(long organizationId, long? projectId, long classId, bool hideArchived);

    Task<ClassResponseDto> CreateClass(
        long currentUserId, long organizationId, long? projectId, CreateClassRequestDto dto);

    Task<List<ClassResponseDto>> BulkCreateClasses(
        long currentUserId, long organizationId, long? projectId,
        List<CreateClassRequestDto> classRequestDtos);

    Task<ClassResponseDto> UpdateClass(
        long currentUserId, long organizationId, long? projectId,
        long classId, UpdateClassRequestDto dto);

    Task<bool> ArchiveClass(long currentUserId, long organizationId, long? projectId, long classId);
    Task<bool> UnarchiveClass(long currentUserId, long organizationId, long? projectId, long classId);
    Task<bool> DeleteClass(long currentUserId, long organizationId, long? projectId, long classId);

    Task<ClassResponseDto> GetOrCreateClass(
        long currentUserId, long organizationId, long? projectId, string className);

    Task<List<LatticeClassDto>> GetLatticeClasses(long organizationId, long projectId);
}