using System.Text.Json.Serialization;

namespace deeplynx.models;

public class InsightEndpointHealthResponseDto
{
    [JsonPropertyName("reachable")]
    public bool Reachable { get; set; }
    
    [JsonPropertyName("model_available")]
    public bool ModelAvailable { get; set; }
    
    [JsonPropertyName("latency_ms")]
    public double? LatencyMs { get; set; }
    
    [JsonPropertyName("model_metadata")]
    public Dictionary<string, object>? ModelMetadata { get; set; }
    
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}