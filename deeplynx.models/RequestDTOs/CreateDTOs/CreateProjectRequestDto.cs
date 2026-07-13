using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class CreateProjectRequestDto
{
    [Required]
    [MaxLength(50)] public string Name { get; set; }
    
    [MaxLength(250)] public string? Description { get; set; }

    public string? Abbreviation { get; set; }

    public string? Banner { get; set; }
    
    public bool? RequireSensitivityLabel { get; set; }
}