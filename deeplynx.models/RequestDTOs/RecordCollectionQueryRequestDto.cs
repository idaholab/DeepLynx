namespace deeplynx.models;

public class RecordCollectionQueryRequestDto
{
    public string? Search { get; set; }
    public long[]? SensitivityLabelIds { get; set; }
    public long[]? TagIds { get; set; }
    public string? Sort { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    private const int MaxPageSize = 500;

    public int GetValidatedPageSize()
    {
        if (PageSize <= 0) return 25;
        return PageSize > MaxPageSize ? MaxPageSize : PageSize;
    }
}