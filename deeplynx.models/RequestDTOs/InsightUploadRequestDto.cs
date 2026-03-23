using System.Text.Json.Serialization;

namespace deeplynx.models;

public class InsightUploadRequestDto
{
    [JsonPropertyName("file_info")]
    public List<InsightUploadFileInfoBody> FileInfo { get; set; } = [];

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

public class InsightUploadFileInfoBody
{
    [JsonPropertyName("fileId")]
    public long FileId { get; set; }

    [JsonPropertyName("fileURI")]
    public string FileUri { get; set; } = string.Empty;
}