using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("oauth_refresh_tokens", Schema = "deeplynx")]
public partial class OauthRefreshToken
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("token_hash")]
    public string TokenHash { get; set; } = null!;

    [Column("application_id")]
    public long ApplicationId { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("scope")]
    public string? Scope { get; set; }

    [Column("expires_at", TypeName = "timestamp without time zone")]
    public DateTime ExpiresAt { get; set; }

    [Column("revoked")]
    public bool Revoked { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("last_used_at", TypeName = "timestamp without time zone")]
    public DateTime? LastUsedAt { get; set; }

    [Column("revoked_at", TypeName = "timestamp without time zone")]
    public DateTime? RevokedAt { get; set; }

    [ForeignKey("ApplicationId")]
    [InverseProperty("OauthRefreshTokens")]
    public virtual OauthApplication OauthApplication { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("OauthRefreshTokens")]
    public virtual User User { get; set; } = null!;
}
