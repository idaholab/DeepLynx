namespace deeplynx.models;

public class ObjectStorageResponseDto
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public long? ProjectId { get; set; }
    public long? OrganizationId { get; set; }
    public bool Default { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public long? LastUpdatedBy { get; set; }
    public bool IsArchived { get; set; } = false;
    public string? AzureFilePath { get; set; } = null;
}