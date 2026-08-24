using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("oauth_applications", Schema = "deeplynx")]
public partial class OauthApplication
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }
    
    [Column("client_id")]
    public string ClientId { get; set; } = null!;

    [Column("client_secret_hash")]
    public string ClientSecretHash { get; set; }
    
    // used for oauth redirect
    [Column("callback_url")]
    public string CallbackUrl { get; set; } = null!;
    
    // used for any frontend/api redirect for configurable DL ecosystem apps
    [Column("base_url")]
    public string? BaseUrl { get; set; } = null!;
    
    [Column("app_owner_email")]
    public string? AppOwnerEmail { get; set; } = null!;
    
    [Column("is_archived")]
    public bool IsArchived { get; set; }
    
    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("last_updated_by")]
    public long? LastUpdatedBy { get; set; }
    
    [InverseProperty("OauthApplication")]
    public virtual ICollection<OauthToken> OauthTokens { get; set; }

    [InverseProperty("OauthApplication")]
    public virtual ICollection<OauthDeviceAuthorizationRequest> OauthDeviceAuthorizationRequests { get; set; } = new List<OauthDeviceAuthorizationRequest>();

    [InverseProperty("OauthApplication")]
    public virtual ICollection<OauthRefreshToken> OauthRefreshTokens { get; set; } = new List<OauthRefreshToken>();
    
    [InverseProperty("UpdatedOauthApplications")]
    public virtual User? LastUpdatedByUser { get; set; }

    [InverseProperty("OauthApplication")]
    public virtual ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
}
