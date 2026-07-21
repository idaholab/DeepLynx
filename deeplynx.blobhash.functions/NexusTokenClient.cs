using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace deeplynx.blobhash.functions;

public sealed class NexusTokenClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NexusTokenClient> _logger;
    private readonly BlobHashSettings _settings;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _cachedTokenExpiresAt;

    public NexusTokenClient(
        HttpClient httpClient,
        BlobHashSettings settings,
        ILogger<NexusTokenClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_cachedToken) && _cachedTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
            return _cachedToken;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken) && _cachedTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
                return _cachedToken;

            var request = new NexusTokenRequest
            {
                ApiKey = _settings.NexusApiKey,
                ApiSecret = _settings.NexusApiSecret,
                ExpirationMinutes = _settings.NexusTokenExpirationMinutes
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_settings.NexusBaseUrl}/oauth/tokens",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<string>(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Nexus token endpoint returned an empty token.");

            _cachedToken = token;
            _cachedTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_settings.NexusTokenExpirationMinutes - 2);

            _logger.LogInformation("Refreshed Nexus callback token.");

            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public void Invalidate()
    {
        _cachedToken = null;
        _cachedTokenExpiresAt = DateTimeOffset.MinValue;
    }
}
