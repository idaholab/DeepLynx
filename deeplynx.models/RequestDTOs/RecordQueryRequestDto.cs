namespace deeplynx.models;

public class RecordQueryRequestDto
{
    public string? Name { get; set; }
    public long? DataSourceId { get; set; }
    public string? FileType { get; set; }
    public long? ClassId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    private const int MaxPageSize = 500;

    public int GetValidatedPageSize()
    {
        if (PageSize <= 0) return 25;
        return PageSize > MaxPageSize ? MaxPageSize : PageSize;
    }
}