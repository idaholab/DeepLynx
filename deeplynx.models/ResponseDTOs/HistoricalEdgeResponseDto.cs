namespace deeplynx.models;

public class HistoricalEdgeResponseDto
{
    public long? Id { get; set; }
    public long OriginId { get; set; }
    public long DestinationId { get; set; }
    public long? RelationshipId { get; set; }
    public string? RelationshipName { get; set; }
    public long DataSourceId { get; set; }
    public string DataSourceName { get; set; } = string.Empty;
    public long ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateTime LastUpdatedAt { get; set; }
    public long? LastUpdatedBy { get; set; }
    public bool IsArchived { get; set; } = false;
}
