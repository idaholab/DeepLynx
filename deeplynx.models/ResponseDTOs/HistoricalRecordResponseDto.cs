namespace deeplynx.models;

public class HistoricalRecordResponseDto
{
    public long? Id { get; set; }
    public string? Uri { get; set; }
    public string? Properties { get; set; } = null!;
    public string? OriginalId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public long? ClassId { get; set; }
    public string? ClassName { get; set; }
    public long DataSourceId { get; set; }
    public long? RecordId { get; set; }
    public string DataSourceName { get; set; }
    public long? ObjectStorageId { get; set; }
    public string? ObjectStorageName { get; set; }
    public long ProjectId { get; set; }
    public string ProjectName { get; set; }
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string? Tags { get; set; } = null!;
    public List<string>? Labels { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public string? LastUpdatedBy { get; set; }
    public bool IsArchived { get; set; } = false;
}