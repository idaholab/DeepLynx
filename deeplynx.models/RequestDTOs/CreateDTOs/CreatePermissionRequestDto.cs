using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using deeplynx.models.Converters;

namespace deeplynx.models;

// Only allow users to create label-based permissions
public class CreatePermissionRequestDto
{
    [Required]
    public string Name { get; set; }
    public string? Description { get; set; }
    [Required]
    public string Action { get; set; }
    [JsonConverter(typeof(NullableLongJsonConverter))]
    public long? LabelId { get; set; }
}