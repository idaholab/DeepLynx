using System.Text.Json.Serialization;

namespace deeplynx.models;

/// <summary>
///     Payload sent from Nexus to Lattice to trigger async document extraction.
///     Lattice must return 202 immediately and process the extraction asynchronously,
///     then call back to Nexus via the staging endpoint when complete.
/// </summary>
public class LatticeExtractionTriggerRequestDto
{
    [JsonPropertyName("extraction_id")]
    public long ExtractionId { get; set; }

    [JsonPropertyName("record_id")]
    public long RecordId { get; set; }

    [JsonPropertyName("document_uri")]
    public string? DocumentUri { get; set; }

    [JsonPropertyName("file_type")]
    public string? FileType { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "discovery";

    [JsonPropertyName("ontology_context")]
    public List<OntologySimilarityResultDto> OntologyContext { get; set; } = [];

    [JsonPropertyName("nexus_config")]
    public LatticeNexusConfigDto NexusConfig { get; set; } = new();
}

public class LatticeNexusConfigDto
{
    [JsonPropertyName("org_id")]
    public long OrgId { get; set; }

    [JsonPropertyName("project_id")]
    public long ProjectId { get; set; }

    [JsonPropertyName("datasource_id")]
    public long DataSourceId { get; set; }

    [JsonPropertyName("base_url")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}
