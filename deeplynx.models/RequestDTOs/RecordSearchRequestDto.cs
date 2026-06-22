using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

/// <summary>
/// Search details for a broad record search.
/// </summary>
public class RecordSearchRequestDto
{
    /// <summary>
    /// Space-separated broad search terms
    /// </summary>
    public string? UserQuery { get; set; }
    /// <summary>
    /// Tags required for every record
    /// </summary>
    public long[] TagIds { get; set; } = [];
    /// <summary>
    /// Class ids to include in the search
    /// </summary>
    public long[] ClassIds { get; set; } = [];
    /// <summary>
    /// Filters to insight eligible records if `true`
    /// </summary>
    public bool IsInsightEligible { get; set; } = false;
    /// <summary>
    /// The Insight embedded status of the record
    /// </summary>
    public EmbeddedRequestDto Embedding { get; set; } = EmbeddedRequestDto.Any;
    /// <summary>
    /// Whether to hide archived record
    /// </summary>
    public bool HideArchived { get; set; } = true;
}
