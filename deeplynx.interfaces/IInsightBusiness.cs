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
        InsightUploadApiRequestDto payload);

    IAsyncEnumerable<string> StreamInsightQuery(
        long currentUserId,
        long organizationId,
        long projectId,
        long? languageModelConfigId,
        long? embeddingModelConfigId,
        InsightQueryApiRequestDto payload,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    Task<InsightIngestionStatusResponseDto> FetchInsightIngestionStatus(long recordId);

    void TriggerEmbedding(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        string uri,
        long? vlmConfigId = null,
        long? embeddingModelConfigId = null,
        bool overwrite = false);

    bool IsSupportedFile(string fileType);

}