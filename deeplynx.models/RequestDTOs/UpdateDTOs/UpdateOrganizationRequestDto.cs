using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class UpdateOrganizationRequestDto
{
    [MaxLength(50)] public string? Name { get; set; }

    [MaxLength(250)] public string? Description { get; set; }

    public bool? DefaultOrg { get; set; }
    
    public string? Banner { get; set; }
    
    public bool? RequireSensitivityLabel {get; set;}
}