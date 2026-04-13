using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace deeplynx.datalayer.Models;

[Table("ontology_vector", Schema = "dl_vector")]
public class OntologyVector
{
    [Key]
    [Column("id")]
    public long Id { get; set; }
    
    [Column("vector")]
    public Vector Vector { get; set; } = null!;
    
    [Column("class_id")]
    public long? ClassId { get; set; }
    
    [Column("relationship_id")]
    public long? RelationshipId { get; set; }

    [ForeignKey("ClassId")]
    [InverseProperty("OntologyVectors")]
    public virtual Class Class { get; set; } = null!;
    
    [ForeignKey("RelationshipId")]
    [InverseProperty("OntologyVectors")]
    public virtual Relationship Relationship { get; set; } = null!;
}