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

    [JsonPropertyName("llm_server_url")]
    public string? LlmServerUrl { get; set; }

    [JsonPropertyName("llm_model_name")]
    public string? LlmModelName { get; set; }

    [JsonPropertyName("llm_auth_token")]
    public string? LlmAuthToken { get; set; }

    [JsonPropertyName("embedding_server_url")]
    public string? EmbeddingServerUrl { get; set; }

    [JsonPropertyName("embedding_model_name")]
    public string? EmbeddingModelName { get; set; }

    [JsonPropertyName("embedding_auth_token")]
    public string? EmbeddingAuthToken { get; set; }
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