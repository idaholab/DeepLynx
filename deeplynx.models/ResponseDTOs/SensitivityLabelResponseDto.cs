using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.models;

public class SensitivityLabelResponseDto
{
    [Column("id")]
    public long Id { get; set; }
    
    [Column("name")]
    public string Name { get; set; }
    
    [Column("description")]
    public string? Description { get; set; }
    
    [Column("last_updated_at")]
    public DateTime LastUpdatedAt { get; set; }
    
    [Column("last_updated_by")]
    public long? LastUpdatedBy { get; set; }
    
    [Column("is_archived")]
    public bool IsArchived { get; set; }
    
    [Column("project_id")]
    public long? ProjectId { get; set; }
    
    [Column("organization_id")]
    public long? OrganizationId { get; set; }
}