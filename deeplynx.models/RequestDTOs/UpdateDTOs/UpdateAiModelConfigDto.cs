using System.Text.Json.Serialization;

namespace deeplynx.models;

public class UpdateAiModelConfigDto
{
    [JsonPropertyName("model_name")] public string? ModelName { get; set; }
    
    [JsonPropertyName("model_type")] public string? ModelType { get; set; }
    
    [JsonPropertyName("server_url")] public string? ServerUrl { get; set; }
    
    [JsonPropertyName("requires_token")] public bool? RequiresToken { get; set; }
    
    [JsonPropertyName("default")] public bool? Default { get; set; }
    
}