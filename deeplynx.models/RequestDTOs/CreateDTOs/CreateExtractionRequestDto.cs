using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class CreateExtractionRequestDto
{
    [JsonPropertyName("properties")] public JsonObject? Properties { get; set; }

    [JsonPropertyName("classes")] public List<CreateClassRequestDto>? Classes { get; set; }

    [JsonPropertyName("relationships")] public List<StagingRelationshipDto>? Relationships { get; set; }

    [JsonPropertyName("records")] public List<StagingRecordDto>? Records { get; set; }

    [JsonPropertyName("edges")] public List<StagingEdgeDto>? Edges { get; set; }
}

public class StagingRelationshipDto
{
    [Required] [JsonPropertyName("name")] public string Name { get; set; } = null!;

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("properties")] public JsonObject? Properties { get; set; }

    /// <summary>Direct class ID (deeplynx or staging from a prior extraction)</summary>
    [JsonPropertyName("origin_id")]
    public long? OriginId { get; set; }

    /// <summary>Name of a class being created in this same extraction payload</summary>
    [JsonPropertyName("origin_name")]
    public string? OriginName { get; set; }

    /// <summary>Direct class ID (deeplynx or staging from a prior extraction)</summary>
    [JsonPropertyName("destination_id")]
    public long? DestinationId { get; set; }

    /// <summary>Name of a class being created in this same extraction payload</summary>
    [JsonPropertyName("destination_name")]
    public string? DestinationName { get; set; }
}

public class StagingRecordDto
{
    [Required] [JsonPropertyName("name")] public string Name { get; set; } = null!;

    [Required]
    [JsonPropertyName("original_id")]
    public string OriginalId { get; set; } = null!;

    [Required]
    [JsonPropertyName("properties")]
    public JsonObject Properties { get; set; } = null!;

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("uri")] public string? Uri { get; set; }

    [JsonPropertyName("file_type")] public string? FileType { get; set; }

    [JsonPropertyName("object_storage_id")]
    public long? ObjectStorageId { get; set; }

    /// <summary>Direct class ID (deeplynx or staging from a prior extraction)</summary>
    [JsonPropertyName("class_id")]
    public long? ClassId { get; set; }

    /// <summary>Name of a class in this payload or an existing deeplynx class</summary>
    [JsonPropertyName("class_name")]
    public string? ClassName { get; set; }
}

public class StagingEdgeDto
{
    [JsonPropertyName("properties")] public JsonObject? Properties { get; set; }

    /// <summary>Direct staging record ID from a prior extraction</summary>
    [JsonPropertyName("origin_id")]
    public long? OriginId { get; set; }

    /// <summary>original_id of a record in this same extraction payload</summary>
    [JsonPropertyName("origin_original_id")]
    public string? OriginOriginalId { get; set; }

    /// <summary>Direct staging record ID from a prior extraction</summary>
    [JsonPropertyName("destination_id")]
    public long? DestinationId { get; set; }

    /// <summary>original_id of a record in this same extraction payload</summary>
    [JsonPropertyName("destination_original_id")]
    public string? DestinationOriginalId { get; set; }

    /// <summary>Direct relationship ID (deeplynx or staging from a prior extraction)</summary>
    [JsonPropertyName("relationship_id")]
    public long? RelationshipId { get; set; }

    /// <summary>Name of a relationship being created in this same extraction payload</summary>
    [JsonPropertyName("relationship_name")]
    public string? RelationshipName { get; set; }

    /// <summary>original_id of an existing deeplynx record to use as origin</summary>
    [JsonPropertyName("deeplynx_origin_original_id")]
    public string? DeeplynxOriginOriginalId { get; set; }

    /// <summary>original_id of an existing deeplynx record to use as destination</summary>
    [JsonPropertyName("deeplynx_destination_original_id")]
    public string? DeeplynxDestinationOriginalId { get; set; }

    /// <summary>Name of an existing deeplynx relationship</summary>
    [JsonPropertyName("deeplynx_relationship_name")]
    public string? DeeplynxRelationshipName { get; set; }
}