namespace deeplynx.models;

public class InsightUploadApiRequestDto
{
    public List<FileInfoDto> FileInfo { get; set; } = new();

    public class FileInfoDto
    {
        public long FileId { get; set; }
        public string FileUri { get; set; } = string.Empty;
    }
}