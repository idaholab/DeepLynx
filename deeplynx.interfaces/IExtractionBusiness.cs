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
}
