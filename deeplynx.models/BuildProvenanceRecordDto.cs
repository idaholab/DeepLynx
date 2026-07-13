using System.Text.Json.Serialization;

namespace deeplynx.models;

public class BuildProvenanceRecordDto
{
    [JsonPropertyName("record_id")]
    public long RecordId { get; init; }

    [JsonPropertyName("historical_record_id")]
    public long HistoricalRecordId { get; init; }

    [JsonPropertyName("action")]
    public string Action { get; init; } = null!;

    [JsonPropertyName("actor_id")]
    public long ActorId { get; init; }

    [JsonPropertyName("organization_id")]
    public long OrganizationId { get; init; }

    [JsonPropertyName("project_id")]
    public long ProjectId { get; init; }

    [JsonPropertyName("file_uri")]
    public string? FileUri { get; init; }

    [JsonPropertyName("file_hash")]
    public string? FileHash { get; init; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; init; }

    [JsonPropertyName("file_type")]
    public string? FileType { get; init; }

    [JsonPropertyName("ai_config_id")]
    public long? AiConfigId { get; init; }

    [JsonPropertyName("ai_model_provider")]
    public string? AiModelProvider { get; init; }

    [JsonPropertyName("ai_model_name")]
    public string? AiModelName { get; init; }

    [JsonPropertyName("ai_model_type")]
    public string? AiModelType { get; init; }

    [JsonPropertyName("ai_server_url")]
    public string? AiServerUrl { get; init; }
}