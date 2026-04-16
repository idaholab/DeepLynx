using System.Text.Json.Serialization;

namespace deeplynx.models;

/// <summary>
///     Response from Lattice after accepting an extraction trigger.
///     Lattice returns this immediately (202 Accepted) before processing begins.
/// </summary>
public class LatticeExtractionTriggerResponseDto
{
    [JsonPropertyName("extraction_id")]
    public long ExtractionId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "accepted";
}
