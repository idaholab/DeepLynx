using System.ComponentModel.DataAnnotations;
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

    [Required]
    [JsonPropertyName("llm_server_url")]
    public string LlmServerUrl { get; set; }

    [Required]
    [JsonPropertyName("llm_model_name")]
    public string LlmName { get; set; }

    [Required]
    [JsonPropertyName("embedding_server_url")]
    public string EmbeddingServerUrl { get; set; }

    [Required]
    [JsonPropertyName("embedding_model_name")]
    public string EmbeddingModelName { get; set; }

    [JsonPropertyName("llm_auth_token")]
    public string? LlmToken { get; set; }

    [JsonPropertyName("embedding_auth_token")]
    public string? EmbeddingModelToken { get; set; }
}