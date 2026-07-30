namespace deeplynx.models;

public class ProjectResponseDto
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Abbreviation { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public long? LastUpdatedBy { get; set; }
    public bool IsArchived { get; set; } = false;
    public long? OrganizationId { get; set; }
    public string? Banner { get; set; }
    public bool? RequireSensitivityLabel { get; set; }
    public ObjectStorageResponseDto? AssociatedObjectStorage { get; set; }
}