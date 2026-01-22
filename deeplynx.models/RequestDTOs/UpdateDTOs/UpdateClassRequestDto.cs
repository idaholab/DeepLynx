using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class UpdateClassRequestDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("properties")] public JsonObject? Properties { get; set; }

    [JsonPropertyName("uuid")] public string? Uuid { get; set; }
}