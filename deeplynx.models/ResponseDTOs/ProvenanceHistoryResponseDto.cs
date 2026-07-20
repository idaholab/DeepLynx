namespace deeplynx.models.ResponseDTOs;

public class ProvenanceHistoryResponseDto
{
    /// <summary>
    ///     Informational message — populated when there is no provenance history to show.
    ///     Null when records are present.
    /// </summary>
    public string? Message { get; set; }
    public List<ProvenanceRecordResponseDto> Records { get; set; } = new();
}