using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

public class AiModelConfigs
{
    [Key] [Column("id")] public long Id { get; set; }
    
    [Column("organization_id")] public long OrganizationId { get; set; }
    
    [Column("project_id")] public long? ProjectId { get; set; }
    
    [Column("server_url")] public string ServerUrl { get; set; }
    
    [Column("model_name")] public string ModelName { get; set; }
    
    [Column("model_type")] public string Model_type { get; set; }
    
    [Column("requires_token")] public bool RequiresToken { get; set; }
    
    [Column("default")] public bool Default { get; set; }
    
    [ForeignKey("ProjectId")]
    [InverseProperty("AIModelConfigs")]
    public virtual Project? Project { get; set; }
    
    [ForeignKey("ProjectId")]
    [InverseProperty("AIModelConfigs")]
    public virtual Project? Project { get; set; }
}