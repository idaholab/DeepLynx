using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using deeplynx.models;

public class AirflowServiceClient
{
    private readonly HttpClient _client;
    private readonly string? _airflowJwt;

    public AirflowServiceClient(HttpClient client)
    {
        _client = client;
        var url = Environment.GetEnvironmentVariable("AIRFLOW_BASE_URL");
        if (!string.IsNullOrWhiteSpace(url))
        {
            _client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
        }

        _airflowJwt = Environment.GetEnvironmentVariable("AIRFLOW_JWT")?.Trim();
    }

    public async Task<AirflowDagListResponseDto> GetAllDags()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v2/dags");
        AuthorizeRequest(request);
        var response = await _client.SendAsync(request);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<AirflowDagListResponseDto>()
               ?? throw new InvalidOperationException("Airflow returned an empty response body");
    }

    public async Task<JsonObject> GetHealth()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v2/monitor/health");
        AuthorizeRequest(request);
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
        AuthorizeRequest(request);
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
        AuthorizeRequest(request);
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
        AuthorizeRequest(request);
        var response = await _client.SendAsync(request);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<AirflowDagRunResponseDto>()
               ?? throw new InvalidOperationException($"Airflow returned an empty DAG run response for '{dagRunId}'");
    }

    private void AuthorizeRequest(HttpRequestMessage request)
    {
        if (_client.BaseAddress is null)
        {
            throw new InvalidOperationException("AIRFLOW_BASE_URL environment variable is not set.");
        }

        if (string.IsNullOrWhiteSpace(_airflowJwt))
        {
            throw new InvalidOperationException("AIRFLOW_JWT environment variable is not set.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _airflowJwt);
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
}
