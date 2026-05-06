using System.Text.Json.Serialization;

namespace deeplynx.models;

public class EmbeddingStatusResponseDto
{
    [JsonPropertyName("ontology_ready")]
    public bool OntologyReady { get; set; }

    [JsonPropertyName("class_count")]
    public int ClassCount { get; set; }

    [JsonPropertyName("embedded_class_count")]
    public int EmbeddedClassCount { get; set; }

    [JsonPropertyName("relationship_count")]
    public int RelationshipCount { get; set; }

    [JsonPropertyName("embedded_relationship_count")]
    public int EmbeddedRelationshipCount { get; set; }
}
