using deeplynx.models;

namespace deeplynx.interfaces;

public interface IOauthDeviceAuthorizationBusiness
{
    Task<DeviceAuthorizationResponseDto> CreateDeviceAuthorizationRequest(
        string? clientId,
        string? scope,
        string verificationUri);

    Task<int> CleanupExpiredOrConsumedRequests();
}
