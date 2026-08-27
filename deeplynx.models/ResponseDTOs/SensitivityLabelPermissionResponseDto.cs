namespace deeplynx.models;

public class SensitivityLabelPermissionResponseDto
{
    public long Id { get; set; }
    public long LabelId { get; set; }
    public string Action { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public long? LastUpdatedBy { get; set; }
    public bool IsArchived { get; set; }
}