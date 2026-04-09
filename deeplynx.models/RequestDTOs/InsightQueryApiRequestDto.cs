using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class InsightQueryApiRequestDto
{
    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("file_ids")]
    public long[]? FileIds { get; set; }

    [JsonPropertyName("sampling_parameters")]
    public InsightSamplingParametersDto? SamplingParameters { get; set; }
}