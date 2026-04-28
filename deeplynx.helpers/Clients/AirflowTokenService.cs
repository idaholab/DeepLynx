using System.Net.Http.Json;
using System.Text.Json.Serialization;

public class AirflowTokenService : IDisposable
{
    private readonly HttpClient _http = new();
    private string? _token;
    private DateTimeOffset _expiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Airflow default token lifetime is 3600s; refresh at 55m to give a safe buffer
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(55);

    public async Task<string> GetTokenAsync()
    {
        if (_token != null && DateTimeOffset.UtcNow < _expiry)
            return _token;

        await _lock.WaitAsync();
        try
        {
            // Re-check after acquiring the lock — another thread may have refreshed it
            if (_token != null && DateTimeOffset.UtcNow < _expiry)
                return _token;

            var url = Environment.GetEnvironmentVariable("AIRFLOW_BASE_URL")
                      ?? throw new InvalidOperationException("AIRFLOW_BASE_URL environment variable is not set.");
            var username = Environment.GetEnvironmentVariable("AIRFLOW_USERNAME")
                           ?? throw new InvalidOperationException("AIRFLOW_USERNAME environment variable is not set.");
            var password = Environment.GetEnvironmentVariable("AIRFLOW_PASSWORD")
                           ?? throw new InvalidOperationException("AIRFLOW_PASSWORD environment variable is not set.");

            _http.BaseAddress ??= new Uri(url.TrimEnd('/') + "/");

            var response = await _http.PostAsJsonAsync("auth/token", new { username, password });
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Airflow token request failed with {(int)response.StatusCode}: {body}",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>()
                         ?? throw new InvalidOperationException("Airflow token response was empty.");
            _token = result.AccessToken
                     ?? throw new InvalidOperationException("Airflow token response did not contain an access_token.");
            _expiry = DateTimeOffset.UtcNow.Add(TokenLifetime);

            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _http.Dispose();

    private record TokenResponse([property: JsonPropertyName("access_token")] string? AccessToken);
}
