using System.Text.Json.Serialization;

namespace deeplynx.models;

public class InsightPipelineStatusResponseDto
{
    [JsonPropertyName("record_id")]
    public long RecordId { get; set; }

    [JsonPropertyName("job_id")]
    public string? JobId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("stage")]
    public string? Stage { get; set; }

    [JsonPropertyName("worker")]
    public string? Worker { get; set; }

    [JsonPropertyName("progress")]
    public float? Progress { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}