using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

public class OauthDeviceAuthorizationBusiness : IOauthDeviceAuthorizationBusiness
{
    private readonly DeeplynxContext _context;
    private readonly ILogger<OauthDeviceAuthorizationBusiness> _logger;

    public OauthDeviceAuthorizationBusiness(
        DeeplynxContext context,
        ILogger<OauthDeviceAuthorizationBusiness> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CleanupExpiredOrConsumedRequests()
    {
        var nowWithoutTz = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var requests = await _context.OauthDeviceAuthorizationRequests
            .Where(request => request.ExpiresAt <= nowWithoutTz
                              || request.Status == OauthDeviceAuthorizationStatus.Consumed)
            .ToListAsync();

        if (requests.Count == 0)
        {
            return 0;
        }

        _context.OauthDeviceAuthorizationRequests.RemoveRange(requests);

        var deleted = await _context.SaveChangesAsync();
        _logger.LogInformation("Cleaned up {RequestCount} OAuth device authorization requests", requests.Count);

        return deleted;
    }
}
