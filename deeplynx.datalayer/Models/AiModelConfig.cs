using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("ai_model_configs", Schema = "deeplynx")]
public class AiModelConfig
{
    [Key] [Column("id")] 
    public long Id { get; set; }
    
    [Column("organization_id")] 
    public long OrganizationId { get; set; }
    
    [Column("project_id")] 
    public long? ProjectId { get; set; }
    
    [Column("server_url")] 
    public string ServerUrl { get; set; } = null!;
    
    [Column("model_name")] 
    public string ModelName { get; set; } = null!;
    
    [Column("model_type")] 
    public string ModelType { get; set; } = null!;
    
    [Column("requires_token")] 
    public bool RequiresToken { get; set; }
    
    [Column("default")] 
    public bool Default { get; set; }
    
    [Column("is_archived")] 
    public bool IsArchived { get; set; }
    
    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }
    
    [Column("last_updated_by")]
    public long? LastUpdatedBy { get; set; }
    
    [ForeignKey("OrganizationId")]
    [InverseProperty("AiModelConfigs")] 
    public virtual Organization Organization { get; set; } = null!;
    
    [ForeignKey("ProjectId")]
    [InverseProperty("AiModelConfigs")]
    public virtual Project? Project { get; set; }
    
    [InverseProperty("LastUpdatedAiModelConfigs")]
    public virtual User? LastUpdatedByUser { get; set; }
    
    [InverseProperty("AiModelConfig")]
    public virtual ICollection<UserModelToken> UserModelTokens { get; set; } = new List<UserModelToken>();
}