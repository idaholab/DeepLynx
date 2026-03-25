using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using deeplynx.interfaces;
using deeplynx.models;

namespace deeplynx.business;

public class InsightBusiness : IInsightBusiness
{
    private readonly InsightServiceClient _insightServiceClient;
    private readonly IAiModelConfigBusiness _aiModelConfigBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InsightBusiness" /> class.
    /// </summary>
    /// <param name="insightServiceClient">Used to make requests to the Insight API.</param>
    /// <param name="aiModelConfigBusiness">
    ///     Used to resolve LLM and embedding model configurations when explicit config IDs are not provided.
    /// </param>
    public InsightBusiness(InsightServiceClient insightServiceClient, IAiModelConfigBusiness aiModelConfigBusiness)
    {
        _insightServiceClient = insightServiceClient;
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
    /// <param name="vlmModelConfigId">
    ///     Optional explicit VLM model config ID. If null, the default VLM config for the org/project is used.
    /// </param>
    /// <param name="embeddingModelConfigId">
    ///     Optional explicit embedding model config ID. If null, the default embedding config for the org/project is used.
    /// </param>
    /// <param name="payload">Upload payload from the caller.</param>
    /// <returns>Task that completes once Insight has acknowledged the request (2xx).</returns>
    /// <exception cref="InvalidOperationException">Thrown when Insight returns a non-success status, or when a required token is missing.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when a specified or default model config cannot be found.</exception>
    public async Task QueueInsightUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? vlmModelConfigId,
        long? embeddingModelConfigId,
        InsightUploadRequestDto payload)
    {
        var vlmConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, vlmModelConfigId, "vlm");
        var embeddingConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");

        var request = new InsightUploadRequestDto
        {
            FileInfo = payload.FileInfo.Select(f => new InsightUploadRequestDto.FileInfoDto
            {
                FileId = f.FileId,
                FileUri = NormalizeFileUri(f.FileUri)
            }).ToList(),
            VlmServerUrl = vlmConfig.ServerUrl,
            VlmName = vlmConfig.ModelName,
            VlmToken = vlmConfig.Token,
            EmbeddingServerUrl = embeddingConfig.ServerUrl,
            EmbeddingModelName = embeddingConfig.ModelName,
            EmbeddingModelToken = embeddingConfig.Token
        };

        await _insightServiceClient.Upload(request);

        // Response body intentionally not read — Insight queues the work
        // internally via RabbitMQ. Poll FetchInsightIngestionStatus for progress.
    }

    /// <summary>
    ///     Streams a RAG query response from the Insight API chunk by chunk.
    ///     Maps to POST /query. Language model type can be LLM or VLM.
    /// </summary>
    /// <param name="currentUserId">The ID of the user making the request. Used to resolve model tokens when required.</param>
    /// <param name="organizationId">The ID of the organization. Used to scope model config resolution.</param>
    /// <param name="projectId">The ID of the project. Project-level model config defaults are preferred over org-level defaults.</param>
    /// <param name="languageModelConfigId">
    ///     Optional explicit LLM model config ID. If null, the default language model config for the org/project is used.
    /// </param>
    /// <param name="embeddingModelConfigId">
    ///     Optional explicit embedding model config ID. If null, the default embedding config for the org/project is used.
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
        long? languageModelConfigId,
        long? embeddingModelConfigId,
        InsightQueryRequestDto payload,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var llmConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, languageModelConfigId, "llm");
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
    public Task<InsightIngestionStatusResponseDto> FetchInsightIngestionStatus(long recordId)
    {
        return _insightServiceClient.GetIngestionStatus(recordId);
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
    /// <param name="modelType">The model type string used when falling back to the default (e.g. "llm" or "embedding").</param>
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
        AiModelConfigResponseDto config;

        // If requesting the default LLM and none exists, fallback to the default VLM.
        // (This scenario is expected only for insight queries.)
        if (!modelConfigId.HasValue && modelType == "llm")
        {
            try
            {
                config = await _aiModelConfigBusiness.GetDefaultAiModelConfig(
                    currentUserId, organizationId, projectId, "llm");
            }
            catch (KeyNotFoundException)
            {
                config = await _aiModelConfigBusiness.GetDefaultAiModelConfig(
                    currentUserId, organizationId, projectId, "vlm");
            }
        }
        // Otherwise:
        // - If a modelConfigId is provided, fetch that specific config
        // - If not, fetch the default config for the requested modelType
        else
        {
            config = modelConfigId.HasValue
                ? await _aiModelConfigBusiness.GetAiModelConfigWithToken(currentUserId, organizationId, projectId,
                    modelConfigId.Value)
                : await _aiModelConfigBusiness.GetDefaultAiModelConfig(currentUserId, organizationId, projectId,
                    modelType);
        }

        if (config.RequiresToken == true && string.IsNullOrWhiteSpace(config.Token))
            throw new InvalidOperationException(
                $"The {modelType} model configuration (ID: {config.Id}) requires an API token, " +
                $"but none was found for user {currentUserId}. Please add a token for this model.");

        return config;
    }

    private async IAsyncEnumerable<string> StreamInsightQueryCore(
        AiModelConfigResponseDto llmConfig,
        AiModelConfigResponseDto embeddingConfig,
        InsightQueryRequestDto payload,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sp = payload.SamplingParameters;
        var request = new InsightQueryRequestDto
        {
            Question = payload.Question,
            FileIds = payload.FileIds,
            SamplingParameters = new InsightSamplingParametersDto
            {
                Temperature = sp?.Temperature ?? 0.7,
                MaxTokens = sp?.MaxTokens ?? 1024,
                TopP = sp?.TopP ?? 0.9
            },
            LlmServerUrl = llmConfig.ServerUrl,
            LlmName = llmConfig.ModelName,
            LlmToken = llmConfig.Token,
            EmbeddingServerUrl = embeddingConfig.ServerUrl,
            EmbeddingModelName = embeddingConfig.ModelName,
            EmbeddingModelToken = embeddingConfig.Token
        };

        // EnsureSuccessStatusCode is called inside Query; no need to re-check here.
        var stream = await _insightServiceClient.Query(request);

        using var reader = new StreamReader(stream);
        var buffer = new char[4096];

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var charsRead = await reader.ReadAsync(buffer, cancellationToken);
            if (charsRead == 0) break;
            yield return new string(buffer, 0, charsRead);
        }
    }

    /// <summary>
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