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

    [Column("extraction_id")] public long? ExtractionId { get; set; }

    [Column("file_content_hash")] public string? FileContentHash { get; set; }

    [NotMapped] public ICollection<RecordTagDto> Tags { get; set; } = new List<RecordTagDto>();
    [NotMapped] public ICollection<RecordLabelDto> Labels { get; set; } = new List<RecordLabelDto>();

    [Column("embedded")] public bool Embedded { get; set; }

    // V1 is a frozen contract, so we bridge to V2 here (renaming Labels to SensitivityLabels) instead of renaming in place.
    public RecordResponseDtoV2 ToV2()
    {
        return new RecordResponseDtoV2
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Uri = Uri,
            Properties = Properties,
            ObjectStorageId = ObjectStorageId,
            OriginalId = OriginalId,
            ClassId = ClassId,
            DataSourceId = DataSourceId,
            ProjectId = ProjectId,
            OrganizationId = OrganizationId,
            LastUpdatedAt = LastUpdatedAt,
            LastUpdatedBy = LastUpdatedBy,
            IsArchived = IsArchived,
            FileType = FileType,
            FileSize = FileSize,
            ExtractionId = ExtractionId,
            FileContentHash = FileContentHash,
            Tags = Tags,
            SensitivityLabels = Labels,
            Embedded = Embedded
        };
    }
}

public class RecordResponseDtoV2
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

    [Column("extraction_id")] public long? ExtractionId { get; set; }

    [Column("file_content_hash")] public string? FileContentHash { get; set; }

    [NotMapped] public ICollection<RecordTagDto> Tags { get; set; } = new List<RecordTagDto>();
    [NotMapped] public ICollection<RecordLabelDto> SensitivityLabels { get; set; } = new List<RecordLabelDto>();

    [Column("embedded")] public bool Embedded { get; set; }
}
