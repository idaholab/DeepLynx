using System.Text.Json.Serialization;

namespace deeplynx.models;

/// <summary>
///     Error payload sent by Lattice when an extraction fails.
///     Lattice POSTs this to the /error endpoint so Nexus can mark the extraction as failed.
/// </summary>
public class LatticeExtractionErrorDto
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}
