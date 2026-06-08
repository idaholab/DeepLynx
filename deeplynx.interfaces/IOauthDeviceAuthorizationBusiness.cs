namespace deeplynx.interfaces;

public interface IOauthDeviceAuthorizationBusiness
{
    Task<int> CleanupExpiredOrConsumedRequests();
}
