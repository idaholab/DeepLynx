namespace deeplynx.models;

public class PermissionResponseDto
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string Action { get; set; }
    public string? Resource { get; set; }
    public bool IsDefault { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public long? LastUpdatedBy { get; set; }
    public bool IsArchived { get; set; }
    public long? ProjectId { get; set; }
    public long? OrganizationId { get; set; }
}