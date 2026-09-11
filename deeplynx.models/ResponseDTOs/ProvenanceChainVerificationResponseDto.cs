namespace deeplynx.models.ResponseDTOs;

public class ProvenanceChainVerificationResponseDto
{
    public long RecordId { get; set; }
    public bool IsValid { get; set; }
    public int RecordsVerified { get; set; }
    public long? CheckpointRecordId { get; set; }
    public long? FirstInvalidProvenanceRecordId { get; set; }
    public string? ExpectedChainHash { get; set; }
    public string? ActualChainHash { get; set; }
    public string Message { get; set; } = null!;
}
