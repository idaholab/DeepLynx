using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class UpdateRecordRequestDto
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
    
    [JsonPropertyName("properties")]
    public JsonObject? Properties { get; set; }
    
    [JsonPropertyName("original_id")]
    public string? OriginalId { get; set; }
    
    [JsonPropertyName("name")]
    [MaxLength(50)]
    public string? Name { get; set; }
    
    [JsonPropertyName("class_id")]
    public long? ClassId { get; set; }
    
    [JsonPropertyName("class_name")]
    public string? ClassName { get; set; }
    
    [JsonPropertyName("description")]
    [MaxLength(250)]
    public string? Description { get; set; }
    
    [JsonPropertyName("object_storage_id")]
    public long? ObjectStorageId { get; set; }
    
    [JsonPropertyName("file_type")]
    public string? FileType { get; set; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; set; }
}