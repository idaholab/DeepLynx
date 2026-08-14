using deeplynx.models;

namespace deeplynx.interfaces;

public interface IGroupBusiness
{
    [Obsolete("V1-only. Used by deprecated v1 group endpoints. Superseded by GetGroupMembersPaginated. " +
              "Remove once v1 group endpoints are sunset.", error: false)]
    Task<IEnumerable<UserResponseDto>> GetGroupMembers(long organizationId, long groupId);
    Task<IEnumerable<GroupResponseDto>> GetAllGroups(long organizationId, bool hideArchived = true);
    Task<PaginatedResponse<UserResponseDto>> GetGroupMembersPaginated(long organizationId, long groupId, PaginatedRequestDto paginatedRequestDto);
    Task<GroupResponseDto> GetGroup(long organizationId, long groupId, bool hideArchived = true);
    Task<GroupResponseDto> CreateGroup(long currentUserId, long organizationId, CreateGroupRequestDto dto);

    Task<GroupResponseDto> UpdateGroup(long currentUserId, long organizationId, long groupId,
        UpdateGroupRequestDto dto);

    Task<bool> ArchiveGroup(long currentUserId, long organizationId, long groupId);
    Task<bool> UnarchiveGroup(long currentUserId, long organizationId, long groupId);
    Task<bool> DeleteGroup(long currentUserId, long organizationId, long groupId);
    Task<bool> AddUserToGroup(long userId, long organizationId, long groupId);
    Task<bool> RemoveUserFromGroup(long userId, long organizationId, long groupId);
}