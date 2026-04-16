using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class OntologySimilarityResultDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [Column("technical_id")]
    [JsonPropertyName("technical_id")]
    public long? TechnicalId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [Column("text_chunk")]
    [JsonPropertyName("text_chunk")]
    public string? TextChunk { get; set; }
}
