using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class CreatePermissionRequestDto
{
    [Required]
    public string Name { get; set; }
    public string? Description { get; set; }
    [Required]
    public string Action { get; set; }
}