using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace deeplynx.models;
public class OntologySimilarityResultDto
{
    [Column("name")]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [Column("class_or_relationship_id")]
    [JsonPropertyName("class_or_relationship_id")]
    public long? ClassRelationshipId { get; set; }

    [Column("type")]
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [Column("description")]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [Column("score")]
    [JsonPropertyName("score")]
    public double Score { get; set; }

    [Column("text_chunk")]
    [JsonPropertyName("text_chunk")]
    public string? TextChunk { get; set; }

    [Column("origin_class")]
    [JsonIgnore]
    public string? OriginClass { get; set; }

    [Column("destination_class")]
    [JsonIgnore]
    public string? DestinationClass { get; set; }

    [NotMapped]
    [JsonPropertyName("relationship_pattern")]
    public RelationshipPattern? RelationshipPattern => Type == "relationship" ? new RelationshipPattern
    {
        OriginClassName      = OriginClass,
        RelationshipName     = Name,
        DestinationClassName = DestinationClass
    } : null;
}

public class RelationshipPattern
{
    [JsonPropertyName("origin_class_name")]
    public string? OriginClassName { get; set; }

    [JsonPropertyName("relationship_name")]
    public string? RelationshipName { get; set; }

    [JsonPropertyName("destination_class_name")]
    public string? DestinationClassName { get; set; }
}