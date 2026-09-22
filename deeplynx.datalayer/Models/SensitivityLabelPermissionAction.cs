using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
 
namespace deeplynx.datalayer.Models;
 
[Table("sensitivity_label_permission_actions", Schema = "deeplynx")]
public partial class SensitivityLabelPermissionAction
{
    [Key]
    [Column("id")]
    public long Id { get; set; }
 
    [Column("name")]
    public string Name { get; set; } = null!;
 
    [Column("description")]
    public string? Description { get; set; }
    
    [InverseProperty("LabelPermission")]
    public virtual ICollection<SensitivityLabelGrant> SensitivityLabelGrants { get; set; } = new List<SensitivityLabelGrant>();
}