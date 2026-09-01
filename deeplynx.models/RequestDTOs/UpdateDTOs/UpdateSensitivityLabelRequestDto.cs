namespace deeplynx.models;

public class UpdateSensitivityLabelRequestDto
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public List<string>? PermissionActions { get; set; }
}