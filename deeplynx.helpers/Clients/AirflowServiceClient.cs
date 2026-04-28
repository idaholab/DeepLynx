using System.Net.Http.Headers;
using System.Net.Http.Json;
using deeplynx.models;

public class AirflowServiceClient
{
    private readonly HttpClient _client;
    private readonly AirflowTokenService _tokenService;

    public AirflowServiceClient(HttpClient client, AirflowTokenService tokenService)
    {
        var url = Environment.GetEnvironmentVariable("AIRFLOW_BASE_URL")
                  ?? throw new InvalidOperationException("AIRFLOW_BASE_URL environment variable is not set.");
        _client = client;
        _client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
        _tokenService = tokenService;
    }

    public async Task<AirflowDagListResponseDto> GetAllDags()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v2/dags");
        await AuthorizeRequest(request);
        var response = await _client.SendAsync(request);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<AirflowDagListResponseDto>()
               ?? throw new InvalidOperationException("Airflow returned an empty response body");
    }

    public async Task<AirflowDagRunResponseDto> TriggerDagRun(string dagId, TriggerDagRunRequestDto dto)
    {
        dto.LogicalDate ??= DateTimeOffset.UtcNow;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v2/dags/{dagId}/dagRuns");
        request.Content = JsonContent.Create(dto);
        await AuthorizeRequest(request);
        var response = await _client.SendAsync(request);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<AirflowDagRunResponseDto>()
               ?? throw new InvalidOperationException($"Airflow returned an empty response body for DAG '{dagId}'");
    }

    private async Task AuthorizeRequest(HttpRequestMessage request)
    {
        var token = await _tokenService.GetTokenAsync();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Airflow responded with {(int)response.StatusCode} ({response.ReasonPhrase}): {body}",
                inner: null,
                statusCode: response.StatusCode);
        }
    }
}
