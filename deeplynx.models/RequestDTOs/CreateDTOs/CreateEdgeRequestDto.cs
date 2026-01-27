using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CreateEdgeRequestDto
{
    [JsonPropertyName("origin_id")] public long? OriginId { get; set; }

    [JsonPropertyName("properties")] public JsonObject? Properties { get; set; }

    [JsonPropertyName("destination_id")] public long? DestinationId { get; set; }

    [JsonPropertyName("relationship_id")] public long? RelationshipId { get; set; }

    [JsonPropertyName("relationship_name")]
    public string? RelationshipName { get; set; }

    [JsonPropertyName("origin_oid")] public string? OriginOid { get; set; }

    [JsonPropertyName("destination_oid")] public string? DestinationOid { get; set; }
}