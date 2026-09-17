using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("sensitivity_label_grants", Schema = "deeplynx")]
public partial class SensitivityLabelGrant
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public long? UserId { get; set; }

    [Column("group_id")]
    public long? GroupId { get; set; }

    [Column("label_id")]
    public long LabelId { get; set; }
    
    [Column("label_permission_id")]
    public long? LabelPermissionId { get; set; }

    [Column("granted_by")]
    public long? GrantedBy { get; set; }

    [Column("granted_at", TypeName = "timestamp without time zone")]
    public DateTime GrantedAt { get; set; }
    
    [ForeignKey("UserId")]
    [InverseProperty("SensitivityLabelGrants")]
    public virtual User? User { get; set; } = null!;

    [ForeignKey("GroupId")]
    [InverseProperty("SensitivityLabelGrants")]
    public virtual Group? Group { get; set; } = null!;

    [ForeignKey("LabelId")]
    [InverseProperty("SensitivityLabelGrants")]
    public virtual SensitivityLabel Label { get; set; } = null!;
    
    [ForeignKey("LabelPermissionId")]
    [InverseProperty("SensitivityLabelGrants")]
    public virtual SensitivityLabelPermissionAction LabelPermission { get; set; } = null!;

    [ForeignKey("GrantedBy")]
    [InverseProperty("GrantedSensitivityLabelGrants")]
    public virtual User? GrantedByUser { get; set; }
}