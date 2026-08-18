using deeplynx.models;

namespace deeplynx.interfaces;

public interface ITagBusiness
{
    [Obsolete("V1-only. Used by deprecated v1 tag endpoints. Superseded by GetAllTagsPaginated. " +
              "Remove once v1 tag endpoints are sunset.", error: false)]
    Task<List<TagResponseDto>> GetAllTags(long currentUserId, long organizationId, long[]? projectId, bool hideArchived, bool isSysAdmin, bool isOrgAdmin);
    Task<PaginatedResponse<TagResponseDto>> GetAllTagsPaginated(
        long currentUserId,
        long organizationId,
        long[]? projectIds,
        PaginatedRequestDto paginatedRequestDto,
        bool hideArchived = true,
        bool isSysAdmin = false,
        bool isOrgAdmin = false);
    Task<TagResponseDto> GetTag(long organizationId, long? projectId, long tagId, bool hideArchived);
    Task<TagResponseDto> CreateTag(long organizationId, long currentUserId, long? projectId, CreateTagRequestDto tagRequestDto);
    Task<List<TagResponseDto>> BulkCreateTags(long organizationId, long currentUserId, long? projectId, List<CreateTagRequestDto> tags);
    Task<TagResponseDto> UpdateTag(long organizationId, long currentUserId, long? projectId, long tagId, UpdateTagRequestDto tagRequestDto);
    Task<bool> DeleteTag(long organizationId, long? projectId, long tagId);
    Task<bool> ArchiveTag(long organizationId, long currentUserId, long? projectId, long tagId);
    Task<bool> UnarchiveTag(long organizationId, long currentUserId, long? projectId, long tagId);
    Task<List<TagResponseDto>> GetTagsByName(long organizationId, long? projectId, List<string> tagNames, bool hideArchived);

}