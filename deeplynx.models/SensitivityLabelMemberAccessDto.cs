namespace deeplynx.models;
public class SensitivityLabelMemberAccessDto
{
    public long? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }

    public long? GroupId { get; set; }
    public string? GroupName { get; set; }
    public List<GroupMemberDto>? GroupMembers { get; set; }

    public List<LabelPermissionGrantDto> Permissions { get; set; } = new();
}

public class LabelPermissionGrantDto
{
    public long GrantId { get; set; }
    public long? LabelPermissionId { get; set; }
    public string? LabelPermissionName { get; set; }
    public string? LabelPermissionDescription { get; set; }
    public DateTime GrantedAt { get; set; }
    public long? GrantedBy { get; set; }
}

public class GroupMemberDto
{
    public long UserId { get; set; }
    public string UserName { get; set; } = null!;
    public string UserEmail { get; set; } = null!;
}