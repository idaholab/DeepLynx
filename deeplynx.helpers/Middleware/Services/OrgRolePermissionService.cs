using deeplynx.datalayer.Models;
using deeplynx.helpers.Cache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers;

//Keeping this interface in the same file as the service
public interface IOrgRolePermissionService
{ 
    Task<bool> PermissionInOrg(long userId, long orgId, string action, string resource);
}

public class OrgRolePermissionService : IOrgRolePermissionService
{
    private readonly DeeplynxContext _dbContext;
    private readonly ILogger<OrgRolePermissionService> _logger;

    public OrgRolePermissionService(
        DeeplynxContext dbContext, 
        ILogger<OrgRolePermissionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> PermissionInOrg(
        long userId, 
        long orgId, 
        string action, 
        string resource)
    {
        bool hasPermission;

        _logger.LogInformation(
            "Checking permission - User: {UserId}, Organization: {OrgId}, Action: {Action}, Resource: {Resource}",
            userId, orgId, action, resource);
        
        // Check the cache before querying the db
        var cacheKey = CacheKeys.OrgPermission(userId, orgId, action, resource);
        var cached = await CacheService.Instance.GetAsync<bool?>(cacheKey);
        if (cached.HasValue)
        {
            hasPermission = cached.Value;
        }
        else
        {
            hasPermission = _dbContext.Database
                .SqlQuery<bool>($@"
                    SELECT EXISTS(
                        SELECT 1
                        FROM deeplynx.organization_users ou
                        WHERE ou.user_id = {userId}
                        AND ou.organization_id = {orgId}
                        AND (ou.is_org_admin = true OR {action} = 'read')) as has_permission")
                    .AsEnumerable()
                    .FirstOrDefault();

            // Populate cache on miss
            await CacheService.Instance.SetAsync(cacheKey, hasPermission, (TimeSpan?)null);
        }

        if (hasPermission)
        {
            _logger.LogInformation(
                "Permission granted (group) - User: {UserId}, Organization: {OrgId}, Action: {Action}, Resource: {Resource}",
                userId, orgId, action, resource);
        }
        else
        {
            _logger.LogWarning(
                "Permission denied - User: {UserId}, Organization: {OrgId}, Action: {Action}, Resource: {Resource}",
                userId, orgId, action, resource);
        }

        return hasPermission;
    }
    
}