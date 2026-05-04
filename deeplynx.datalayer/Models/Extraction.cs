using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("extractions", Schema = "deeplynx")]
public class Extraction
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("properties", TypeName = "jsonb")]
    public string? Properties { get; set; }

    [Column("created_by")]
    public long? CreatedBy { get; set; }

    [Column("status")]
    public string Status { get; set; } = ExtractionStatus.Complete;

    [Column("mode")]
    public string? Mode { get; set; }

    [ForeignKey("CreatedBy")]
    public virtual User? CreatedByUser { get; set; }
}

public static class ExtractionStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Complete = "complete";
    public const string Failed = "failed";
}

public static class ExtractionMode
{
    public const string Discovery = "discovery";
    public const string Strict = "strict";
}
