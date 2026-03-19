using System.Net.Http.Json;

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

    public async Task CreateEmbedding(CreateInsightEmbeddingRequestDto request)
    {
        await _client.PostAsJsonAsync("/upload_document", request);
    }
}