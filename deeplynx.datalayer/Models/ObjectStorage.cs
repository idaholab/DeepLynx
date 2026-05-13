using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("object_storages", Schema = "deeplynx")]
public partial class ObjectStorage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("type")]
    public string Type { get; set; } = null!;
    
    [Column("config_encrypted")]
    public string ConfigEncrypted { get; set; } = null!;

    [Column("project_id")]
    public long? ProjectId { get; set; }
    
    [Column("organization_id")]
    public long OrganizationId { get; set; }

    [Column("default")]
    public bool Default { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("last_updated_by")]
    public long? LastUpdatedBy { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    [ForeignKey("ProjectId")]
    [InverseProperty("ObjectStorages")]
    public virtual Project? Project { get; set; }

    [ForeignKey("OrganizationId")]
    [InverseProperty("ObjectStorages")]
    public virtual Organization Organization { get; set; } = null!;

    [InverseProperty("ObjectStorage")]
    public virtual ICollection<Record> Records { get; set; } = new List<Record>();
    
    [InverseProperty("LastUpdatedObjectStorages")]
    public virtual User? LastUpdatedByUser { get; set; }
}
