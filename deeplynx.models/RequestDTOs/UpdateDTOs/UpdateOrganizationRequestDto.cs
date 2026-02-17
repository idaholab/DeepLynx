namespace deeplynx.models;

public class UpdateOrganizationRequestDto
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public bool? DefaultOrg { get; set; }
    
    public string? Banner { get; set; }
    
    public bool? RequireSensitivityLabel {get; set;}
}