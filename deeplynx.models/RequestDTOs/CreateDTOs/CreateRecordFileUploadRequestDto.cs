using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace deeplynx.models;

public class CreateRecordFileUploadRequestDto
{
    [MaxLength(100)]
    [Required] public string Name { get; set; }

    [Required] public string Description { get; set; }

    [Required] public JsonObject Properties { get; set; }

    [Required] public string OriginalId { get; set; }

    public long? ClassId { get; set; }

    public string? ClassName { get; set; }
}