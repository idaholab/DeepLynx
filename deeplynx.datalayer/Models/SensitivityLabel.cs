using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("sensitivity_labels", Schema = "deeplynx")]
public partial class SensitivityLabel
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("last_updated_by")]
    public long? LastUpdatedBy { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("project_id")]
    public long? ProjectId { get; set; }

    [Column("organization_id")]
    public long OrganizationId { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    [ForeignKey("OrganizationId")]
    [InverseProperty("SensitivityLabels")]
    public virtual Organization Organization { get; set; } = null!;

    [InverseProperty("Label")]
    public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();

    [ForeignKey("ProjectId")]
    [InverseProperty("SensitivityLabels")]
    public virtual Project? Project { get; set; }

    [ForeignKey("LabelId")]
    [InverseProperty("Labels")]
    public virtual ICollection<Record> Records { get; set; } = new List<Record>();

     [ForeignKey("LabelId")]
    [InverseProperty("Labels")]
    public virtual ICollection<RecordCollection> RecordCollections { get; set; } = new List<RecordCollection>();
    
    [ForeignKey("LabelId")]
    [InverseProperty("Labels")]
    public virtual ICollection<RecordCollection> RecordCollections { get; set; } = new List<RecordCollection>();
    
    [InverseProperty("LastUpdatedSensitivityLabels")]
    public virtual User? LastUpdatedByUser { get; set; }
}
