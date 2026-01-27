using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CreateRecordRequestDto
{
    [Required]
    [JsonPropertyName("name")]
    public string Name { get; set; }
        
    [JsonPropertyName("description")]
    [Required]
    public string Description { get; set; }
    
    [JsonPropertyName("object_storage_id")]
    public long? ObjectStorageId { get; set; }
    
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
    
    [Required]
    [JsonPropertyName("properties")]
    public JsonObject Properties { get; set; }
    
    [Required]
    [JsonPropertyName("original_id")]
    public string OriginalId { get; set; }
    
    [JsonPropertyName("class_id")]
    public long? ClassId { get; set; }
    
    [JsonPropertyName("class_name")]
    public string? ClassName { get; set; } 
    
    [JsonPropertyName("file_type")]
    public string? FileType { get; set; }
    
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
    
    [JsonPropertyName("sensitivity_labels")]
    public List<string>? SensitivityLabels { get; set; }
}