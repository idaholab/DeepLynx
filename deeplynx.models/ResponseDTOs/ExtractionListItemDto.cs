using System.Text.Json.Serialization;

namespace deeplynx.models;

public class ExtractionListItemDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = null!;
    [JsonPropertyName("mode")] public string? Mode { get; set; }
    [JsonPropertyName("created_by")] public long? CreatedBy { get; set; }
    
    [JsonPropertyName("project_id")] public long? ProjectId { get; set; }
}
