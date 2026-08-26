namespace deeplynx.models;

public class UserSensitivityLabelResponseDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; }
    public string UserEmail { get; set; }
    public long LabelId { get; set; }
    public long? GrantedBy { get; set; }
    public string? GrantedByName { get; set; }
    public DateTime GrantedAt { get; set; }
}
