using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.models;

public class RelationshipResponseDto
{
    [Column("id")] public long Id { get; set; }

    [Column("name")] public string Name { get; set; } = null!;

    [Column("description")] public string? Description { get; set; }

    [Column("properties")] public string? Properties { get; set; }

    [Column("uuid")] public string? Uuid { get; set; }

    [Column("project_id")] public long? ProjectId { get; set; }

    [Column("organization_id")] public long OrganizationId { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("last_updated_by")] public long? LastUpdatedBy { get; set; }

    [Column("is_archived")] public bool IsArchived { get; set; } = false;

    [Column("origin_id")] public long? OriginId { get; set; }

    [Column("destination_id")] public long? DestinationId { get; set; }
}