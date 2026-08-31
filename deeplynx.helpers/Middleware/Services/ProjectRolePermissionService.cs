using deeplynx.datalayer.Models;
using deeplynx.helpers.Cache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace deeplynx.helpers;

//Keeping this interface in the same file as the service
public interface IProjectRolePermissionService
{
    Task<bool> PermissionInProject(long userId, long projectId, string action, string resource);
    Task<List<long>> PermissionsInProjects(long userId, long[] projectIds, string action, string resource);
    Task<List<long>> GetPermittedProjectIdsAsync(long userId, string action, string resource);
}

public class ProjectRolePermissionService : IProjectRolePermissionService
{
    private readonly DeeplynxContext _dbContext;
    private readonly ILogger<ProjectRolePermissionService> _logger;

    public ProjectRolePermissionService(
        DeeplynxContext dbContext,
        ILogger<ProjectRolePermissionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> PermissionInProject(
        long userId,
        long projectId,
        string action,
        string resource)
    {

        bool hasPermission;
        
        _logger.LogInformation(
            "Checking permission - User: {UserId}, Project: {ProjectId}, Action: {Action}, Resource: {Resource}",
            userId, projectId, action, resource);

        // Check the cache before querying the db
        var cacheKey = CacheKeys.ProjectPermission(userId, projectId, action, resource);
        var cached = await CacheService.Instance.GetAsync<bool?>(cacheKey);
        if (cached.HasValue)
        {
            hasPermission = cached.Value;
        }
        else
        {
            //check for whether a user has permission to an action/resource within a project through group membership
            hasPermission = _dbContext.Database
                .SqlQuery<bool>($@"
                    SELECT EXISTS (
                            SELECT 1
                            FROM deeplynx.users u
                            LEFT JOIN deeplynx.group_users gu ON gu.user_id = u.id
                            LEFT JOIN deeplynx.groups g ON gu.group_id = g.id
                            LEFT JOIN deeplynx.project_members pm ON (pm.user_id = u.id OR pm.group_id = g.id)
                            LEFT JOIN deeplynx.roles r ON r.id = pm.role_id
                            LEFT JOIN deeplynx.role_permissions rp ON rp.role_id = pm.role_id
                            LEFT JOIN deeplynx.permissions perm ON rp.permission_id = perm.id
                            WHERE u.id = {userId}
                            AND pm.project_id = {projectId}
                            AND perm.resource = {resource}
                            AND perm.action = {action}
                            AND r.is_archived = false
                            AND perm.is_archived = false
                        ) AS has_permission")
                    .AsEnumerable()
                    .FirstOrDefault();

            // Populate cache on miss
            await CacheService.Instance.SetAsync(cacheKey, hasPermission, (TimeSpan?)null);
        }

        if (hasPermission)
        {
            _logger.LogInformation(
                "Permission granted (group) - User: {UserId}, Project: {ProjectId}, Action: {Action}, Resource: {Resource}",
                userId, projectId, action, resource);
        }
        else
        {
            _logger.LogWarning(
                "Permission denied - User: {UserId}, Project: {ProjectId}, Action: {Action}, Resource: {Resource}",
                userId, projectId, action, resource);
        }

        return hasPermission;
    }

    public async Task<List<long>> PermissionsInProjects(
        long userId,
        long[] projectIds,
        string action,
        string resource)
    {
        _logger.LogInformation(
            "Bulk permission check - User: {UserId}, Projects: {ProjectIds}, Action: {Action}, Resource: {Resource}",
            userId, string.Join(',', projectIds), action, resource);

        if (projectIds == null || projectIds.Length == 0)
            return new List<long>();

        var result = new List<long>();
        var uncachedIds = new List<long>();

        // Check cache for each projectId before querying the db
        foreach (var projectId in projectIds)
        {
            var cacheKey = CacheKeys.ProjectPermission(userId, projectId, action, resource);
            var cached = await CacheService.Instance.GetAsync<bool?>(cacheKey);
            if (cached.HasValue)
            {
                if (cached.Value) result.Add(projectId);
            }
            else
            {
                uncachedIds.Add(projectId);
            }
        }

        if (uncachedIds.Count == 0)
        {
            return result;
        }

        var sql = @"
        SELECT DISTINCT pm.project_id
        FROM deeplynx.users u
        LEFT JOIN deeplynx.group_users gu ON gu.user_id = u.id
        LEFT JOIN deeplynx.groups g ON gu.group_id = g.id
        LEFT JOIN deeplynx.project_members pm ON (pm.user_id = u.id OR pm.group_id = g.id)
        LEFT JOIN deeplynx.roles r ON r.id = pm.role_id
        LEFT JOIN deeplynx.role_permissions rp ON rp.role_id = pm.role_id
        LEFT JOIN deeplynx.permissions perm ON rp.permission_id = perm.id
        WHERE u.id = @userId
          AND pm.project_id = ANY(@projectIds)
          AND perm.resource = @resource
          AND perm.action = @action
          AND r.is_archived = false
          AND perm.is_archived = false";

        var userIdParam = new NpgsqlParameter("userId", userId);
        var projectIdsParam = new NpgsqlParameter("projectIds", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Bigint)
        {
            Value = uncachedIds.ToArray()
        };
        var resourceParam = new NpgsqlParameter("resource", resource);
        var actionParam = new NpgsqlParameter("action", action);

        // Search the database for every authorized projectId that wasn't found in the cache
        var authorizedIds = await _dbContext.Database
            .SqlQueryRaw<long>(sql, userIdParam, projectIdsParam, resourceParam, actionParam)
            .ToListAsync();

        var authorizedSet = new HashSet<long>(authorizedIds);

        // Populate cache with all uncachedIds, marking them as authorized or not based on db query
        foreach (var projectId in uncachedIds)
        {
            var isAuthorized = authorizedSet.Contains(projectId);
            await CacheService.Instance.SetAsync(
                CacheKeys.ProjectPermission(userId, projectId, action, resource),
                isAuthorized,
                (TimeSpan?)null);

            if (isAuthorized)
            {
                result.Add(projectId);
            }
        }

        _logger.LogInformation(
            "Bulk permission check result - User: {UserId}, Authorized Projects: {AuthorizedProjects}",
            userId, string.Join(',', result));

        return result;
    }

    public async Task<List<long>> GetPermittedProjectIdsAsync(long userId, string action, string resource)
    {
        var permittedProjectIds = await _dbContext.ProjectMembers
            .FromSqlInterpolated($@"
                SELECT DISTINCT pm.project_id
                FROM deeplynx.users u
                LEFT JOIN deeplynx.group_users gu ON gu.user_id = u.id
                LEFT JOIN deeplynx.groups g ON gu.group_id = g.id
                LEFT JOIN deeplynx.project_members pm ON (pm.user_id = u.id OR pm.group_id = g.id)
                LEFT JOIN deeplynx.roles r ON r.id = pm.role_id
                LEFT JOIN deeplynx.role_permissions rp ON rp.role_id = pm.role_id
                LEFT JOIN deeplynx.permissions perm ON rp.permission_id = perm.id
                WHERE u.id = {userId}
                AND perm.resource = {resource}
                AND perm.action = {action}
                AND r.is_archived = false
                AND perm.is_archived = false")
            .Select(pm => pm.ProjectId)
            .ToListAsync();

        return permittedProjectIds;
    }
}
