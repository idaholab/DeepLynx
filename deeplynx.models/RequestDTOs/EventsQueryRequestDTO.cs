namespace deeplynx.models;

public class EventsQueryRequestDto
{ 
    public long? LastUpdatedBy { get; set; }
    public string? ProjectName { get; set; }
    public string? Operation { get; set; }
    public string? EntityType { get; set; }
    public string? EntityName { get; set; }
    public string? DataSourceName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 500;
    private const int MaxPageSize = 500;

    public int GetValidatedPageSize()
    {
        if (PageSize <= 0) return 25;
        return PageSize > MaxPageSize ? MaxPageSize : PageSize;
    }
}