using System.Text.Json.Serialization;

namespace deeplynx.models;

public class ScrapeObjectStorageResponseDto
{
    [JsonPropertyName("processed")]
    public long Processed { get; set; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}