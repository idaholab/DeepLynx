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
        InsightUploadRequestDto payload);

    IAsyncEnumerable<string> StreamInsightQuery(
        long currentUserId,
        long organizationId,
        long projectId,
        long? languageModelConfigId,
        long? embeddingModelConfigId,
        InsightQueryRequestDto payload,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    Task<InsightIngestionStatusResponseDto> FetchInsightIngestionStatus(long recordId);
}