using System.Text.Json.Serialization;

namespace deeplynx.models;

public class InsightSamplingParametersDto
{
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }
}