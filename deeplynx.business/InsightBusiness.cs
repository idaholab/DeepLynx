using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

public class InsightBusiness : IInsightBusiness
{
    /// <summary>
    ///     File types supported for embedding by the Insight service.
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedFileTypes = new HashSet<string>
    {
        "pdf", "txt", "html", "htm"
    };

    private readonly DeeplynxContext _context;
    private readonly InsightServiceClient _insightServiceClient;
    private readonly IAiModelConfigBusiness _aiModelConfigBusiness;
    private readonly ILogger<InsightBusiness> _logger;
    private readonly DeeplynxContext _context;

    private readonly ISensitivityLabelService _sensitivityLabelService;

    public InsightBusiness(
        DeeplynxContext context,
        InsightServiceClient insightServiceClient,
        IAiModelConfigBusiness aiModelConfigBusiness,
        ILogger<InsightBusiness> logger, DeeplynxContext context,
        ISensitivityLabelService sensitivityLabelService)
    {
        _context = context;
        _insightServiceClient = insightServiceClient;
        _aiModelConfigBusiness = aiModelConfigBusiness;
        _logger = logger;
        _context = context;
        _sensitivityLabelService = sensitivityLabelService;
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
    /// <param name="payload">Upload dto from the caller containing file IDs and URIs.</param>
    /// <exception cref="InvalidOperationException">Thrown when Insight returns a non-success status, or when a required token is missing.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when a specified or default model config cannot be found.</exception>
    public async Task QueueInsightUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? vlmModelConfigId,
        long? embeddingModelConfigId,
        InsightUploadApiRequestDto payload)
    {
        var vlmConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, vlmModelConfigId, "vlm");
        var embeddingConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");

        var recordIds = payload.FileInfo.Select(f => f.FileId).ToList();

        var authorizedIds = await _sensitivityLabelService
            .FilterAuthorizedRecordIds(currentUserId, organizationId, projectId, recordIds, _context);

        var authorizedFileInfo = payload.FileInfo
            .Where(f => authorizedIds.Contains(f.FileId))
            .ToList();

        var request = new InsightUploadRequestDto
        {
            FileInfo = authorizedFileInfo.Select(f => new InsightUploadRequestDto.FileInfoDto
            {
                FileId = f.FileId,
                FileUri = NormalizeFileUri(f.FileUri)
            }).ToList(),
            VlmServerUrl = vlmConfig.ServerUrl,
            VlmName = vlmConfig.ModelName,
            VlmToken = vlmConfig.Token,
            EmbeddingServerUrl = embeddingConfig.ServerUrl,
            EmbeddingModelName = embeddingConfig.ModelName,
            EmbeddingModelToken = embeddingConfig.Token,
            Overwrite = false // this endpoint will never be used for updates
        };

