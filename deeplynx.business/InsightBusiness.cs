using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using deeplynx.interfaces;
using deeplynx.models;

namespace deeplynx.business;

public class InsightBusiness : IInsightBusiness
{
    // Upload uses mixed casing — camelCase inside file_info items, snake_case everywhere else.
    // Query and status are fully snake_case.
    // We use explicit JsonPropertyName attributes on all request models to be precise.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly IAiModelConfigBusiness _aiModelConfigBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InsightBusiness" /> class.
    /// </summary>
    /// <param name="httpClient">
    ///     Typed HttpClient pre-configured with the Insight base URL in the Program.cs (URL is defined in the .env)
    /// </param>
    /// <param name="aiModelConfigBusiness">
    ///     Used to resolve LLM and embedding model configurations when explicit config IDs are not provided.
    /// </param>
    public InsightBusiness(HttpClient httpClient, IAiModelConfigBusiness aiModelConfigBusiness)
    {
        _httpClient = httpClient;
        _aiModelConfigBusiness = aiModelConfigBusiness;
    }

    /// <summary>
    ///     Fires an upload request to Insight and returns immediately.
    ///     Insight manages its own RabbitMQ queue internally, so embedding progress
    ///     can be tracked via <see cref="FetchInsightIngestionStatus"/> without blocking the caller.
    ///     Maps to POST /upload_document.
    /// </summary>
    /// <param name="currentUserId">The ID of the user making the request. Used to resolve model tokens when required.</param>
    /// <param name="organizationId">The ID of the organization. Used to scope model config resolution.</param>
    /// <param name="projectId">The ID of the project. Project-level model config defaults are preferred over org-level defaults.</param>
    /// <param name="llmModelConfigId">
    ///     Optional explicit LLM model config ID. If null, the default language model config for the org/project is used.
    /// </param>
    /// <param name="embeddingModelConfigId">
    ///     Optional explicit embedding model config ID. If null, the default embedding model config for the org/project is used.
    /// </param>
    /// <param name="payload">Upload payload from the caller.</param>
    /// <returns>Task that completes once Insight has acknowledged the request (2xx).</returns>
    /// <exception cref="InvalidOperationException">Thrown when Insight returns a non-success status, or when a required token is missing.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when a specified or default model config cannot be found.</exception>
    public async Task QueueInsightUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? llmModelConfigId,
        long? embeddingModelConfigId,
        InsightUploadRequestDto payload)
    {
        var llmConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, llmModelConfigId, "language");
        var embeddingConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");

        var body = new InsightUploadRequestBody
        {
            FileInfo = payload.FileInfo.Select(f => new InsightUploadFileInfoBody
            {
                FileId = f.FileId,
                FileUri = NormalizeFileUri(f.FileUri)
            }).ToList(),
            LlmServerUrl = llmConfig.ServerUrl,
            LlmModelName = llmConfig.ModelName,
            LlmAuthToken = llmConfig.Token,
            EmbeddingServerUrl = embeddingConfig.ServerUrl,
            EmbeddingModelName = embeddingConfig.ModelName,
            EmbeddingAuthToken = embeddingConfig.Token
        };

        var response = await _httpClient.PostAsync("/upload_document", Serialize(body));

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(err) ? "Insight upload failed" : err);
        }

        // Response body intentionally not read — Insight queues the work
        // internally via RabbitMQ. Poll FetchInsightIngestionStatus for progress.
    }

    /// <summary>
    ///     Streams a RAG query response from the Insight API chunk by chunk.
    ///     Maps to POST /query.
    /// </summary>
    /// <param name="currentUserId">The ID of the user making the request. Used to resolve model tokens when required.</param>
    /// <param name="organizationId">The ID of the organization. Used to scope model config resolution.</param>
    /// <param name="projectId">The ID of the project. Project-level model config defaults are preferred over org-level defaults.</param>
    /// <param name="llmModelConfigId">
    ///     Optional explicit LLM model config ID. If null, the default language model config for the org/project is used.
    /// </param>
    /// <param name="embeddingModelConfigId">
    ///     Optional explicit embedding model config ID. If null, the default embedding model config for the org/project is used.
    /// </param>
    /// <param name="payload">Query payload from the caller.</param>
    /// <param name="cancellationToken">Token to cancel the streaming operation.</param>
    /// <returns>
    ///     An async enumerable of string chunks as they stream from Insight.
    ///     Forward these directly to the HTTP response stream in your controller.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when Insight returns a non-success status, or when a required token is missing.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when a specified or default model config cannot be found.</exception>
    public async IAsyncEnumerable<string> StreamInsightQuery(
        long currentUserId,
        long organizationId,
        long projectId,
        long? llmModelConfigId,
        long? embeddingModelConfigId,
        InsightQueryRequestDto payload,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Resolve configs before entering the iterator — exceptions cannot be thrown
        // directly from inside an async iterator (they would be swallowed until enumeration).
        var llmConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, llmModelConfigId, "language");
        var embeddingConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");

        await foreach (var chunk in StreamInsightQueryCore(llmConfig, embeddingConfig, payload, cancellationToken))
            yield return chunk;
    }

    /// <summary>
    ///     Fetches the ingestion status of a previously queued document from Insight.
    ///     Maps to GET /ingestion_status/{recordId}.
    /// </summary>
    /// <param name="recordId">The Insight file ID to check.</param>
    /// <returns>The parsed ingestion status from Insight.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Insight returns a non-success status.</exception>
    public async Task<InsightIngestionStatusResponseDto> FetchInsightIngestionStatus(long recordId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/ingestion_status/{recordId}");
        request.Headers.Accept.Add(new("application/json"));
        var response = await _httpClient.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(text) ? "Insight status check failed" : text);

        return JsonSerializer.Deserialize<InsightIngestionStatusResponseDto>(text, JsonOptions)
               ?? throw new InvalidOperationException("Insight status returned an empty response");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Resolves a model configuration by explicit ID or falls back to the org/project default for the given model type.
    ///     Throws if the model requires a token but none is stored for the user.
    /// </summary>
    /// <param name="currentUserId">The ID of the user making the request.</param>
    /// <param name="organizationId">The organization scope for the lookup.</param>
    /// <param name="projectId">The project scope. Project-level defaults take priority over org-level.</param>
    /// <param name="modelConfigId">
    ///     An explicit model config ID to look up. When null, the default config for
    ///     <paramref name="modelType"/> is resolved instead.
    /// </param>
    /// <param name="modelType">The model type string used when falling back to the default (e.g. "language" or "embedding").</param>
    /// <returns>The resolved <see cref="AiModelConfigResponseDto"/>, with <c>Token</c> populated if applicable.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the config or its default cannot be found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a token is required but not found for the user.</exception>
    private async Task<AiModelConfigResponseDto> ResolveModelConfig(
        long currentUserId,
        long organizationId,
        long projectId,
        long? modelConfigId,
        string modelType)
    {
        var config = modelConfigId.HasValue
            ? await _aiModelConfigBusiness.GetAiModelConfigWithToken(currentUserId, organizationId, projectId, modelConfigId.Value)
            : await _aiModelConfigBusiness.GetDefaultAiModelConfig(currentUserId, organizationId, projectId, modelType);

        if (config.RequiresToken == true && string.IsNullOrWhiteSpace(config.Token))
            throw new InvalidOperationException(
                $"The {modelType} model configuration (ID: {config.Id}) requires an API token, " +
                $"but none was found for user {currentUserId}. Please add a token for this model.");

        return config;
    }

    /// <summary>
    ///     Inner async iterator for <see cref="StreamInsightQuery"/>. Separated so that config resolution
    ///     (and any exceptions it may throw) happens before enumeration begins.
    /// </summary>
    private async IAsyncEnumerable<string> StreamInsightQueryCore(
        AiModelConfigResponseDto llmConfig,
        AiModelConfigResponseDto embeddingConfig,
        InsightQueryRequestDto payload,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sp = payload.SamplingParameters;
        var body = new InsightQueryRequestBody
        {
            Question = payload.Question,
            FileIds = payload.FileIds,
            SamplingParameters = new InsightSamplingParametersBody
            {
                Temperature = sp?.Temperature ?? 0.1,
                MaxTokens = sp?.MaxTokens ?? 1024,
                TopP = sp?.TopP ?? 0.9
            },
            LlmServerUrl = llmConfig.ServerUrl,
            LlmModelName = llmConfig.ModelName,
            LlmAuthToken = llmConfig.Token,
            EmbeddingServerUrl = embeddingConfig.ServerUrl,
            EmbeddingModelName = embeddingConfig.ModelName,
            EmbeddingAuthToken = embeddingConfig.Token
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/query")
        {
            Content = Serialize(body),
            Headers = { Accept = { new("text/plain") } }
        };

        // ResponseHeadersRead lets us start consuming the body before it finishes arrives,
        // which is what enables real streaming.
        var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(err) ? "Insight query failed" : err);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[4096];

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var charsRead = await reader.ReadAsync(buffer, cancellationToken);
            if (charsRead == 0) break;
            yield return new string(buffer, 0, charsRead);
        }
    }

    private static StringContent Serialize<T>(T obj) =>
        new(JsonSerializer.Serialize(obj, JsonOptions), Encoding.UTF8, "application/json");

    /// <summary>
    ///     Mirrors the normalizeInsightFileUri logic from the frontend.
    ///     Ensures URIs are either fully-qualified (http/https/etc.) or rooted at /data/.
    /// </summary>
    private static string NormalizeFileUri(string fileUri)
    {
        var trimmed = fileUri.Trim();
        if (string.IsNullOrEmpty(trimmed)) return trimmed;

        // Already a fully-qualified URI (http://, s3://, etc.)
        if (Regex.IsMatch(trimmed, @"^[a-z][a-z0-9+.\-]*:\/\/", RegexOptions.IgnoreCase))
            return trimmed;

        // Already rooted at /data/
        if (trimmed.StartsWith("/data/"))
            return trimmed;

        // Nexus org-scoped path starting with org_
        if (trimmed.StartsWith("org_"))
            return $"/data/{trimmed}";

        // Path containing /org_ somewhere in the middle
        var orgIdx = trimmed.IndexOf("/org_", StringComparison.Ordinal);
        if (orgIdx >= 0)
            return $"/data{trimmed[orgIdx..]}";

        return trimmed;
    }
}

