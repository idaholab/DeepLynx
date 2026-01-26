using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class UpdateRelationshipRequestDto
{
    [JsonPropertyName("name")] public string? Name { get; set; } = null!;

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("properties")] public JsonObject? Properties { get; set; }

    public string? Uuid { get; set; }

    [JsonPropertyName("origin_id")] public long? OriginId { get; set; }

    [JsonPropertyName("destination_id")] public long? DestinationId { get; set; }
}