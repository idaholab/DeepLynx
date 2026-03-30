using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("user_model_tokens", Schema = "deeplynx")]
public class UserModelToken
{
    [Key] [Column("id")] 
    public long Id { get; set; }
    
    [Column("user_id")]
    public long UserId { get; set; }
    
    [Column("ai_model_config_id")] 
    public long AiModelConfigId { get; set; }
    
    [Column("token")] 
    public string Token { get; set; } = null!;
    
    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }
    
    [ForeignKey("UserId")]
    [InverseProperty("UserModelTokens")]
    public virtual User User { get; set; } = null!;
    
    [ForeignKey("AiModelConfigId")]
    [InverseProperty("UserModelTokens")]
    public virtual AiModelConfig AiModelConfig { get; set; } = null!;
}