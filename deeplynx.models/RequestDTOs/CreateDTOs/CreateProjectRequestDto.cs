using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class CreateProjectRequestDto
{
    [Required]
    public string Name { get; set; }
    
    public string? Description { get; set; }

    public string? Abbreviation { get; set; }

    public string? Banner { get; set; }
    
    public bool? RequireSensitivityLabel { get; set; }
}