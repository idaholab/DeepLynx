using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class CreateOauthApplicationRequestDto
{
    [Required] [MaxLength(50)] public string Name { get; set; }

    [MaxLength(250)] public string? Description { get; set; }

    // used for oauth redirect
    [Required] public string CallbackUrl { get; set; }

    // used for any frontend/api redirect for configurable DL ecosystem apps
    public string? BaseUrl { get; set; }

    public string? AppOwnerEmail { get; set; }
}