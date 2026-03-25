using System.Text.Json.Serialization;

namespace deeplynx.models;

public class InsightQueryRequestDto
{
    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("file_ids")]
    public long[]? FileIds { get; set; }

    [JsonPropertyName("sampling_parameters")]
    public InsightSamplingParametersDto? SamplingParameters { get; set; }
}

public class InsightSamplingParametersDto
{
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }
}