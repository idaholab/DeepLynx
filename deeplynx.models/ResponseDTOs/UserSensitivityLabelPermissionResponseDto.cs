public class UserSensitivityLabelPermissionResponseDto
{
    public long Id { get; set; }
    public long LabelId { get; set; }
    public long UserId { get; set; }
    public long? LabelPermissionId { get; set; }
    public string? LabelPermissionName { get; set; }
    public string? LabelPermissionDescription { get; set; }
    public DateTime GrantedAt { get; set; }
    public long? GrantedBy { get; set; }
}