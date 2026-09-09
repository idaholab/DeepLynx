using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("groups", Schema = "deeplynx")]
public partial class Group
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

    [Column("organization_id")]
    public long OrganizationId { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    [ForeignKey("OrganizationId")]
    [InverseProperty("Groups")]
    public virtual Organization Organization { get; set; } = null!;

    [InverseProperty("Group")]
    public virtual ICollection<ProjectMember> ProjectMembers { get; set; } = new List<ProjectMember>();

    [ForeignKey("GroupId")]
    [InverseProperty("Groups")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
    
    [InverseProperty("LastUpdatedGroups")]
    public virtual User? LastUpdatedByUser { get; set; }

    [InverseProperty("Group")]
    public virtual ICollection<GroupSensitivityLabel> GroupSensitivityLabels { get; set; } = new List<GroupSensitivityLabel>();
}
