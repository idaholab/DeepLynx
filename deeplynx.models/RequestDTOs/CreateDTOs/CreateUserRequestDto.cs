using System.ComponentModel.DataAnnotations;
namespace deeplynx.models;

public class CreateUserRequestDto
{
    [Required]
    public string Name { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public bool? IsArchived { get; set; } = false;
    public bool? IsActive { get; set; } = false;
}