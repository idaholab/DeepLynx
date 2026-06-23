using System.Text.Json.Serialization;

namespace deeplynx.models;

public class PromoteExtractionRequestDto
{
    [JsonPropertyName("class_ids")] public List<long> ClassIds { get; set; } = [];
    [JsonPropertyName("record_ids")] public List<long> RecordIds { get; set; } = [];
    [JsonPropertyName("relationship_ids")] public List<long> RelationshipIds { get; set; } = [];
    [JsonPropertyName("edge_ids")] public List<long> EdgeIds { get; set; } = [];

    /// <summary>
    ///     Bulk-approve every not-yet-promoted item whose validation status is in this list.
    ///     Only <c>valid</c> and <c>novel_discovery</c> are accepted.
    /// </summary>
    [JsonPropertyName("approve_by_status")] public List<string> ApproveByStatus { get; set; } = [];
}
