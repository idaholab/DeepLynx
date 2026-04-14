using System.Text.Json.Serialization;

namespace deeplynx.models;

public class InsightIngestionStatusResponseDto
{
    [JsonPropertyName("file_id")]
    public long FileId { get; set; }

    [JsonPropertyName("indexed")]
    public bool Indexed { get; set; }

    [JsonPropertyName("chunk_count")]
    public int ChunkCount { get; set; }

    [JsonPropertyName("page_count")]
    public int PageCount { get; set; }
}