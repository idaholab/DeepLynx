using System.Text.Json.Serialization;

namespace deeplynx.models;

/// <summary>
/// Response from a single bounded scrape call. Mirrors the BackfillFileSizesResponseDto
/// convention: the client loops, passing NextCursor back in as afterCursor, until
/// NextCursor comes back null.
/// </summary>
public class ScrapeObjectStorageResponseDto
{
    [JsonPropertyName("processed")]
    public long Processed { get; set; }

    /// <summary>
    /// Opaque cursor to pass back in as afterCursor on the next call.
    /// Null once the entire storage has been fully scraped.
    /// </summary>
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}