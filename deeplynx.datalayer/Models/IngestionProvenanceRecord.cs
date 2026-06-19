using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("ingestion_provenance_records", Schema = "deeplynx")]
public class IngestionProvenanceRecord
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("record_id")]
    public long RecordId { get; set; }

    [Column("organization_id")]
    public long OrganizationId { get; set; }

    [Column("project_id")]
    public long ProjectId { get; set; }

    [Column("artifact_version_id")]
    public string ArtifactVersionId { get; set; } = null!;

    [Column("pipeline_run_id")]
    public string PipelineRunId { get; set; } = null!;

    [Column("prov_id")]
    public string ProvId { get; set; } = null!;

    [StringLength(64)]
    [Column("content_hash")]
    public string ContentHash { get; set; } = null!;

    [Column("provenance_json", TypeName = "jsonb")]
    public string ProvenanceJson { get; set; } = null!;

    [Column("pipeline_version")]
    public string? PipelineVersion { get; set; }

    [Column("processing_config_version")]
    public string? ProcessingConfigVersion { get; set; }

    [Column("embedding_model_name")]
    public string? EmbeddingModelName { get; set; }

    [Column("signature")]
    public string? Signature { get; set; }

    [Column("signature_algorithm")]
    public string? SignatureAlgorithm { get; set; }

    [Column("signing_key_name")]
    public string? SigningKeyName { get; set; }

    [Column("signing_key_version")]
    public string? SigningKeyVersion { get; set; }

    [StringLength(64)]
    [Column("signed_payload_hash")]
    public string? SignedPayloadHash { get; set; }

    [Column("signed_at", TypeName = "timestamp without time zone")]
    public DateTime? SignedAt { get; set; }

    [Column("verification_status")]
    public string? VerificationStatus { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("RecordId")]
    public virtual Record Record { get; set; } = null!;
}
