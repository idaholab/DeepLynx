using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InsightServiceException(
                BuildInsightEndpointFailureMessage("/upload_document", response, responseBody),
                response.StatusCode,
                responseBody);
        }

        if (string.IsNullOrWhiteSpace(responseBody))
            throw new InvalidOperationException("Insight returned an empty response body");

        return JsonSerializer.Deserialize<InsightUploadResponseDto>(
                   responseBody,
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
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

    public async Task<InsightEndpointHealthResponseDto> EndpointHealth(
        InsightEndpointHealthRequestDto dto)
    {
        var response = await _client.PostAsJsonAsync("/endpoint_health", dto);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InsightServiceException(
                BuildInsightEndpointFailureMessage("/endpoint_health", response, responseBody),
                response.StatusCode,
                responseBody);
        }
        
        if (string.IsNullOrWhiteSpace(responseBody))
            throw new InvalidOperationException("Insight returned an empty response body");
        
        return JsonSerializer.Deserialize<InsightEndpointHealthResponseDto>(
            responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Insight returned an empty response body");
    }
    
    public async Task EmbedStrings(InsightEmbedStringRequestDto dto)
    {
        var response = await _client.PostAsJsonAsync("/embed_strings", dto);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InsightServiceException(
                BuildInsightEndpointFailureMessage("/embed_strings", response, responseBody),
                response.StatusCode,
                responseBody);
        }
    }

    public async Task<HttpResponseMessage> LatticeExtraction(
        string prompt,
        string llmModelName,
        object queryInfo)
    {
        var requestParams = new { prompt, llm_model_name = llmModelName, query_info = queryInfo };

        HttpResponseMessage response;
        try
        {
            response = await _client.PostAsJsonAsync("/lattice_query", requestParams);
        }
        catch (Exception ex)
        {
            throw new InsightServiceException(
                "Failed to connect to Insight /lattice_query endpoint.",
                innerException: ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InsightServiceException(
                BuildLatticeExtractionFailureMessage(response, responseBody),
                response.StatusCode,
                responseBody);
        }

        return response;
    }

    private static string BuildLatticeExtractionFailureMessage(HttpResponseMessage response, string responseBody)
    {
        return BuildInsightEndpointFailureMessage("/lattice_query", response, responseBody);
    }

    private static string BuildInsightEndpointFailureMessage(
        string endpoint,
        HttpResponseMessage response,
        string responseBody)
    {
        var status = $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim();
        var detail = ExtractInsightErrorDetail(responseBody);

        return string.IsNullOrWhiteSpace(detail)
            ? $"Insight {endpoint} failed with {status}."
            : $"Insight {endpoint} failed with {status}. {detail}";
    }

    private static string? ExtractInsightErrorDetail(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return null;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (TryReadErrorProperty(root, "detail", out var detail)) return NormalizeNestedErrorDetail(detail);
            if (TryReadErrorProperty(root, "message", out var message)) return NormalizeNestedErrorDetail(message);
            if (TryReadErrorProperty(root, "error", out var error)) return NormalizeNestedErrorDetail(error);
        }
        catch (JsonException)
        {
            return NormalizeNestedErrorDetail(responseBody.Trim());
        }

        return NormalizeNestedErrorDetail(responseBody.Trim());
    }

    private static bool TryReadErrorProperty(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        if (property.ValueKind == JsonValueKind.Object &&
            property.TryGetProperty("message", out var message) &&
            message.ValueKind == JsonValueKind.String)
        {
            value = message.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = property.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string NormalizeNestedErrorDetail(string detail)
    {
        var trimmed = detail.Trim();
        var jsonStart = trimmed.IndexOf('{');
        if (jsonStart < 0) return SanitizeProviderError(trimmed);

        var prefix = trimmed[..jsonStart].Trim().TrimEnd(':');
        var json = trimmed[jsonStart..];

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string nested;
            if (TryReadErrorProperty(root, "error", out nested) ||
                TryReadErrorProperty(root, "detail", out nested) ||
                TryReadErrorProperty(root, "message", out nested))
            {
                nested = SanitizeProviderError(nested);
                return string.IsNullOrWhiteSpace(prefix)
                    ? nested
                    : $"{prefix}: {nested}";
            }
        }
        catch (JsonException)
        {
            return SanitizeProviderError(trimmed);
        }

        return SanitizeProviderError(trimmed);
    }

    private static string SanitizeProviderError(string message)
    {
        var sanitized = message.Trim();
        var sensitiveMarkers = new[]
        {
            "Received API Key",
            "Key Hash (Token)",
            "Unable to find token in cache",
            "LiteLLM_VerificationTokenTable"
        };

        foreach (var marker in sensitiveMarkers)
        {
            var index = sanitized.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                sanitized = sanitized[..index].Trim();
                break;
            }
        }

        sanitized = Regex.Replace(sanitized, @"sk-[A-Za-z0-9._-]+", "sk-...");
        sanitized = sanitized.Trim().TrimEnd(' ', ',', '.', ':');

        return string.IsNullOrWhiteSpace(sanitized)
            ? "The model endpoint rejected the supplied credentials."
            : $"{sanitized}.";
    }
}

public sealed class InsightServiceException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public string? ResponseBody { get; }

    public InsightServiceException(
        string message,
        HttpStatusCode? statusCode = null,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
