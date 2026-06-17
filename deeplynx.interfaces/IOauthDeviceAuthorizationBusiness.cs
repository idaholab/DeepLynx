using deeplynx.models;

namespace deeplynx.interfaces;

public interface IOauthDeviceAuthorizationBusiness
{
    Task<DeviceAuthorizationResponseDto> CreateDeviceAuthorizationRequest(
        string? clientId,
        string? scope,
        string verificationUri);

    Task<OauthTokenGrantResponseDto> ExchangeDeviceCodeForToken(string? deviceCode, string? clientId);

    Task<OauthTokenGrantResponseDto> ExchangeRefreshTokenForToken(string? refreshToken, string? clientId);

    Task<DeviceVerificationLookupResponseDto> GetDeviceAuthorizationRequest(string? userCode);

    Task<DeviceVerificationLookupResponseDto> SetDeviceAuthorizationDecision(
        string? userCode,
        bool approve,
        long userId);

    Task<int> CleanupExpiredOrConsumedRequests();
}
