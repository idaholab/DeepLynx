using System.Runtime.CompilerServices;
using deeplynx.models;

namespace deeplynx.interfaces;

public interface IInsightBusiness
{
    Task QueueInsightUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? vlmModelConfigId,
        long? embeddingModelConfigId,
        InsightUploadApiRequestDto payload,
        string? userJwt = null);

    IAsyncEnumerable<string> StreamInsightQuery(
        long currentUserId,
        long organizationId,
        long projectId,
        long? languageModelConfigId,
        long? embeddingModelConfigId,
        InsightQueryApiRequestDto payload,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    Task<InsightIngestionStatusResponseDto> FetchInsightIngestionStatus(long recordId);

    Task<InsightEndpointHealthResponseDto> CheckEndpointHealth(
        long currentUserId,
        long organizationId,
        long projectId,
        long modelConfigId,
        string modelType);
    
    void TriggerEmbedding(
        long projectId,
        long recordId,
        string uri,
        AiModelConfigResponseDto.WithToken vlmConfig,
        AiModelConfigResponseDto.WithToken embeddingConfig,
        string? userJwt = null,
        bool overwrite = false);

    Task<AiModelConfigResponseDto.WithToken> ResolveModelConfig(
        long currentUserId,
        long organizationId,
        long projectId,
        long? modelConfigId,
        string modelType);

    bool IsSupportedFile(string fileType);
    Task QueueInsightEmbedStrings(
    long currentUserId,
    long organizationId,
    long projectId,
    long? embeddingModelConfigId);

}