using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("api_keys", Schema = "deeplynx")]
public class ApiKey
{
    [Key] [Column("id")] public long Id { get; set; }

    [Column("user_id")] public long UserId { get; set; }

    [Column("secret")] public string Secret { get; set; }

    [Column("key")] public string Key { get; set; }

    [Column("application_id")] public long? ApplicationId { get; set; }

    [Column("created_by")] public long? CreatedBy { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("ApiKeys")]
    public virtual User User { get; set; } = null!;

    [ForeignKey("ApplicationId")]
    [InverseProperty("ApiKeys")]
    public virtual OauthApplication? OauthApplication { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("CreatedApiKeys")]
    public virtual User? CreatedByUser { get; set; }
}