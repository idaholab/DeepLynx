using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

/// <summary>
/// Staging-only. Holds edges where one or both endpoints reference existing deeplynx records
/// rather than records staged in the same extraction. Resolved to real deeplynx edges at promotion.
/// </summary>
public class CrossSchemaEdge
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("extraction_id")]
    public long ExtractionId { get; set; }

    [Column("data_source_id")]
    public long DataSourceId { get; set; }

    [Column("project_id")]
    public long ProjectId { get; set; }

    [Column("organization_id")]
    public long OrganizationId { get; set; }

    [Column("properties", TypeName = "jsonb")]
    public string? Properties { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("last_updated_by")]
    public long? LastUpdatedBy { get; set; }

    /// <summary>original_id of a record staged in this same extraction</summary>
    [Column("origin_original_id")]
    public string? OriginOriginalId { get; set; }

    /// <summary>original_id of an existing deeplynx record</summary>
    [Column("deeplynx_origin_original_id")]
    public string? DeeplynxOriginOriginalId { get; set; }

    /// <summary>original_id of a record staged in this same extraction</summary>
    [Column("destination_original_id")]
    public string? DestinationOriginalId { get; set; }

    /// <summary>original_id of an existing deeplynx record</summary>
    [Column("deeplynx_destination_original_id")]
    public string? DeeplynxDestinationOriginalId { get; set; }

    /// <summary>Name of a relationship staged in this same extraction</summary>
    [Column("relationship_name")]
    public string? RelationshipName { get; set; }

    /// <summary>Name of an existing deeplynx relationship</summary>
    [Column("deeplynx_relationship_name")]
    public string? DeeplynxRelationshipName { get; set; }
}
