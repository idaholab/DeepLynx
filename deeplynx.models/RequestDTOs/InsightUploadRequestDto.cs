using System.Text.Json.Serialization;

namespace deeplynx.models;

public class InsightUploadRequestDto
{
    [JsonPropertyName("file_info")]
    public List<InsightUploadFileInfoBody> FileInfo { get; set; } = [];

}

public class InsightUploadFileInfoBody
{
    [JsonPropertyName("fileId")]
    public long FileId { get; set; }

    [JsonPropertyName("fileURI")]
    public string FileUri { get; set; } = string.Empty;
}