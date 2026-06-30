using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;



[Table("embeddings_logs", Schema = "dl_vector")]
public class EmbeddingLogs
{
    public enum QueueStatus {
        Pending,
        InProgress,
        Failed,
        Retrying,
        Error
    }

    [Key]
    [Column("id")]
    public required long Id { get; set; }

    [Column("job_id")]
    public required long JobId { get; set; }

    [Column("stage")]
    public required string Stage { get; set; }

    [Column("status")]
    public required QueueStatus Status { get; set; }

    [Column("worker")]
    public required string Worker { get; set; }

    [Column("progress")]
    public float Progress { get; set; }

    [Column("error")]
    public required string Error { get; set; }

    [Column("timestamp")]
    public required DateTime Timestamp { get; set; }
}
