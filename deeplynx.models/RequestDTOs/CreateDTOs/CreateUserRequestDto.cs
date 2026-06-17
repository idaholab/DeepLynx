namespace deeplynx.models;

public class CreateUserRequestDto
{
    public string Name { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public bool? IsArchived { get; set; } = false;
    public bool? IsActive { get; set; } = false;

    /// <summary>Whether to create a service account. Defaults to a human account when not specified.</summary>
    public bool? IsServiceAccount { get; set; } = false;
}
