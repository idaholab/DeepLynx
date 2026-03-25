using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class InsightUploadRequestDto
{
    [Required]
    [JsonPropertyName("file_info")]
    public List<FileInfoDto> FileInfo { get; set; }

    [Required]
    [JsonPropertyName("vlm_server_url")]
    public string VlmServerUrl { get; set; }

    [Required]
    [JsonPropertyName("vlm_name")]
    public string VlmName { get; set; }

    [Required]
    [JsonPropertyName("embedding_server_url")]
    public string EmbeddingServerUrl { get; set; }

    [Required]
    [JsonPropertyName("embedding_model_name")]
    public string EmbeddingModelName { get; set; }

    [JsonPropertyName("vlm_token")]
    public string? VlmToken { get; set; }

    [JsonPropertyName("embedding_model_token")]
    public string? EmbeddingModelToken { get; set; }

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