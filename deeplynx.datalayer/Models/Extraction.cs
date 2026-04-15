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

    [ForeignKey("CreatedBy")]
    public virtual User? CreatedByUser { get; set; }
}
