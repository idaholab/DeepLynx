using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public class TriggerDagRunRequestDto
{
    [JsonPropertyName("dag_run_id")]
    public string? DagRunId { get; set; }

    [JsonPropertyName("logical_date")]
    public DateTimeOffset? LogicalDate { get; set; }

    [JsonPropertyName("data_interval_start")]
    public DateTimeOffset? DataIntervalStart { get; set; }

    [JsonPropertyName("data_interval_end")]
    public DateTimeOffset? DataIntervalEnd { get; set; }

    [JsonPropertyName("run_after")]
    public DateTimeOffset? RunAfter { get; set; }

    [JsonPropertyName("conf")]
    public JsonObject? Conf { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}
