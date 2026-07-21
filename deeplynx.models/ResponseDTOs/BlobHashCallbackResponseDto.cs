namespace deeplynx.models;

public class BlobHashCallbackResponseDto
{
    public long RecordId { get; set; }
    public string BlobName { get; set; } = null!;
    public string HashAlgorithm { get; set; } = null!;
    public string HashHex { get; set; } = null!;
    public bool Updated { get; set; }
}
