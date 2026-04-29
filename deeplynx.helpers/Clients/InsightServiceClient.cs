using System.Net;
using System.Net.Http.Json;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc.Infrastructure;

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
    
    public async Task EmbedStrings(InsightEmbedStringRequestDto dto)
    {
        var response = await _client.PostAsJsonAsync("/embed_strings", dto);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task<HttpResponseMessage> LatticeExtraction(string prompt, string llm_model_name)
    {
        var requestParams = new { mode = "lattice", prompt = prompt, llm_model_name = llm_model_name}; 
        var response = await _client.PostAsJsonAsync("/query", requestParams);
        response.EnsureSuccessStatusCode();
        return response;
    }
}