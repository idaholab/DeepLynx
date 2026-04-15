using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("edges", Schema = "deeplynx")]
public class Edge
{
    [Column("origin_id")] public long OriginId { get; set; }

    [Column("destination_id")] public long DestinationId { get; set; }

    [Column("relationship_id")] public long? RelationshipId { get; set; }

    [Column("data_source_id")] public long DataSourceId { get; set; }

    [Column("project_id")] public long ProjectId { get; set; }

    [Column("properties", TypeName = "jsonb")]
    public string? Properties { get; set; }

    [Column("organization_id")] public long OrganizationId { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("last_updated_by")] public long? LastUpdatedBy { get; set; }

    [Key] [Column("id")] public long Id { get; set; }

    [Column("is_archived")] public bool IsArchived { get; set; }

    [Column("extraction_id")] public long? ExtractionId { get; set; }

    [ForeignKey("DataSourceId")]
    [InverseProperty("Edges")]
    public virtual DataSource DataSource { get; set; } = null!;

    [ForeignKey("DestinationId")]
    [InverseProperty("EdgeDestinations")]
    public virtual Record Destination { get; set; } = null!;

    [InverseProperty("Edge")]
    public virtual ICollection<HistoricalEdge> HistoricalEdges { get; set; } = new List<HistoricalEdge>();

    [ForeignKey("OriginId")]
    [InverseProperty("EdgeOrigins")]
    public virtual Record Origin { get; set; } = null!;

    [ForeignKey("ProjectId")]
    [InverseProperty("Edges")]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey("OrganizationId")]
    [InverseProperty("Edges")]
    public virtual Organization Organization { get; set; } = null!;

    [ForeignKey("RelationshipId")]
    [InverseProperty("Edges")]
    public virtual Relationship? Relationship { get; set; }

    [InverseProperty("LastUpdatedEdges")] public virtual User? LastUpdatedByUser { get; set; }
}