        await _insightServiceClient.Upload(request);
    }

    /// <summary>
    ///     Fire-and-forget wrapper around <see cref="QueueInsightUpload"/>.
    ///     Errors are logged rather than propagated so the caller is not blocked.
    ///     Intended for use after file uploads and updates where embedding should
    ///     happen asynchronously without affecting the response to the caller.
    /// </summary>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="recordId">The ID of the record to embed.</param>
    /// <param name="uri">The URI of the file to embed.</param>
    /// <param name="vlmConfig">Optional explicit VLM model config ID. If null, the project/org default is used.</param>
    /// <param name="embeddingConfig">Optional explicit embedding model config ID. If null, the project/org default is used.</param>
    /// <param name="overwrite">Whether to overwrite an existing embedding for this record.</param>
    public void TriggerEmbedding(
        long projectId,
        long recordId,
        string uri,
        AiModelConfigResponseDto vlmConfig,
        AiModelConfigResponseDto embeddingConfig,
        bool overwrite = false)
    {
        var request = new InsightUploadRequestDto
        {
            FileInfo = [new() { FileId = recordId, FileUri = NormalizeFileUri(uri) }],
            Overwrite = overwrite,
            VlmServerUrl = vlmConfig.ServerUrl,
            VlmName = vlmConfig.ModelName,
            VlmToken = vlmConfig.Token,
            EmbeddingServerUrl = embeddingConfig.ServerUrl,
            EmbeddingModelName = embeddingConfig.ModelName,
            EmbeddingModelToken = embeddingConfig.Token
        };

        _ = _insightServiceClient.Upload(request)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.LogError(t.Exception,
                        "Insight enqueue failed for record {RecordId} in project {ProjectId}",
                        recordId, projectId);
            }, TaskContinuationOptions.None);
    }

    /// <summary>
    ///     Streams a RAG query response from the Insight API chunk by chunk.
    ///     Maps to POST /query. Language model type can be LLM or VLM.
    /// </summary>
    /// <param name="currentUserId">The ID of the user making the request. Used to resolve model tokens when required.</param>
    /// <param name="organizationId">The ID of the organization. Used to scope model config resolution.</param>
    /// <param name="projectId">The ID of the project. Project-level model config defaults are preferred over org-level defaults.</param>
    /// <param name="languageModelConfigId">
    ///     Optional explicit LLM model config ID. If null, the default LLM config is used,
    ///     falling back to the default VLM config if no LLM is configured.
    /// </param>
    /// <param name="embeddingModelConfigId">
    ///     Optional explicit embedding model config ID. If null, the default embedding config for the org/project is used.
    /// </param>
    /// <param name="payload">Query payload containing the question, file IDs, and sampling parameters.</param>
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
        InsightQueryApiRequestDto payload,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var llmConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, languageModelConfigId, "llm");
        var embeddingConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");

        var authorizedIds = await _sensitivityLabelService
            .FilterAuthorizedRecordIds(currentUserId, organizationId, projectId, payload.FileIds, _context);

        payload.FileIds = payload.FileIds
            .Where(id => authorizedIds.Contains(id))
            .ToArray();

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

    /// <summary>
    ///     Returns whether the given file type is supported for embedding by Insight.
    ///     Check is case-insensitive.
    /// </summary>
    /// <param name="fileType">File extension without the leading dot (e.g. "pdf", "txt").</param>
    public bool IsSupportedFile(string fileType) =>
        SupportedFileTypes.Contains(fileType, StringComparer.OrdinalIgnoreCase);

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
    /// <param name="modelType">The model type string used when falling back to the default (e.g. "llm", "vlm", or "embedding").</param>
    /// <returns>The resolved <see cref="AiModelConfigResponseDto"/>, with <c>Token</c> populated if applicable.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the config or its default cannot be found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a token is required but not found for the user.</exception>
    public async Task<AiModelConfigResponseDto> ResolveModelConfig(
        long currentUserId,
        long organizationId,
        long projectId,
        long? modelConfigId,
        string modelType)
    {
        AiModelConfigResponseDto config;

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
    
    /// <summary>
    ///     Queues embedding jobs for all class and relationship descriptions in the project.
    ///     Fetches descriptions from the database, publishes each as an OntologyMessage to Insight's
    ///     RabbitMQ ontology_queue, and returns immediately. Maps to POST /embed_strings.
    /// </summary>
    /// <param name="currentUserId">The ID of the user making the request. Used to resolve model tokens when required.</param>
    /// <param name="organizationId">The ID of the organization. Used to scope model config resolution.</param>
    /// <param name="projectId">The ID of the project whose class and relationship descriptions will be embedded.</param>
    /// <param name="embeddingModelConfigId">
    ///     Optional explicit embedding model config ID. If null, the project/org default is used.
    ///     If no default is configured, Insight falls back to its own environment defaults.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when a required model token is missing.</exception>
    public async Task QueueInsightEmbedStrings(
        long currentUserId,
        long organizationId,
        long projectId,
        long? embeddingModelConfigId)
    {
        string? serverUrl = null;
        string? modelName = null;
        string? token = null;

        try
        {
            var embeddingConfig = await ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");
            serverUrl = embeddingConfig.ServerUrl;
            modelName = embeddingConfig.ModelName;
            token = embeddingConfig.Token;
        }
        catch (KeyNotFoundException)
        {
            // No default configured — Insight will fall back to its own ENV vars
        }

        var classEmbeds = await _context.Classes
            .Where(c => c.ProjectId == projectId && !string.IsNullOrEmpty(c.Description))
            .Select(c => new InsightEmbedStringRequestDto.EmbedStringDto
            {
                ClassId = c.Id,
                Text = c.Description!
            })
            .ToListAsync();

        var relationshipEmbeds = await _context.Relationships
            .Where(r => r.ProjectId == projectId && !string.IsNullOrEmpty(r.Description))
            .Select(r => new InsightEmbedStringRequestDto.EmbedStringDto
            {
                RelationshipId = r.Id,
                Text = r.Description!
            })
            .ToListAsync();

        var request = new InsightEmbedStringRequestDto
        {
            EmbedStringInfo = classEmbeds.Concat(relationshipEmbeds).ToList(),
            EmbeddingServerUrl = serverUrl,
            EmbeddingModelName = modelName,
            EmbeddingModelToken = token,
        };

        await _insightServiceClient.EmbedStrings(request);
    }

    private async IAsyncEnumerable<string> StreamInsightQueryCore(
        AiModelConfigResponseDto llmConfig,
        AiModelConfigResponseDto embeddingConfig,
        InsightQueryApiRequestDto payload,
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

        if (Regex.IsMatch(trimmed, @"^[a-z][a-z0-9+.\-]*:\/\/", RegexOptions.IgnoreCase))
            return trimmed;

        if (trimmed.StartsWith("/data/"))
            return trimmed;

        if (trimmed.StartsWith("org_"))
            return $"/data/{trimmed}";

        var orgIdx = trimmed.IndexOf("/org_", StringComparison.Ordinal);
        if (orgIdx >= 0)
            return $"/data{trimmed[orgIdx..]}";

        return trimmed;
    }
}