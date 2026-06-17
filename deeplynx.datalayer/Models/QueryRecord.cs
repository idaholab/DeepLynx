using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

public partial class QueryRecord
{
    [Column("id")]
    public long Id { get; set; }

    [Column("uri")]
    public string? Uri { get; set; }
    
    [Column("properties", TypeName = "jsonb")]
    public string Properties { get; set; } = null!;

    [Column("original_id")]
    public string? OriginalId { get; set; }
    
    [Column("name")]
    public string? Name { get; set; }
    
    [Column("description")]
    public string? Description { get; set; }

    [Column("class_id")]
    public long? ClassId { get; set; }

    [Column("class_name")]
    public string? ClassName { get; set; }

    [Column("data_source_id")]
    public long DataSourceId { get; set; }

    [Column("data_source_name")]
    public string DataSourceName { get; set; } = null!;
    
    [Column("object_storage_id")]
    public long? ObjectStorageId { get; set; }

    [Column("object_storage_name")]
    public string? ObjectStorageName { get; set; }

    [Column("project_id")]
    public long ProjectId { get; set; }
    
    [Column("project_name")]
    public string ProjectName { get; set; } = null!;
    
    [Column("organization_id")]
    public long OrganizationId { get; set; }
    
    [Column("file_type")]
    public string? FileType { get; set; }
    
    [Column("file_size")]
    public long? FileSize { get; set; }
    
    [Column("tags", TypeName = "jsonb")]
    public string? Tags { get; set; }
    
    [Column("labels", TypeName = "jsonb")]
    public string? Labels { get; set; }

    [Column("last_updated_by")]
    public long? LastUpdatedBy { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }
}
