using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.models;

public class LatticeRelationshipDto
{
    [Column("origin_class_name")] public string OriginClassName { get; set; } = null!;
    [Column("relationship_name")] public string ClassName { get; set; } = null!;
    [Column("relationship_description")] public string ClassDescription { get; set; } = null!;
    [Column("relationship_properties")] public string ClassProperties { get; set; } = null!;
    [Column("destination_class_name")] public string DestinationClassName { get; set; } = null!;
}