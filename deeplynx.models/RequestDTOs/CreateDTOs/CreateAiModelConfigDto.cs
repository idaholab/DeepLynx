using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CreateAiModelConfigDto
{
    [Required] 
    [JsonPropertyName("server_url")] public string ServerUrl { get; set; }
    
    [Required] 
    [JsonPropertyName("model_type")] public string ModelType { get; set; }
    
    [Required] 
    [JsonPropertyName("model_provider")] public string ModelProvider { get; set; }
    
    [Required] 
    [JsonPropertyName("model_name")] public string ModelName { get; set; }
    
    [Required] 
    [JsonPropertyName("requires_token")] public bool RequiresToken { get; set; }
    
    [Required] 
    [JsonPropertyName("default")] public bool Default { get; set; }
}