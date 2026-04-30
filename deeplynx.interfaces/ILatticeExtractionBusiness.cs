using deeplynx.models;

namespace deeplynx.interfaces;

public interface ILatticeExtractionBusiness
{
    Task<ExtractionResponseDto> LatticeEntityStaging(
        long currentUserId,
        long organizationId,
        long projectId,
        long dataSourceId,
        CreateStagingRequestDto dto,
        long? extractionId = null);

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

