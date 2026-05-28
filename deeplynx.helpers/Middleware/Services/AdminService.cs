using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers;

//Keeping this interface in the same file as the service
public interface IAdminService
{
    Task<bool> SysAdminCheck(long userId);

    Task<bool> OrgAdminCheck(long userId, long organizationId);

    Task<bool> ProjectAdminCheck(long userId, long organizationId, List<long> projectIds);

    Task<bool> OrgAdminInSystemCheck(long userId);
}

public class AdminService : IAdminService
{
    private readonly DeeplynxContext _dbContext;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        DeeplynxContext dbContext,
        ILogger<AdminService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> SysAdminCheck(
        long userId)
    {

        //check for whether a user has permission to an action/resource within a organization through group membership
        var hasPermission = _dbContext.Database
            .SqlQuery<bool>($@"
             SELECT EXISTS(
                SELECT 1
                FROM deeplynx.users u
                WHERE u.id = {userId}
                  AND u.is_sys_admin = true
                ) as has_permission")
            .AsEnumerable()
            .FirstOrDefault();

        if (hasPermission)
            _logger.LogInformation(
                "Permission granted - User: {UserId}",
                userId);
        else
            _logger.LogWarning(
                "Permission denied - User: {UserId}",
                userId);

        return hasPermission;
    }

    public async Task<bool> OrgAdminCheck(
        long userId, long organizationId)
    {
        var hasPermission = _dbContext.Database
            .SqlQuery<bool>($@"
            SELECT EXISTS(
                SELECT 1
                FROM deeplynx.organization_users ou
                WHERE ou.organization_id = {organizationId}
                  AND ou.user_id = {userId}
                  AND ou.is_org_admin = true
                ) as has_permission")
            .AsEnumerable()
            .FirstOrDefault();

        if (hasPermission)
            _logger.LogInformation(
                "Permission granted - User: {UserId}",
                userId);
        else
            _logger.LogWarning(
                "Permission denied - User: {UserId}",
                userId);

        return hasPermission;
    }

    public async Task<bool> ProjectAdminCheck(
        long userId, long organizationId, List<long> projectIds)
    {
        // If no project IDs provided, return false
        if (projectIds == null || !projectIds.Any())
        {
            _logger.LogWarning(
                "Permission denied - No project IDs provided for User: {UserId}",
                userId);
            return false;
        }

        // Get all project IDs where the user is a direct admin OR an admin through group membership
        var adminProjectIds = await _dbContext.ProjectMembers
            .Where(pm =>
                pm.Role.Name == "Admin" &&
                pm.Role.OrganizationId == organizationId &&
                (
                    // Direct membership
                    pm.UserId == userId ||
                    // Group membership
                    pm.Group.Users.Any(gu => gu.Id == userId)
                ))
            .Select(pm => pm.ProjectId)
            .Distinct()
            .ToListAsync();

        // Check if all requested project IDs are in the user's admin projects
        var hasPermission = projectIds.All(id => adminProjectIds.Contains(id));

        if (hasPermission)
            _logger.LogInformation(
                "Permission granted - User: {UserId} is admin on all {ProjectCount} projects in Organization: {OrganizationId}",
                userId, projectIds.Count, organizationId);
        else
            _logger.LogWarning(
                "Permission denied - User: {UserId} is not admin on all requested projects in Organization: {OrganizationId}. Requested: [{RequestedProjects}], Has admin on: [{AdminProjects}]",
                userId, organizationId, string.Join(", ", projectIds), string.Join(", ", adminProjectIds));

        return hasPermission;
    }

    public async Task<bool> OrgAdminInSystemCheck(long userId)
    {
        return await _dbContext.OrganizationUsers
            .AnyAsync(ou => ou.UserId == userId && ou.IsOrgAdmin);
    }
}