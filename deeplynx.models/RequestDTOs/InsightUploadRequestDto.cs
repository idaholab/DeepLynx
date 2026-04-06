using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class InsightUploadRequestDto
{
    [Required]
    [JsonPropertyName("file_info")]
    public List<FileInfoDto> FileInfo { get; set; }

    [Required]
    [JsonPropertyName("llm_server_url")]
    public string VlmServerUrl { get; set; }

    [Required]
    [JsonPropertyName("llm_model_name")]
    public string VlmName { get; set; }

    [Required]
    [JsonPropertyName("embedding_server_url")]
    public string EmbeddingServerUrl { get; set; }

    [Required]
    [JsonPropertyName("embedding_model_name")]
    public string EmbeddingModelName { get; set; }

    [JsonPropertyName("llm_auth_token")]
    public string? VlmToken { get; set; }

    [JsonPropertyName("embedding_auth_token")]
    public string? EmbeddingModelToken { get; set; }

    [JsonPropertyName("user_jwt")]
    public string? UserJwt { get; set; }

    [JsonPropertyName("overwrite")]
    public bool? Overwrite { get; set; }

    public class FileInfoDto
    {
        [Required]
        [JsonPropertyName("fileId")]
        public long FileId { get; set; }

        [Required]
        [JsonPropertyName("fileURI")]
        public string FileUri { get; set; }
    }
}