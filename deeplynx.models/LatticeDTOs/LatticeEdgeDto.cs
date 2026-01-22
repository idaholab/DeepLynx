using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.models;

public class LatticeEdgeDto
{
    [Column("origin_name")] public string OriginName { get; set; } = null!;
    [Column("origin_class_name")] public string? OriginClassName { get; set; } = null!;
    [Column("relationship_name")] public string? RelationshipName { get; set; } = null!;
    [Column("edge_properties")] public string? RelationshipProperties { get; set; } = null!;
    [Column("destination_name")] public string? DestinationName { get; set; } = null!;
    [Column("destination_class_name")] public string DestinationClassName { get; set; } = null!;
}