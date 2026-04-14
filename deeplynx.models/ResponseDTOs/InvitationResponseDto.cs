namespace deeplynx.models;

public class InvitationResponseDto
{
    public string[]? EmailsFailed { get; set; }
    public long[]? ExistingUsersFailed { get; set; }
    public long[]? GroupsFailed { get; set; }
}