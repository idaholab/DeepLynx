namespace deeplynx.models;

public class UserModelTokenResponseDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long AiModelConfigId { get; set; }
    public string Token { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}