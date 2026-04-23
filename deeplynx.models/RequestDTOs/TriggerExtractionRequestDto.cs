using System.Text.Json.Serialization;

namespace deeplynx.models;

public class TriggerExtractionRequestDto
{
    [JsonPropertyName("data_source_id")]
    public long DataSourceId { get; set; }

    /// <summary>"discovery" or "strict"</summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "discovery";
}