// -------------------------------------------------------------------------
// Insight API request bodies — wire format only, private to this file.
// These match what the Insight API expects exactly. Do not expose these
// outside InsightBusiness; use the public DTOs for your API's contract.
// -------------------------------------------------------------------------

/// <summary>
///     Wire body for POST /upload_document.
///     Top-level fields are snake_case; file_info items use camelCase —
///     this is intentional and matches what the Insight API expects.
/// </summary>
file sealed class InsightUploadRequestBody
{
    [JsonPropertyName("file_info")]
    public List<InsightUploadFileInfoBody> FileInfo { get; set; } = [];

    [JsonPropertyName("llm_server_url")]
    public string? LlmServerUrl { get; set; }

    [JsonPropertyName("llm_model_name")]
    public string? LlmModelName { get; set; }

    [JsonPropertyName("llm_auth_token")]
    public string? LlmAuthToken { get; set; }

    [JsonPropertyName("embedding_server_url")]
    public string? EmbeddingServerUrl { get; set; }

    [JsonPropertyName("embedding_model_name")]
    public string? EmbeddingModelName { get; set; }

    [JsonPropertyName("embedding_auth_token")]
    public string? EmbeddingAuthToken { get; set; }
}

/// <summary>
///     Individual file entry inside file_info.
///     Insight expects camelCase for these fields specifically.
/// </summary>
file sealed class InsightUploadFileInfoBody
{
    [JsonPropertyName("fileId")]
    public long FileId { get; set; }

    [JsonPropertyName("fileURI")]
    public string FileUri { get; set; } = string.Empty;
}

/// <summary>Wire body for POST /query.</summary>
file sealed class InsightQueryRequestBody
{
    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("file_ids")]
    public long[]? FileIds { get; set; }

    [JsonPropertyName("sampling_parameters")]
    public InsightSamplingParametersBody SamplingParameters { get; set; } = new();

    [JsonPropertyName("llm_server_url")]
    public string? LlmServerUrl { get; set; }

    [JsonPropertyName("llm_model_name")]
    public string? LlmModelName { get; set; }

    [JsonPropertyName("llm_auth_token")]
    public string? LlmAuthToken { get; set; }

    [JsonPropertyName("embedding_server_url")]
    public string? EmbeddingServerUrl { get; set; }

    [JsonPropertyName("embedding_model_name")]
    public string? EmbeddingModelName { get; set; }

    [JsonPropertyName("embedding_auth_token")]
    public string? EmbeddingAuthToken { get; set; }
}

file sealed class InsightSamplingParametersBody
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.1;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1024;

    [JsonPropertyName("top_p")]
    public double TopP { get; set; } = 0.9;
}