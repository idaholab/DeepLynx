using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("user_sensitivity_labels", Schema = "deeplynx")]
public partial class UserSensitivityLabel
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("label_id")]
    public long LabelId { get; set; }

    [Column("granted_by")]
    public long? GrantedBy { get; set; }

    [Column("granted_at", TypeName = "timestamp without time zone")]
    public DateTime GrantedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UserSensitivityLabels")]
    public virtual User User { get; set; } = null!;

    [ForeignKey("LabelId")]
    [InverseProperty("UserSensitivityLabels")]
    public virtual SensitivityLabel Label { get; set; } = null!;

    [ForeignKey("GrantedBy")]
    [InverseProperty("GrantedUserSensitivityLabels")]
    public virtual User? GrantedByUser { get; set; }
}