using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;



[Table("embeddings_logs", Schema = "dl_vector")]
public class EmbeddingLogs
{

    [Key]
    [Column("id")]
    public required long Id { get; set; }

    [Column("record_id")]
    public required long RecordId { get; set; }

    [Column("job_id")]
    public required string JobId { get; set; }

    [Column("stage")]
    public required string Stage { get; set; }

    [Column("status")]
    public required string Status { get; set; }

    [Column("worker")]
    public required string Worker { get; set; }

    [Column("progress")]
    public float Progress { get; set; }

    [Column("error")]
    public string? Error { get; set; }

    [Column("timestamp")]
    public required DateTime Timestamp { get; set; }

    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }
}
