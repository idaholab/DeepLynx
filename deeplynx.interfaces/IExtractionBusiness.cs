using deeplynx.models;

namespace deeplynx.interfaces;

public interface IExtractionBusiness
{
    Task<ExtractionResponseDto> LatticeEntityStaging(
        long currentUserId,
        long organizationId,
        long projectId,
        long dataSourceId,
        CreateStagingRequestDto dto);

    Task<List<OntologySimilarityResultDto>> SearchOntologySimilarity(
        long recordId,
        long projectId,
        int limit = 5,
        string? termType = null);
}
