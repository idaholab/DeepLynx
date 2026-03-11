using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class UpdateUserModelTokenRequestDto
{
    [Required]
    public string Token { get; set; }
}