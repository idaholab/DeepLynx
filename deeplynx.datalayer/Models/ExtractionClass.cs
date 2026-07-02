using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("extraction_classes", Schema = "lattice")]
public class ExtractionClass
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("extraction_id")]
    public long ExtractionId { get; set; }

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

    [Column("ontology_class_id")]
    public long? OntologyClassId { get; set; }

    [Column("rejected")]
    public bool Rejected { get; set; }

    public virtual ICollection<ExtractionRecord> Records { get; set; } = [];
    public virtual ICollection<ExtractionRelationship> OriginRelationships { get; set; } = [];
    public virtual ICollection<ExtractionRelationship> DestinationRelationships { get; set; } = [];
}
