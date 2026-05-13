using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class AirflowDagListResponseDto
{
    [JsonPropertyName("dags")]
    public List<AirflowDagDto> Dags { get; set; } = [];

    [JsonPropertyName("total_entries")]
    public int TotalEntries { get; set; }
}

public class AirflowDagDto
{
    [JsonPropertyName("dag_id")]
    public string DagId { get; set; } = string.Empty;

    [JsonPropertyName("dag_display_name")]
    public string? DagDisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_paused")]
    public bool IsPaused { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("owners")]
    public List<string> Owners { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<AirflowDagTagDto> Tags { get; set; } = [];

    [JsonPropertyName("timetable_description")]
    public string? TimetableDescription { get; set; }
}

public class AirflowDagTagDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class AirflowDagRunResponseDto
{
    [JsonPropertyName("dag_run_id")]
    public string? DagRunId { get; set; }

    [JsonPropertyName("dag_id")]
    public string DagId { get; set; } = string.Empty;

    [JsonPropertyName("dag_display_name")]
    public string? DagDisplayName { get; set; }

    [JsonPropertyName("logical_date")]
    public DateTimeOffset? LogicalDate { get; set; }

    [JsonPropertyName("queued_at")]
    public DateTimeOffset? QueuedAt { get; set; }

    [JsonPropertyName("start_date")]
    public DateTimeOffset? StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public DateTimeOffset? EndDate { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("data_interval_start")]
    public DateTimeOffset? DataIntervalStart { get; set; }

    [JsonPropertyName("data_interval_end")]
    public DateTimeOffset? DataIntervalEnd { get; set; }

    [JsonPropertyName("run_after")]
    public DateTimeOffset? RunAfter { get; set; }

    [JsonPropertyName("last_scheduling_decision")]
    public DateTimeOffset? LastSchedulingDecision { get; set; }

    [JsonPropertyName("run_type")]
    public string? RunType { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("triggered_by")]
    public string? TriggeredBy { get; set; }

    [JsonPropertyName("triggering_user_name")]
    public string? TriggeringUserName { get; set; }

    [JsonPropertyName("conf")]
    public JsonObject? Conf { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("bundle_version")]
    public string? BundleVersion { get; set; }
}
