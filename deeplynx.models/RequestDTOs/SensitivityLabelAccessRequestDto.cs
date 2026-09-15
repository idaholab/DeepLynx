namespace deeplynx.models;

public class GrantLabelAccessDto
{
    public long[]? UserIds { get; set; }
    public long[]? GroupIds { get; set; }
    public long[] LabelPermissionIds { get; set; } = Array.Empty<long>();
}