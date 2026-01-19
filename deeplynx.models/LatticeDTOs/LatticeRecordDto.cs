using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.models;

public class LatticeRecordDto
{
    [Column("record_name")] public string RecordName { get; set; } = null!;
    [Column("record_description")] public string RecordDescription { get; set; } = null!;
    [Column("record_properties")] public string RecordProperties { get; set; } = null!;
    [Column("class_name")] public string ClassName { get; set; } = null!;
}