using deeplynx.models;

namespace deeplynx.interfaces;

public interface ILatticeExtractionBusiness
{
    Task<ExtractionResponseDto> ProcessInsightCallback(
        long organizationId,
        long projectId,
        long dataSourceId,
        long extractionId,
        InsightExtractionCallbackDto dto);

    Task MarkExtractionFailed(long extractionId, long organizationId, long projectId, string? errorMessage = null);

    Task<ExtractionStagingResponseDto> GetExtractionStaging(long extractionId);

    Task<ExtractionStagingResponseDto> GetExtractionStaging(long extractionId, long organizationId, long projectId);

    Task<ExtractionResponseDto> PromoteExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long extractionId,
        bool approve);

    Task<EmbeddingStatusResponseDto> GetEmbeddingStatus(long projectId);

    Task<List<ExtractionListItemDto>> ListExtractionsByProject(long projectId);

    Task<List<OntologySimilarityResultDto>> SearchOntologySimilarity(
        long recordId,
        long projectId,
        long limit);

    Task<long> TriggerLatticeExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        string mode);
}
