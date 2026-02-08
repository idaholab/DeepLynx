using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class RecordLabelLinkDto
{
    [JsonPropertyName("record_id")]
    public long RecordId { get; set; } 
    
    [JsonPropertyName("label_id")]
    public long LabelId { get; set; }
}