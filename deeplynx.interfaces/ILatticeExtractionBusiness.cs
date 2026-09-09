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

    Task<bool> ProcessExtractionProgress(
        long projectId,
        long extractionId,
        InsightExtractionProgressCombinedDto progressDto);

    Task MarkExtractionFailed(long extractionId, long organizationId, long projectId, string? errorMessage = null);

    Task<ExtractionStagingResponseDto> GetExtractionStaging(long extractionId);

    Task<ExtractionStagingResponseDto> GetExtractionStaging(long extractionId, long organizationId, long projectId);

    Task<ExtractionResponseDto> PromoteExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long extractionId,
        PromoteExtractionRequestDto request);

    Task<ExtractionResponseDto> RejectExtraction(long extractionId, RejectExtractionRequestDto request);

    Task<EmbeddingStatusResponseDto> GetEmbeddingStatus(long projectId);

    [Obsolete("V1-only. Used by deprecated v1 lattice extraction endpoints. Superseded by ListExtractionsByProjectPaginated. " +
              "Remove once v1 lattice extraction endpoints are sunset.", error: false)]
    Task<List<ExtractionListItemDto>> ListExtractionsByProject(long projectId);
    Task<PaginatedResponse<ExtractionListItemDto>> ListExtractionsByProjectPaginated(
        long projectId,
        PaginatedRequestDto paginatedRequestDto);

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
