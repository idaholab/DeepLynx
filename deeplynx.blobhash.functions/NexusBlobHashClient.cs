using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace deeplynx.blobhash.functions;

public sealed class NexusBlobHashClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NexusBlobHashClient> _logger;
    private readonly NexusTokenClient _tokenClient;
    private readonly BlobHashSettings _settings;

    public NexusBlobHashClient(
        HttpClient httpClient,
        NexusTokenClient tokenClient,
        BlobHashSettings settings,
        ILogger<NexusBlobHashClient> logger)
    {
        _httpClient = httpClient;
        _tokenClient = tokenClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task SendBlobHashAsync(
        long organizationId,
        long projectId,
        NexusBlobHashCallbackRequest callback,
        CancellationToken cancellationToken)
    {
        var callbackUrl =
            $"{_settings.NexusBaseUrl}/organizations/{organizationId}/projects/{projectId}/internal/blob-hashes";

        for (var attempt = 1; attempt <= _settings.CallbackMaxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, callbackUrl)
            {
                Content = JsonContent.Create(callback)
            };

            var token = await _tokenClient.GetTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Stored SHA-256 hash for blob {BlobName} in Nexus project {ProjectId}.",
                    callback.BlobName,
                    projectId);
                return;
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                _logger.LogWarning(
                    "Nexus rejected blob hash callback for {BlobName} with status {StatusCode}: {ResponseBody}",
                    callback.BlobName,
                    response.StatusCode,
                    responseBody);
                return;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                _tokenClient.Invalidate();

            if (!ShouldRetry(response.StatusCode) || attempt == _settings.CallbackMaxAttempts)
                throw new InvalidOperationException(
                    $"Nexus blob hash callback failed for {callback.BlobName} with status {(int)response.StatusCode}: {responseBody}");

            var delay = TimeSpan.FromMilliseconds(
                _settings.CallbackBaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

            _logger.LogWarning(
                "Nexus blob hash callback for {BlobName} returned {StatusCode}; retrying attempt {NextAttempt}/{MaxAttempts} after {Delay}.",
                callback.BlobName,
                response.StatusCode,
                attempt + 1,
                _settings.CallbackMaxAttempts,
                delay);

            await Task.Delay(delay, cancellationToken);
        }
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.NotFound
               or HttpStatusCode.RequestTimeout
               or HttpStatusCode.TooManyRequests
               or HttpStatusCode.Unauthorized
               or HttpStatusCode.Forbidden
               || (int)statusCode >= 500;
    }
}
