using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CreateOrganizationRequestDto
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; }

    [MaxLength(250)]
    public string? Description { get; set; }
    public string? Banner { get; set; }
    public bool? RequireSensitivityLabel { get; set; }
    public bool? CreateContainerPerProject { get; set; } = false;
}