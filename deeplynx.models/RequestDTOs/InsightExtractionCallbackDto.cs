using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class InsightExtractionCallbackDto
{
    [JsonPropertyName("classes")] public List<InsightExtractedClassDto> Classes { get; set; } = [];
    [JsonPropertyName("relationships")] public List<InsightExtractedRelationshipDto> Relationships { get; set; } = [];
}

public class InsightExtractedClassDto
{
    [JsonPropertyName("class")] public string Class { get; set; } = null!;
    [JsonPropertyName("class_type")] public string ClassType { get; set; } = null!;
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
    [JsonPropertyName("attributes")] public JsonObject? Attributes { get; set; }
}

public class InsightExtractedRelationshipDto
{
    [JsonPropertyName("subject")] public string Subject { get; set; } = null!;
    [JsonPropertyName("subject_type")] public string SubjectType { get; set; } = null!;
    [JsonPropertyName("relationship_type")] public string RelationshipType { get; set; } = null!;
    [JsonPropertyName("object")] public string Object { get; set; } = null!;
    [JsonPropertyName("object_type")] public string ObjectType { get; set; } = null!;
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
}
