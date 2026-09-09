using deeplynx.models;

namespace deeplynx.interfaces;

public interface IGroupSensitivityLabelBusiness
{
    Task<IEnumerable<GroupSensitivityLabelResponseDto>> GetGroupsWithAccessToLabel(long labelId, long organizationId, long? projectId);
    Task<GroupSensitivityLabelResponseDto> GrantLabelAccessToGroup(long currentUserId, long labelId, long groupId, long organizationId, long? projectId);
    Task<bool> RevokeLabelAccessFromGroup(long labelId, long groupId, long organizationId, long? projectId);
    Task<bool> SetGroupsForLabel(long currentUserId, long labelId, long[] groupIds, long organizationId, long? projectId);
}
