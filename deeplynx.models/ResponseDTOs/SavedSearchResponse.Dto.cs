using System.Text.Json.Serialization;

namespace deeplynx.models;

public class SavedSearchResponseDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("lastUpdatedAt")]
    public DateTime LastUpdatedAt { get; set; }

    [JsonPropertyName("query")]
    public CustomQueryDtos.CustomQueryResponseDto Query { get; set; }
}