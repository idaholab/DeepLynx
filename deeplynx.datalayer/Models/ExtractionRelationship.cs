using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("extraction_relationships", Schema = "lattice")]
public class ExtractionRelationship
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("extraction_id")]
    public long ExtractionId { get; set; }

    [Column("origin_class_id")]
    public long OriginClassId { get; set; }

    [Column("destination_class_id")]
    public long DestinationClassId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("organization_id")]
    public long OrganizationId { get; set; }

    [Column("project_id")]
    public long ProjectId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("promoted_id")]
    public long? PromotedId { get; set; }

    [Column("validation_status")]
    public string? ValidationStatus { get; set; }

    [Column("ontology_relationship_id")]
    public long? OntologyRelationshipId { get; set; }

    /// <summary>Set when a reviewer rejects this staged item; rejected items are never promoted.</summary>
    [Column("rejected")]
    public bool Rejected { get; set; }

    public virtual ExtractionClass? OriginClass { get; set; }
    public virtual ExtractionClass? DestinationClass { get; set; }
    public virtual ICollection<ExtractionEdge> Edges { get; set; } = [];
}
