using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.models;

public class EdgeResponseDto
{
    [Column("id")] public long Id { get; set; }

    [Column("origin_id")] public long OriginId { get; set; }

    [Column("destination_id")] public long DestinationId { get; set; }

    [Column("relationship_id")] public long? RelationshipId { get; set; }

    [Column("data_source_id")] public long DataSourceId { get; set; }

    [Column("project_id")] public long ProjectId { get; set; }

    [Column("organization_id")] public long OrganizationId { get; set; }

    [Column("properties")] public string? Properties { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("last_updated_by")] public long? LastUpdatedBy { get; set; }

    [Column("is_archived")] public bool IsArchived { get; set; } = false;
}