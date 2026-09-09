using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("oauth_device_authorization_requests", Schema = "deeplynx")]
public partial class OauthDeviceAuthorizationRequest
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("device_code_hash")]
    public string DeviceCodeHash { get; set; } = null!;

    [Column("user_code_hash")]
    public string UserCodeHash { get; set; } = null!;

    [Column("application_id")]
    public long ApplicationId { get; set; }

    [Column("user_id")]
    public long? UserId { get; set; }

    [Column("scope")]
    public string? Scope { get; set; }

    [Column("status")]
    public string Status { get; set; } = OauthDeviceAuthorizationStatus.Pending;

    [Column("polling_interval_seconds")]
    public int PollingIntervalSeconds { get; set; } = 5;

    [Column("poll_count")]
    public int PollCount { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("expires_at", TypeName = "timestamp without time zone")]
    public DateTime ExpiresAt { get; set; }

    [Column("last_polled_at", TypeName = "timestamp without time zone")]
    public DateTime? LastPolledAt { get; set; }

    [Column("approved_at", TypeName = "timestamp without time zone")]
    public DateTime? ApprovedAt { get; set; }

    [Column("denied_at", TypeName = "timestamp without time zone")]
    public DateTime? DeniedAt { get; set; }

    [Column("consumed_at", TypeName = "timestamp without time zone")]
    public DateTime? ConsumedAt { get; set; }

    [ForeignKey("ApplicationId")]
    [InverseProperty("OauthDeviceAuthorizationRequests")]
    public virtual OauthApplication OauthApplication { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("OauthDeviceAuthorizationRequests")]
    public virtual User? User { get; set; }
}

public static class OauthDeviceAuthorizationStatus
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Denied = "denied";
    public const string Expired = "expired";
    public const string Consumed = "consumed";
}
