using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("extraction_edges", Schema = "lattice")]
public class ExtractionEdge
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("extraction_id")]
    public long ExtractionId { get; set; }

    [Column("extraction_relationship_id")]
    public long ExtractionRelationshipId { get; set; }

    [Column("origin_record_id")]
    public long OriginRecordId { get; set; }

    [Column("destination_record_id")]
    public long DestinationRecordId { get; set; }

    [Column("confidence")]
    public double Confidence { get; set; }

    [Column("organization_id")]
    public long OrganizationId { get; set; }

    [Column("project_id")]
    public long ProjectId { get; set; }

    [Column("data_source_id")]
    public long DataSourceId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("promoted_id")]
    public long? PromotedId { get; set; }

    public virtual ExtractionRelationship? ExtractionRelationship { get; set; }
    public virtual ExtractionRecord? OriginRecord { get; set; }
    public virtual ExtractionRecord? DestinationRecord { get; set; }
}
