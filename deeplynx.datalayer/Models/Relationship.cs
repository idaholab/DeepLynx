using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("relationships", Schema = "deeplynx")]
public class Relationship
{
    [Key] [Column("id")] public long Id { get; set; }

    [Column("name")] public string Name { get; set; } = null!;

    [Column("description")] public string? Description { get; set; }

    [Column("uuid")] public string? Uuid { get; set; }

    [Column("origin_id")] public long? OriginId { get; set; }

    [Column("properties", TypeName = "jsonb")]
    public string? Properties { get; set; }

    [Column("destination_id")] public long? DestinationId { get; set; }

    [Column("project_id")] public long? ProjectId { get; set; }

    [Column("organization_id")] public long OrganizationId { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("last_updated_by")] public long? LastUpdatedBy { get; set; }

    [Column("is_archived")] public bool IsArchived { get; set; }

    [ForeignKey("DestinationId")]
    [InverseProperty("RelationshipDestinations")]
    public virtual Class? Destination { get; set; }

    [InverseProperty("Relationship")] public virtual ICollection<Edge> Edges { get; set; } = new List<Edge>();

    [ForeignKey("OriginId")]
    [InverseProperty("RelationshipOrigins")]
    public virtual Class? Origin { get; set; }

    [ForeignKey("ProjectId")]
    [InverseProperty("Relationships")]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey("OrganizationId")]
    [InverseProperty("Relationships")]
    public virtual Organization Organization { get; set; } = null!;

    [InverseProperty("LastUpdatedRelationships")]
    public virtual User? LastUpdatedByUser { get; set; }
}