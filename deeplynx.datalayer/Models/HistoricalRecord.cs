using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("historical_records", Schema = "deeplynx")]
public partial class HistoricalRecord
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("record_id")]
    public long RecordId { get; set; }

    [Column("uri")]
    public string? Uri { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("properties", TypeName = "jsonb")]
    public string Properties { get; set; } = null!;

    [Column("original_id")]
    public string? OriginalId { get; set; }

    [Column("class_id")]
    public long? ClassId { get; set; }

    [Column("class_name")]
    public string? ClassName { get; set; }

    [Column("data_source_id")]
    public long DataSourceId { get; set; }

    [Column("data_source_name")]
    public string DataSourceName { get; set; } = null!;

    [Column("project_id")]
    public long ProjectId { get; set; }
    
    [Column("organization_id")]
    public long OrganizationId { get; set; }

    [Column("project_name")]
    public string ProjectName { get; set; } = null!;

    [Column("tags", TypeName = "jsonb")]
    public string? Tags { get; set; }
    
    [Column("labels", TypeName = "jsonb")]
    public string? Labels { get; set; }

    [Column("last_updated_by")]
    public string? LastUpdatedBy { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("object_storage_id")]
    public long? ObjectStorageId { get; set; }

    [Column("object_storage_name")]
    public string? ObjectStorageName { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    [ForeignKey("RecordId")]
    [InverseProperty("HistoricalRecords")]
    public virtual Record Record { get; set; } = null!;
    
    [ForeignKey("ProjectId")]
    [InverseProperty("HistoricalRecords")]
    public virtual Project Project { get; set; } = null!;
    
    [ForeignKey("OrganizationId")]
    [InverseProperty("HistoricalRecords")]
    public virtual Organization Organization { get; set; } = null!;
}
