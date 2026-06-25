using System.Text.Json.Serialization;

namespace deeplynx.models;

public class RejectExtractionRequestDto
{
    [JsonPropertyName("class_ids")] public List<long> ClassIds { get; set; } = [];
    [JsonPropertyName("record_ids")] public List<long> RecordIds { get; set; } = [];
    [JsonPropertyName("relationship_ids")] public List<long> RelationshipIds { get; set; } = [];
    [JsonPropertyName("edge_ids")] public List<long> EdgeIds { get; set; } = [];

    /// <summary>
    ///     Bulk-reject every not-yet-resolved item whose validation status is in this list. Any status is
    ///     allowed (unlike approval, which is restricted to <c>valid</c> / <c>novel_discovery</c>).
    /// </summary>
    [JsonPropertyName("reject_by_status")]
    public List<string> RejectByStatus { get; set; } = [];

    /// <summary>
    ///     When true, reject every still-pending (not promoted, not already rejected) item in the
    ///     extraction, regardless of the id/status selections. The "reject the rest" action.
    /// </summary>
    [JsonPropertyName("reject_all_remaining")]
    public bool RejectAllRemaining { get; set; }
}