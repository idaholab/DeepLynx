namespace deeplynx.models;

public class UserActivityCountsDto
{
    public int ActiveLast24Hours { get; set; }
    public int ActiveLast7Days { get; set; }
    public int ActiveLast30Days { get; set; }
    public DateTime GeneratedAt { get; set; }
}
