using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("permissions", Schema = "deeplynx")]
public class Permission
{
    [Key] [Column("id")] public long Id { get; set; }

    [Column("name")] public string Name { get; set; } = null!;

    [Column("description")] public string? Description { get; set; }

    [Column("action")] public string Action { get; set; } = null!;

    [Column("resource")] public string? Resource { get; set; }

    [Column("is_default")] public bool IsDefault { get; set; } = false;

    [Column("project_id")] public long? ProjectId { get; set; }

    [Column("organization_id")] public long? OrganizationId { get; set; }

    [Column("last_updated_by")] public long? LastUpdatedBy { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("is_archived")] public bool IsArchived { get; set; }

    [ForeignKey("ProjectId")]
    [InverseProperty("Permissions")]
    public virtual Project? Project { get; set; }

    [ForeignKey("OrganizationId")]
    [InverseProperty("Permissions")]
    public virtual Organization? Organization { get; set; }

    [ForeignKey("PermissionId")]
    [InverseProperty("Permissions")]
    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    [InverseProperty("LastUpdatedPermissions")]
    public virtual User? LastUpdatedByUser { get; set; }
}