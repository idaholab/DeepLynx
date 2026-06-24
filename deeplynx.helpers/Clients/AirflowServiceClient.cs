using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using deeplynx.models;
using Microsoft.Extensions.Caching.Memory;

public class AirflowServiceClient
{
    private readonly HttpClient _client;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(55);
    private const string TokenCacheKey = "airflow_token";

    public AirflowServiceClient(HttpClient client, IMemoryCache cache)
    {
        var url = Environment.GetEnvironmentVariable("AIRFLOW_BASE_URL")
                  ?? throw new InvalidOperationException("AIRFLOW_BASE_URL environment variable is not set.");
        _client = client;
        _client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
        _cache = cache;
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

    public async Task<JsonObject> GetHealth()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v2/monitor/health");
        await AuthorizeRequest(request);
        var response = await _client.SendAsync(request);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<JsonObject>()
               ?? throw new InvalidOperationException("Airflow returned an empty health response body");
    }

    public async Task<AirflowDagDto> GetDagDetails(string dagId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v2/dags/{Uri.EscapeDataString(dagId)}/details");
        await AuthorizeRequest(request);
        var response = await _client.SendAsync(request);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<AirflowDagDto>()
               ?? throw new InvalidOperationException($"Airflow returned an empty response body for DAG '{dagId}'");
    }

    public async Task<AirflowDagRunResponseDto> TriggerDagRun(string dagId, TriggerDagRunRequestDto dto)
    {
        // logical_date is required by Airflow's trigger API; default to UtcNow if not provided by the caller
        dto.LogicalDate ??= DateTimeOffset.UtcNow;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/v2/dags/{Uri.EscapeDataString(dagId)}/dagRuns");
        request.Content = JsonContent.Create(dto);
        await AuthorizeRequest(request);
        var response = await _client.SendAsync(request);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<AirflowDagRunResponseDto>()
               ?? throw new InvalidOperationException($"Airflow returned an empty response body for DAG '{dagId}'");
    }

    public async Task<AirflowDagRunResponseDto> GetDagRun(string dagId, string dagRunId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v2/dags/{Uri.EscapeDataString(dagId)}/dagRuns/{Uri.EscapeDataString(dagRunId)}");
        await AuthorizeRequest(request);
        var response = await _client.SendAsync(request);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<AirflowDagRunResponseDto>()
               ?? throw new InvalidOperationException($"Airflow returned an empty DAG run response for '{dagRunId}'");
    }

    private async Task AuthorizeRequest(HttpRequestMessage request)
    {
        var token = await _cache.GetOrCreateAsync(TokenCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TokenLifetime;
            return await FetchToken();
        });
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> FetchToken()
    {
        var username = Environment.GetEnvironmentVariable("AIRFLOW_USERNAME")
                       ?? throw new InvalidOperationException("AIRFLOW_USERNAME environment variable is not set.");
        var password = Environment.GetEnvironmentVariable("AIRFLOW_PASSWORD")
                       ?? throw new InvalidOperationException("AIRFLOW_PASSWORD environment variable is not set.");

        var response = await _client.PostAsJsonAsync("auth/token", new { username, password });
        await EnsureSuccess(response);
        var result = await response.Content.ReadFromJsonAsync<TokenResponse>()
                     ?? throw new InvalidOperationException("Airflow token response was empty.");
        return result.AccessToken
               ?? throw new InvalidOperationException("Airflow token response did not contain an access_token.");
    }

    private static async Task<HttpRequestException> CreateAirflowException(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return new HttpRequestException(
                $"Airflow responded with {(int)response.StatusCode} ({response.ReasonPhrase}): {body}",
                inner: null,
                statusCode: response.StatusCode);
        }

        return new HttpRequestException("Airflow request failed unexpectedly.");
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateAirflowException(response);
        }
    }

    private record TokenResponse([property: JsonPropertyName("access_token")] string? AccessToken);
}
