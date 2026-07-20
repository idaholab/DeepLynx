using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("provenance_records", Schema = "deeplynx")]
public class ProvenanceRecord
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("record_id")]
    public long RecordId { get; set; }

    [Column("historical_record_id")]
    public long HistoricalRecordId { get; set; }

    [Column("organization_id")]
    public long OrganizationId { get; set; }

    [Column("project_id")]
    public long ProjectId { get; set; }

    [Column("prov_id")]
    public string ProvId { get; set; } = null!;

    [Column("file_content_hash")]
    public string? FileContentHash { get; set; }

    [Column("provenance_json", TypeName = "jsonb")]
    public string ProvenanceJson { get; set; } = null!;

    [Column("signature")]
    public string? Signature { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("RecordId")]
    [InverseProperty("ProvenanceRecords")]
    public virtual Record Record { get; set; } = null!;

    [ForeignKey("HistoricalRecordId")]
    [InverseProperty("ProvenanceRecords")]
    public virtual HistoricalRecord HistoricalRecord { get; set; } = null!;

    [ForeignKey("ProjectId")]
    [InverseProperty("ProvenanceRecords")]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey("OrganizationId")]
    [InverseProperty("ProvenanceRecords")]
    public virtual Organization Organization { get; set; } = null!;
}