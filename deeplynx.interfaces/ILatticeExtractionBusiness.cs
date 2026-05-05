using deeplynx.models;

namespace deeplynx.interfaces;

public interface ILatticeExtractionBusiness
{
    Task<ExtractionResponseDto> ProcessInsightExtractionCallback(
        long organizationId,
        long projectId,
        long dataSourceId,
        long extractionId,
        InsightExtractionCallbackDto dto);

    Task MarkExtractionFailed(long extractionId, string? errorMessage = null);

    Task<ExtractionStagingResponseDto> GetExtractionStaging(long extractionId);

    Task<ExtractionResponseDto> PromoteExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long extractionId,
        bool approve);

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

