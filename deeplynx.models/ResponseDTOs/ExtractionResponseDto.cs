using System.Text.Json.Serialization;

namespace deeplynx.models;

public class ExtractionResponseDto
{
    [JsonPropertyName("id")] public long Id { get; set; }

    [JsonPropertyName("properties")] public string? Properties { get; set; }

    [JsonPropertyName("created_by")] public long? CreatedBy { get; set; }

    [JsonPropertyName("class_count")] public int ClassCount { get; set; }

    [JsonPropertyName("relationship_count")] public int RelationshipCount { get; set; }

    [JsonPropertyName("record_count")] public int RecordCount { get; set; }

    [JsonPropertyName("edge_count")] public int EdgeCount { get; set; }
}
