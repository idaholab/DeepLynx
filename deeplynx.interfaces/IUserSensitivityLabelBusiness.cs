using deeplynx.models;

namespace deeplynx.interfaces;

public interface IUserSensitivityLabelBusiness
{
    Task<IEnumerable<UserSensitivityLabelResponseDto>> GetUsersWithAccessToLabel(long labelId, long organizationId, long? projectId);
    Task<UserSensitivityLabelResponseDto> GrantLabelAccess(long currentUserId, long labelId, long userId, long organizationId, long? projectId);
    Task<bool> RevokeLabelAccess(long labelId, long userId, long organizationId, long? projectId);
    Task<bool> SetUsersForLabel(long currentUserId, long labelId, long[] userIds, long organizationId, long? projectId);
    Task<IEnumerable<SensitivityLabelPermissionResponseDto>> GetPermissionsForLabel(long labelId, long organizationId, long? projectId);
}
