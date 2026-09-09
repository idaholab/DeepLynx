using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("group_sensitivity_labels", Schema = "deeplynx")]
public partial class GroupSensitivityLabel
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("group_id")]
    public long GroupId { get; set; }

    [Column("label_id")]
    public long LabelId { get; set; }

    [Column("granted_by")]
    public long? GrantedBy { get; set; }

    [Column("granted_at", TypeName = "timestamp without time zone")]
    public DateTime GrantedAt { get; set; }

    [ForeignKey("GroupId")]
    [InverseProperty("GroupSensitivityLabels")]
    public virtual Group Group { get; set; } = null!;

    [ForeignKey("LabelId")]
    [InverseProperty("GroupSensitivityLabels")]
    public virtual SensitivityLabel Label { get; set; } = null!;

    [ForeignKey("GrantedBy")]
    [InverseProperty("GrantedGroupSensitivityLabels")]
    public virtual User? GrantedByUser { get; set; }
}
