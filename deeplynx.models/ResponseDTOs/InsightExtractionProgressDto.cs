using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class InsightExtractionProgressDto
{
    [JsonPropertyName("stage")] public string Stage { get; set; } = null!;
    [JsonPropertyName("detail")] public string Detail { get; set; } = null!;
    [JsonPropertyName("count")] public int? Count { get; set; }
}

public class InsightExtractionProgressCombinedDto
{
    [JsonPropertyName("progress")]
    public Dictionary<string, InsightExtractionProgressDto> Progress { get; set; } = [];
}