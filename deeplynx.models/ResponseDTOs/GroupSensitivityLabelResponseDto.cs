namespace deeplynx.models;

public class GroupSensitivityLabelResponseDto
{
    public long Id { get; set; }
    public long GroupId { get; set; }
    public string GroupName { get; set; }
    public long LabelId { get; set; }
    public long? GrantedBy { get; set; }
    public string? GrantedByName { get; set; }
    public DateTime GrantedAt { get; set; }
}
