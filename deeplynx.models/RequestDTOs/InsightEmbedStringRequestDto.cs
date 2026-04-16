using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using deeplynx.models;

public class InsightEmbedStringRequestDto
{
    [Required]
    [JsonPropertyName("strings")]
    public List<EmbedStringDto> EmbedStringInfo { get; set; }

    [JsonPropertyName("embedding_server_url")]
    public string? EmbeddingServerUrl { get; set; }

    [JsonPropertyName("embedding_model_name")]
    public string? EmbeddingModelName { get; set; }
    

    [JsonPropertyName("embedding_auth_token")]
    public string? EmbeddingModelToken { get; set; }
    
    public class EmbedStringDto
    {
        [JsonPropertyName("class_id")]
        public long? ClassId { get; set; }

        [JsonPropertyName("relationship_id")]
        public long? RelationshipId { get; set; }
        
        [JsonPropertyName("text")]
        [Required]
        public string Text { get; set; } = string.Empty;
    }
}