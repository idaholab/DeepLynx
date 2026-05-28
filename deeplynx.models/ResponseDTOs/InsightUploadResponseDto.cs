namespace deeplynx.models;

public class InsightUploadResponseDto
{
    public int FileId { get; set; }
    public string Status { get; set; } = string.Empty; // "queued", "skipped", "error"
    public string? Error { get; set; }
    public string? Reason { get; set; }
    public string? PdfUrl { get; set; }
    public string? FileType { get; set; }
    public string? QueueName { get; set; }
}