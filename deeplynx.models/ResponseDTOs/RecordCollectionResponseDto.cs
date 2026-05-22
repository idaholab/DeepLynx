using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.models;

public class RecordCollectionTagDto
{
    public long Id { get; set; }
    public string Name { get; set; }
}

public class RecordCollectionLabelDto
{
    public long Id { get; set; }
    public string Name { get; set; }
}

public class RecordCollectionResponseDto
{
    [Column("id")] public long Id { get; set; }

    [Column("name")] public string Name { get; set; }

    [Column("description")] public string Description { get; set; }

    [Column("properties")] public string? Properties { get; set; } = null!;

    [Column("project_id")] public long ProjectId { get; set; }

    [Column("organization_id")] public long OrganizationId { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("last_updated_by")] public long? LastUpdatedBy { get; set; }

    [Column("is_archived")] public bool IsArchived { get; set; } = false;

    [NotMapped] public ICollection<RecordCollectionTagDto> Tags { get; set; }
    [NotMapped] public ICollection<RecordCollectionLabelDto> Labels { get; set; }
}