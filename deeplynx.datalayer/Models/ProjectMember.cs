using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("project_members", Schema = "deeplynx")]
public partial class ProjectMember
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("project_id")]
    public long ProjectId { get; set; }

    [Column("role_id")]
    public long? RoleId { get; set; }

    [Column("group_id")]
    public long? GroupId { get; set; }

    [Column("user_id")]
    public long? UserId { get; set; }

    [Column("is_project_admin")]
    public bool IsProjectAdmin { get; set; } = false;

    [ForeignKey("GroupId")]
    [InverseProperty("ProjectMembers")]
    public virtual Group? Group { get; set; }

    [ForeignKey("ProjectId")]
    [InverseProperty("ProjectMembers")]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey("RoleId")]
    [InverseProperty("ProjectMembers")]
    public virtual Role Role { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("ProjectMembers")]
    public virtual User? User { get; set; }
}
