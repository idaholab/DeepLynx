using deeplynx.models;

namespace deeplynx.interfaces;

public interface IExtractionBusiness
{
    Task<ExtractionResponseDto> CreateExtraction(
        long currentUserId,
        long organizationId,
        long projectId,
        long dataSourceId,
        CreateExtractionRequestDto dto);
}
