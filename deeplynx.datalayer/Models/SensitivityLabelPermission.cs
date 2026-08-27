using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("sensitivity_label_permissions", Schema = "deeplynx")]
public partial class SensitivityLabelPermission
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("label_id")]
    public long LabelId { get; set; }

    [Column("action")]
    public string Action { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("last_updated_by")]
    public long? LastUpdatedBy { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    [ForeignKey("LabelId")]
    [InverseProperty("SensitivityLabelPermissions")]
    public virtual SensitivityLabel Label { get; set; } = null!;

    [InverseProperty("LastUpdatedSensitivityLabelPermissions")]
    public virtual User? LastUpdatedByUser { get; set; }
}