using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class FileUploadInitRequestDto
{
    [Required] public string FileName { get; set; }
    public long FileSize { get; set; }
}

public class FileUploadSessionResponseDto
{
    public string UploadId { get; set; }
    public long ChunkSize { get; set; }
    public int TotalChunks { get; set; }
}

public class FileUploadCompleteRequestDto
{
    [Required] public string UploadId { get; set; }
    [Required] public string FileName { get; set; }
    public int TotalChunks { get; set; }
}