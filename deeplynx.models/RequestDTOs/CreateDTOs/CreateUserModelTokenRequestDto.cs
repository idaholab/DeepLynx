using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class CreateUserModelTokenRequestDto
{
    [Required]
    public string Token { get; set; }
    [Required]
    public long AiModelConfigId { get; set; }
}