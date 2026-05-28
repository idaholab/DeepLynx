using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.models;

public class RecordTagDto
{
    public long Id { get; set; }
    public string Name { get; set; }
}

public class RecordLabelDto
{
    public long Id { get; set; }
    public string Name { get; set; }
}

public class RecordResponseDto
{
    [Column("id")] public long Id { get; set; }

    [Column("name")] public string Name { get; set; }

    [Column("description")] public string Description { get; set; }

    [Column("uri")] public string? Uri { get; set; }

    [Column("properties")] public string Properties { get; set; } = null!;

    [Column("object_storage_id")] public long? ObjectStorageId { get; set; }

    [Column("original_id")] public string OriginalId { get; set; }

    [Column("class_id")] public long? ClassId { get; set; }

    [Column("data_source_id")] public long DataSourceId { get; set; }

    [Column("project_id")] public long ProjectId { get; set; }

    [Column("organization_id")] public long OrganizationId { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("last_updated_by")] public long? LastUpdatedBy { get; set; }

    [Column("is_archived")] public bool IsArchived { get; set; } = false;

    [Column("file_type")] public string? FileType { get; set; }

    [Column("file_size")] public long? FileSize { get; set; }

    [NotMapped] public ICollection<RecordTagDto> Tags { get; set; }
    [NotMapped] public ICollection<RecordLabelDto> Labels { get; set; }
    
    [Column("embedded")] public bool Embedded { get; set; }
}