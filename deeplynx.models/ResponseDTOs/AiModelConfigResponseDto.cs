namespace deeplynx.models;

public class AiModelConfigResponseDto
{
    public long Id { get; set; }
    public long OrganizationId { get; set; }
    public long? ProjectId { get; set; }
    public string ServerUrl { get; set; }
    public string ModelProvider { get; set; }
    public string ModelName { get; set; }
    public string ModelType { get; set; }
    public bool RequiresToken { get; set; }
    public bool Default { get; set; }
    public bool IsArchived { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public long? LastUpdatedBy { get; set; }
    public string? Token { get; set; }
}