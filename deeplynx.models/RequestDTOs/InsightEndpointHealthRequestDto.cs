using System.Text.Json.Serialization;

namespace deeplynx.models;

public class InsightEndpointHealthRequestDto
{
    [JsonPropertyName("server_url")]
    public string ServerUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("model_name")]
    public string ModelName { get; set; } = string.Empty;
    
    [JsonPropertyName("auth_token")]
    public string? AuthToken { get; set; }
}