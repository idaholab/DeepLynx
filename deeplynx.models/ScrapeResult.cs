namespace deeplynx.models;

public class ScrapeResult
{
    public List<CreateRecordRequestDto> Records { get; set; } = new();
    public string? NextCursor { get; set; }
}