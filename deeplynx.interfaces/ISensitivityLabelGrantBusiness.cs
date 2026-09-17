using deeplynx.models;

namespace deeplynx.interfaces;

public interface ISensitivityLabelGrantBusiness
{
    Task<IEnumerable<SensitivityLabelMemberAccessDto>> GetMemberPermissionsForLabel(
        long labelId, long organizationId, long? projectId, 
        long? userId = null, long? groupId = null);

    Task<SensitivityLabelUserAccessDto> GetUserPermissionsMatrixForLabel(
        long labelId, long organizationId, long? projectId, long userId);

    Task<IEnumerable<SensitivityLabelMemberAccessDto>> GetMembersWithLabelAccess(
        long labelId, long organizationId, long? projectId);

    Task<IEnumerable<SensitivityLabelMemberAccessDto>> SetAccessForLabel(
        long currentUserId, long labelId,
        long organizationId, long? projectId,
        GrantLabelAccessDto request);

    Task<bool> RevokeAccessForLabel(
        long labelId, long organizationId, long? projectId,
        long[]? userIds, long[]? groupIds);
}