namespace deeplynx.models;

public class TimeseriesMigrationRecordDto
{
    public long RecordId { get; set; }
    public string Uri { get; set; } = null!;
    public long OrganizationId { get; set; }
    public long ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public long DataSourceId { get; set; }
}
