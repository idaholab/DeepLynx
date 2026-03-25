using System.Net.Http.Json;
using deeplynx.models;

public class InsightServiceClient
{
    private readonly HttpClient _client;

    public InsightServiceClient(HttpClient client)
    {
        _client = client;
        var url = Environment.GetEnvironmentVariable("INSIGHT_FASTAPI_URL")
                  ?? throw new InvalidOperationException("INSIGHT_FASTAPI_URL environment variable is not set.");
        _client.BaseAddress = new Uri(url);
    }

    public async Task<InsightUploadResponseDto> Upload(InsightUploadRequestDto dto)
    {
        var response = await _client.PostAsJsonAsync("/upload_document", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InsightUploadResponseDto>()
               ?? throw new InvalidOperationException("Insight returned an empty response body");
    }

    public async Task<Stream> Query(InsightQueryRequestDto dto)
    {
        var response = await _client.PostAsJsonAsync("/query", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }

    public async Task<InsightIngestionStatusResponseDto> GetIngestionStatus(long fileId)
    {
        var response = await _client.GetAsync($"/ingestion_status/{fileId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InsightIngestionStatusResponseDto>()
               ?? throw new InvalidOperationException($"Insight returned an empty response body for file {fileId}");
    }
}