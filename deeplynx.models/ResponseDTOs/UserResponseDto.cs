namespace deeplynx.models;

public class UserResponseDto
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string? Username { get; set; }
    public bool IsSysAdmin { get; set; }
    public bool? IsOrgAdmin { get; set; }
    public bool IsArchived { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLogin { get; set; }
}
