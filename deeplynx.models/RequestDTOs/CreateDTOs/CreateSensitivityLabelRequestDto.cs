using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CreateSensitivityLabelRequestDto
{
    [Required]
    public string Name { get; set; }

    public string? Description { get; set; }

    public List<string>? PermissionActions { get; set; }
}