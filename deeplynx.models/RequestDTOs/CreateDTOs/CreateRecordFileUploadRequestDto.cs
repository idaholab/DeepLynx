using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CreateRecordFileUploadRequestDto
{
    [Required]
    public string Name { get; set; }
    
    [Required]
    public string Description { get; set; }
    
    [Required]
    public JsonObject Properties { get; set; }
    
    [Required]
    public string OriginalId { get; set; }
    
    public long? ClassId { get; set; }
    
    public string? ClassName { get; set; } 
}