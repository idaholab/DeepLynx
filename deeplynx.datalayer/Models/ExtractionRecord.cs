using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("extraction_records", Schema = "lattice")]
public class ExtractionRecord
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("extraction_id")]
    public long ExtractionId { get; set; }

    [Column("extraction_class_id")]
    public long ExtractionClassId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("attributes", TypeName = "jsonb")]
    public string? Attributes { get; set; }

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

    [Column("validation_status")]
    public string? ValidationStatus { get; set; }

    [Column("frequency")]
    public int Frequency { get; set; }

    [Column("llm_score")]
    public double LlmScore { get; set; }

    [Column("embedding_plausibility")]
    public double EmbeddingPlausibility { get; set; }

    [Column("statistical_frequency")]
    public double StatisticalFrequency { get; set; }

    [Column("structural_consistency")]
    public double StructuralConsistency { get; set; }

    [Column("ensemble_score")]
    public double EnsembleScore { get; set; }

    [Column("deeplynx_record_id")]
    public long? DeeplynxRecordId { get; set; }

    /// <summary>Set when a reviewer rejects this staged item; rejected items are never promoted.</summary>
    [Column("rejected")]
    public bool Rejected { get; set; }

    public virtual ExtractionClass? ExtractionClass { get; set; }
    public virtual ICollection<ExtractionEdge> OriginEdges { get; set; } = [];
    public virtual ICollection<ExtractionEdge> DestinationEdges { get; set; } = [];
}
