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

