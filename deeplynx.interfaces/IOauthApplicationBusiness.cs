using deeplynx.models;

namespace deeplynx.interfaces;

public interface IOauthApplicationBusiness
{
    [Obsolete("V1-only. Used by deprecated v1 OAuth application endpoints. Superseded by GetAllOAuthApplicationsPaginated. " +
          "Remove once v1 OAuth application endpoints are sunset.", error: false)]
    Task<IEnumerable<OauthApplicationResponseDto>> GetAllOauthApplications(bool hideArchived = true);
    Task<PaginatedResponse<OauthApplicationResponseDto>> GetAllOauthApplicationsPaginated(PaginatedRequestDto paginatedRequestDto, bool hideArchived = true);
    Task<OauthApplicationResponseDto> GetOauthApplication(long applicationId, bool hideArchived = true);
    Task<OauthApplicationSecureResponseDto> CreateOauthApplication(
        CreateOauthApplicationRequestDto requestDto, long userId);
    Task<OauthApplicationResponseDto> UpdateOauthApplication(
        long applicationId, UpdateOauthApplicationRequestDto requestDto, long userId);
    Task<bool> ArchiveOauthApplication(long applicationId, long userId);
    Task<bool> UnarchiveOauthApplication(long applicationId, long userId);
    Task<bool> DeleteOauthApplication(long applicationId, long userId);
}