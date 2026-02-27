using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("records", Schema = "deeplynx")]
public partial class Record
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("uri")]
    public string? Uri { get; set; }

    [Column("properties", TypeName = "jsonb")]
    public string Properties { get; set; } = null!;

    [Column("original_id")]
    public string OriginalId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("class_id")]
    public long? ClassId { get; set; }

    [Column("data_source_id")]
    public long DataSourceId { get; set; }

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

    [Column("object_storage_id")]
    public long? ObjectStorageId { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }
    
    [Column("file_type")]
    public string? FileType { get; set; }
    
    [ForeignKey("ClassId")]
    [InverseProperty("Records")]
    public virtual Class? Class { get; set; }

    [ForeignKey("DataSourceId")]
    [InverseProperty("Records")]
    public virtual DataSource DataSource { get; set; } = null!;

    [InverseProperty("Destination")]
    public virtual ICollection<Edge> EdgeDestinations { get; set; } = new List<Edge>();

    [InverseProperty("Origin")]
    public virtual ICollection<Edge> EdgeOrigins { get; set; } = new List<Edge>();

    [InverseProperty("Record")]
    public virtual ICollection<HistoricalRecord> HistoricalRecords { get; set; } = new List<HistoricalRecord>();

    [ForeignKey("ObjectStorageId")]
    [InverseProperty("Records")]
    public virtual ObjectStorage? ObjectStorage { get; set; }

    [ForeignKey("ProjectId")]
    [InverseProperty("Records")]
    public virtual Project Project { get; set; } = null!;
    
    [ForeignKey("OrganizationId")]
    [InverseProperty("Records")]
    public virtual Organization Organization { get; set; } = null!;

    [ForeignKey("RecordId")]
    [InverseProperty("Records")]
    public virtual ICollection<SensitivityLabel> Labels { get; set; } = new List<SensitivityLabel>();

    [ForeignKey("RecordId")]
    [InverseProperty("Records")]
    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
    
    [InverseProperty("LastUpdatedRecords")]
    public virtual User? LastUpdatedByUser { get; set; }
}
