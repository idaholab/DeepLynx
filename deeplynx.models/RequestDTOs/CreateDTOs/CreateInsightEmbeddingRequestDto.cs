using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class CreateInsightEmbeddingRequestDto
{
    [Required]
    public List<FileInfoDto> FileInfo { get; set; }

    [Required]
    public string LanguageModelServerUrl { get; set; }

    [Required]
    public string LanguageModelName { get; set; }

    [Required]
    public string EmbeddingServerUrl { get; set; }

    [Required]
    public string EmbeddingModelName { get; set; }

    public string? LanguageModelToken { get; set; }
    public string? EmbeddingModelToken { get; set; }
    public bool? Overwrite { get; set; }

    public class FileInfoDto
    {
        [Required]
        [JsonPropertyName("fileId")]
        public long FileId { get; set; }

        [Required]
        [JsonPropertyName("fileUri")]
        public string FileUri { get; set; }
    }
}