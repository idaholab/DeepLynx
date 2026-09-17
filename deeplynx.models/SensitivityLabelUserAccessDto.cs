namespace deeplynx.models;

public class SensitivityLabelUserAccessDto
{
    public long UserId { get; set; }
    public long LabelId { get; set; }

    /// <summary>Permission actions and whether the user has each one, from any source (individual or group).</summary>
    public List<SensitivityLabelPermissionFlagDto> TotalPermissions { get; set; } = new();

    /// <summary>Permission actions and whether the user has each one individually.</summary>
    public List<SensitivityLabelPermissionFlagDto> UserPermissions { get; set; } = new();

    /// <summary>Per-group breakdown: each group the user belongs to, with its own permission flags.</summary>
    public List<SensitivityLabelGroupPermissionFlagsDto> GroupPermissions { get; set; } = new();
}

public class SensitivityLabelPermissionFlagDto
{
    public long PermissionId { get; set; }
    public string PermissionName { get; set; } = null!;
    public bool HasPermission { get; set; }
}

public class SensitivityLabelGroupPermissionFlagsDto
{
    public long GroupId { get; set; }
    public string GroupName { get; set; } = null!;
    public List<SensitivityLabelPermissionFlagDto> Permissions { get; set; } = new();
}