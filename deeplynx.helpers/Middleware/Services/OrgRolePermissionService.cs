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
        
        // Organization admins have full permission to every action/resource in the org.
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

        // There is no org-level user->role assignment in the schema (organization_users only
        // carries is_org_admin), so granular per-user permissions cannot be evaluated at the org
        // level. Non-admin members are therefore granted read-only access to org-scoped resources;
        // any create/update/delete (write/update) action requires org admin.
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