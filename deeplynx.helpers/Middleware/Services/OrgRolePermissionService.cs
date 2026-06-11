using deeplynx.datalayer.Models;
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
        _logger.LogInformation(
            "Checking permission - User: {UserId}, Organization: {OrgId}, Action: {Action}, Resource: {Resource}",
            userId, orgId, action, resource);
        
        var isOrgAdmin = _dbContext.Database
            .SqlQuery<bool>($@"
             SELECT EXISTS(
                SELECT 1
                FROM deeplynx.organization_users ou
                WHERE ou.user_id = {userId}
                  AND ou.organization_id = {orgId}
                  AND ou.is_org_admin = true) as has_permission")
            .AsEnumerable()
            .FirstOrDefault();

        var hasPermission = isOrgAdmin;

        if (!hasPermission && string.Equals(action, "read", StringComparison.OrdinalIgnoreCase))
        {
            hasPermission = _dbContext.Database
                .SqlQuery<bool>($@"
             SELECT EXISTS(
                SELECT 1
                FROM deeplynx.organization_users ou
                WHERE ou.user_id = {userId}
                  AND ou.organization_id = {orgId}) as has_permission")
                .AsEnumerable()
                .FirstOrDefault();
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