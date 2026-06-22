using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("embeddings", Schema = "dl_vector")]
public class Embedding
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("record_id")]
    public long RecordId { get; set; }

    [Column("page_number")]
    public int PageNumber { get; set; }

    [Column("text_chunk")]
    public string TextChunk { get; set; } = null!;

    [Column("vector")]
    public string Vector { get; set; } = null!;

    [Column("organization_id")]
    public long? OrganizationId { get; set; }

    [Column("project_id")]
    public long? ProjectId { get; set; }

    [Column("embedding_model")]
    public string? EmbeddingModel { get; set; }

    [Column("dimensions")]
    public int? Dimensions { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [ForeignKey("RecordId")]
    [InverseProperty("Embeddings")]
    public virtual Record Record { get; set; } = null!;

    [ForeignKey("EmbeddingModel")]
    public virtual AiModelConfig? AiModelConfig { get; set; }
}
