using deeplynx.models;

namespace deeplynx.interfaces;

public interface ITagBusiness
{
    Task<List<TagResponseDto>> GetAllTags(long organizationId, long[]? projectId, bool hideArchived);
    Task<TagResponseDto> GetTag(long organizationId, long? projectId, long tagId, bool hideArchived);
    Task<TagResponseDto> CreateTag(long organizationId, long currentUserId, long? projectId, CreateTagRequestDto tagRequestDto);
    Task<List<TagResponseDto>> BulkCreateTags(long organizationId, long currentUserId, long? projectId, List<CreateTagRequestDto> tags);
    Task<TagResponseDto> UpdateTag(long organizationId, long currentUserId, long? projectId, long tagId, UpdateTagRequestDto tagRequestDto);
    Task<bool> DeleteTag(long organizationId, long? projectId, long tagId);
    Task<bool> ArchiveTag(long organizationId, long currentUserId, long? projectId, long tagId);
    Task<bool> UnarchiveTag(long organizationId, long currentUserId, long? projectId, long tagId);
    Task<List<TagResponseDto>> GetTagsByName(long organizationId, long? projectId, List<string> tagNames,  bool hideArchived);

}