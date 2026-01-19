using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.models;

public class LatticeClassDto
{
    [Column("class_name")] public string ClassName { get; set; } = null!;
    [Column("class_description")] public string ClassDescription { get; set; } = null!;
    [Column("class_properties")] public string ClassProperties { get; set; } = null!;
}