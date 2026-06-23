using System.Text.Json.Serialization;

namespace deeplynx.models;

public class ExtractionStagingResponseDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = null!;
    [JsonPropertyName("mode")] public string? Mode { get; set; }
    [JsonPropertyName("created_by")] public long? CreatedBy { get; set; }
    [JsonPropertyName("failure_message")] public string? FailureMessage { get; set; }
    [JsonPropertyName("classes")] public List<StagedClassDto> Classes { get; set; } = [];
    [JsonPropertyName("records")] public List<StagedRecordDto> Records { get; set; } = [];
    [JsonPropertyName("relationships")] public List<StagedRelationshipDto> Relationships { get; set; } = [];
    [JsonPropertyName("edges")] public List<StagedEdgeDto> Edges { get; set; } = [];
}

public class StagedClassDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
    [JsonPropertyName("validation_status")] public string? ValidationStatus { get; set; }
    [JsonPropertyName("ontology_class_id")] public long? OntologyClassId { get; set; }
    [JsonPropertyName("promoted_id")] public long? PromotedId { get; set; }
    [JsonPropertyName("rejected")] public bool Rejected { get; set; }
}

public class StagedRecordDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = null!;

    /// <summary>Staging class this record depends on — used by the UI to cascade enable/disable.</summary>
    [JsonPropertyName("extraction_class_id")] public long ExtractionClassId { get; set; }
    [JsonPropertyName("class_name")] public string? ClassName { get; set; }
    [JsonPropertyName("attributes")] public string? Attributes { get; set; }
    [JsonPropertyName("validation_status")] public string? ValidationStatus { get; set; }
    [JsonPropertyName("ensemble_score")] public double EnsembleScore { get; set; }
    [JsonPropertyName("frequency")] public int Frequency { get; set; }
    [JsonPropertyName("deeplynx_record_id")] public long? DeeplynxRecordId { get; set; }
    [JsonPropertyName("promoted_id")] public long? PromotedId { get; set; }
    [JsonPropertyName("rejected")] public bool Rejected { get; set; }
}

public class StagedRelationshipDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = null!;

    /// <summary>Staging classes this relationship depends on — used by the UI to cascade enable/disable.</summary>
    [JsonPropertyName("origin_class_id")] public long OriginClassId { get; set; }
    [JsonPropertyName("destination_class_id")] public long DestinationClassId { get; set; }
    [JsonPropertyName("origin_class_name")] public string? OriginClassName { get; set; }
    [JsonPropertyName("destination_class_name")] public string? DestinationClassName { get; set; }
    [JsonPropertyName("validation_status")] public string? ValidationStatus { get; set; }
    [JsonPropertyName("ontology_relationship_id")] public long? OntologyRelationshipId { get; set; }
    [JsonPropertyName("promoted_id")] public long? PromotedId { get; set; }
    [JsonPropertyName("rejected")] public bool Rejected { get; set; }
}

public class StagedEdgeDto
{
    [JsonPropertyName("id")] public long Id { get; set; }

    /// <summary>Staging records and relationship this edge depends on — used by the UI to cascade enable/disable.</summary>
    [JsonPropertyName("origin_record_id")] public long OriginRecordId { get; set; }
    [JsonPropertyName("destination_record_id")] public long DestinationRecordId { get; set; }
    [JsonPropertyName("extraction_relationship_id")] public long ExtractionRelationshipId { get; set; }
    [JsonPropertyName("origin_record_name")] public string? OriginRecordName { get; set; }
    [JsonPropertyName("destination_record_name")] public string? DestinationRecordName { get; set; }
    [JsonPropertyName("relationship_name")] public string? RelationshipName { get; set; }
    [JsonPropertyName("validation_status")] public string? ValidationStatus { get; set; }
    [JsonPropertyName("ensemble_score")] public double EnsembleScore { get; set; }
    [JsonPropertyName("frequency")] public int Frequency { get; set; }
    [JsonPropertyName("promoted_id")] public long? PromotedId { get; set; }
    [JsonPropertyName("rejected")] public bool Rejected { get; set; }
}
