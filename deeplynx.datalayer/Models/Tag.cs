using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("tags", Schema = "deeplynx")]
public partial class Tag
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("project_id")]
    public long? ProjectId { get; set; }

    [Column("organization_id")]
    public long OrganizationId { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("last_updated_by")]
    public long? LastUpdatedBy { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    [ForeignKey("ProjectId")]
    [InverseProperty("Tags")]
    public virtual Project? Project { get; set; }

    [ForeignKey("OrganizationId")]
    [InverseProperty("Tags")]
    public virtual Organization Organization { get; set; } = null!;

    [ForeignKey("TagId")]
    [InverseProperty("Tags")]
    public virtual ICollection<Record> Records { get; set; } = new List<Record>();
    
    [ForeignKey("TagId")]
    [InverseProperty("Tags")]
    public virtual ICollection<RecordCollection> RecordCollections { get; set; } = new List<RecordCollection>();

    [InverseProperty("LastUpdatedTags")]
    public virtual User? LastUpdatedByUser { get; set; }
}
