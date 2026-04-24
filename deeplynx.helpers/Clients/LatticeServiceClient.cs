using System.Net.Http.Json;
using deeplynx.models;

public class LatticeServiceClient
{
    private readonly HttpClient _client;

    public LatticeServiceClient(HttpClient client)
    {
        _client = client;
        var url = Environment.GetEnvironmentVariable("LATTICE_FASTAPI_URL")
                  ?? throw new InvalidOperationException("LATTICE_FASTAPI_URL environment variable is not set.");
        _client.BaseAddress = new Uri(url);
        // Lattice's trigger endpoint returns 202 immediately because of long wait times. 
        // The actual extraction runs asynchronously on the Lattice side.
        _client.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<LatticeExtractionTriggerResponseDto> TriggerExtraction(LatticeExtractionTriggerRequestDto dto)
    {
        var response = await _client.PostAsJsonAsync("/extract/nexus-triggered", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LatticeExtractionTriggerResponseDto>()
               ?? throw new InvalidOperationException("Lattice returned an empty response body");
    }
}
