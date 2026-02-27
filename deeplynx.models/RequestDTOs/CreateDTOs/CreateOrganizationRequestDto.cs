using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CreateOrganizationRequestDto
{
    [Required]
    public string Name { get; set; }

    public string? Description { get; set; }
    
    public string? Banner  { get; set; }
    
    public bool? RequireSensitivityLabel { get; set; }
}