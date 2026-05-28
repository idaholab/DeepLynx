using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CreateRecordCollectionRequestDto
{
    [Required]
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    [Required]
    public string Description { get; set; }

    [Required]
    [JsonPropertyName("properties")]
    public JsonObject Properties { get; set; }


    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}