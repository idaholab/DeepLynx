namespace deeplynx.models;

public class OrganizationResponseDto
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public long? LastUpdatedBy { get; set; }
    public bool IsArchived { get; set; }
    public bool DefaultOrg { get; set; } = false;
    public string? Banner { get; set; }
    public bool? RequireSensitivityLabel { get; set; }
}