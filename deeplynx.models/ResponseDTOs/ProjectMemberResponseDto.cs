namespace deeplynx.models;

public class ProjectMemberResponseDto
{
    public string Name { get; set; }
    public long? MemberId { get; set; } 
    public string Email { get; set; }
    public string? Role { get; set; }
    public long? RoleId { get; set; }
    public bool IsProjectAdmin { get; set; }
}