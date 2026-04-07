using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("record_collections", Schema = "deeplynx")]
public partial class RecordCollection
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("properties", TypeName = "jsonb")]
    public string? Properties { get; set; } = null!;

    [Column("original_id")]
    public string? OriginalId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("project_id")]
    public long ProjectId { get; set; }
    
    [Column("organization_id")]
    public long OrganizationId { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("last_updated_by")]
    public long? LastUpdatedBy { get; set; }

    [Column("description")]
    public string Description { get; set; } = null!;

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    [ForeignKey("ProjectId")]
    [InverseProperty("RecordCollections")]
    public virtual Project Project { get; set; } = null!;
    
    [ForeignKey("OrganizationId")]
    [InverseProperty("RecordCollections")]
    public virtual Organization Organization { get; set; } = null!;

    [ForeignKey("RecordCollectionId")]
    [InverseProperty("RecordCollections")]
    public virtual ICollection<SensitivityLabel> Labels { get; set; } = new List<SensitivityLabel>();

    [ForeignKey("RecordCollectionId")]
    [InverseProperty("RecordCollections")]
    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
    
    [ForeignKey("RecordCollectionId")]
    [InverseProperty("RecordCollections")]
    public virtual ICollection<Record> Records { get; set; } = new List<Record>();
    
    [InverseProperty("LastUpdatedRecordCollections")]
    public virtual User? LastUpdatedByUser { get; set; }
}
