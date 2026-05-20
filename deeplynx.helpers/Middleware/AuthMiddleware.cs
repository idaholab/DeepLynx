using deeplynx.helpers.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace deeplynx.helpers;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class AuthAttribute : Attribute
{
    public AuthAttribute(string action, string resource, bool includeArchived = false)
    {
        Action = action;
        Resource = resource;
        IncludeArchived = includeArchived;
    }

    public string Action { get; set; }
    public string Resource { get; set; }
    public bool IncludeArchived { get; set; }
}

/// <summary>
/// Requires the user to be a system administrator
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SysAdminAttribute : Attribute
{
}

/// <summary>
/// Requires the user to be an organization administrator
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class OrgAdminAttribute : Attribute
{
    public bool IncludeArchived { get; set; }

    public OrgAdminAttribute(bool includeArchived = false)
    {
        IncludeArchived = includeArchived;
    }
}

/// <summary>
/// Allows an endpoint to be invoked without organization or project context.
/// When present, auth checks fall back to a scope-exception branch (e.g. system-wide org-admin lookup)
/// instead of rejecting the request for missing context.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class AllowWithoutContextAttribute : Attribute
{
}

public class AuthMiddleware
{
    private readonly RequestDelegate _next;

    public AuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOrgRolePermissionService orgRolePermissionService,
        IProjectRolePermissionService projectRolePermissionService, IAdminService adminService,
        IOrganizationService organizationService)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await _next(context);
            return;
        }

        // Check for admin attributes first
        var sysAdminAttr = endpoint.Metadata.GetMetadata<SysAdminAttribute>();
        var orgAdminAttr = endpoint.Metadata.GetMetadata<OrgAdminAttribute>();
        var authAttributes = endpoint.Metadata.GetOrderedMetadata<AuthAttribute>();
        var allowWithoutContext = endpoint.Metadata.GetMetadata<AllowWithoutContextAttribute>() != null;

        // If no auth attributes at all, continue
        if (sysAdminAttr == null && orgAdminAttr == null && !authAttributes.Any())
        {
            await _next(context);
            return;
        }

        var userId = UserContextStorage.UserId;

        if (userId <= 0)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        var isSysAdmin = await adminService.SysAdminCheck(userId);
        UserContextStorage.IsSysAdmin = isSysAdmin;

        // Handle SysAdmin attribute
        if (sysAdminAttr != null)
        {
            if (!isSysAdmin)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Forbidden: System administrator access required" });
                return;
            }

            await _next(context);
            return;
        }

        // Extract organization and project IDs
        long? organizationId = null;
        var projectIds = new List<long>();
        long? capturedOrgId = null;

        var routeOrgId = context.GetRouteValue("organizationId")?.ToString();
        if (!string.IsNullOrEmpty(routeOrgId) && long.TryParse(routeOrgId, out var tempOrgId))
            organizationId = tempOrgId;

        var routeProjectId = context.GetRouteValue("projectId")?.ToString();
        if (!string.IsNullOrEmpty(routeProjectId) && long.TryParse(routeProjectId, out var tempProjectId))
            projectIds.Add(tempProjectId);

        if (context.Request.Query.TryGetValue("projectIds", out var queryProjectIds))
            foreach (var idValue in queryProjectIds)
                if (!string.IsNullOrEmpty(idValue))
                {
                    var ids = idValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var id in ids)
                        if (long.TryParse(id.Trim(), out var parsedId) && !projectIds.Contains(parsedId))
                            projectIds.Add(parsedId);
                }

        // Handle OrgAdmin attribute
        if (orgAdminAttr != null)
        {
            if (!organizationId.HasValue)
            {
                if (allowWithoutContext)
                {
                    if (isSysAdmin || await adminService.OrgAdminInSystemCheck(userId))
                    {
                        await _next(context);
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "Forbidden: Organization administrator access required" });
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "Bad Request: Organization ID required for organization admin check" });
                return;
            }

            // Check organization existence
            capturedOrgId = await organizationService.CheckExistence(
                null,
                organizationId,
                orgAdminAttr.IncludeArchived
            );

            if (capturedOrgId.HasValue)
                UserContextStorage.OrganizationId = capturedOrgId.Value;

            // System admins automatically pass
            if (isSysAdmin)
            {
                await _next(context);
                return;
            }

            // Check if user is org admin (using permission check)
            var isOrgAdmin = await adminService.OrgAdminCheck(userId, organizationId.Value);
            UserContextStorage.IsOrgAdmin = isOrgAdmin;

            if (!isOrgAdmin)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Forbidden: Organization administrator access required" });
                return;
            }

            await _next(context);
            return;
        }

        if (!isSysAdmin && !organizationId.HasValue && !projectIds.Any() && !allowWithoutContext)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            { error = "Forbidden: Non-admin users require organization or project context" });
            return;
        }

        var firstAuthAttr = authAttributes.FirstOrDefault();
        var includeArchived = firstAuthAttr?.IncludeArchived ?? false;

        if (projectIds.Any() || organizationId.HasValue)
        {
            foreach (var projectId in projectIds)
                capturedOrgId = await organizationService.CheckExistence(
                    projectId,
                    organizationId,
                    includeArchived
                );

            if (!projectIds.Any() && organizationId.HasValue)
                capturedOrgId = await organizationService.CheckExistence(
                    null,
                    organizationId,
                    includeArchived
                );

            if (capturedOrgId.HasValue) UserContextStorage.OrganizationId = capturedOrgId.Value;

            var isProjectAdmin = organizationId.HasValue &&
                                 await adminService.ProjectAdminCheck(userId, organizationId.Value, projectIds);
            UserContextStorage.IsProjectAdmin = isProjectAdmin;
        }

        if (isSysAdmin)
        {
            await _next(context);
            return;
        }

        foreach (var authAttr in authAttributes)
        {
            var hasPermission = false;

            if (projectIds.Any())
            {
                var hasPermissionInAllProjects = true;

                foreach (var projectId in projectIds)
                {
                    var projectPermission = await projectRolePermissionService.PermissionInProject(
                        userId,
                        projectId,
                        authAttr.Action,
                        authAttr.Resource
                    );

                    if (!projectPermission)
                    {
                        hasPermissionInAllProjects = false;
                        break;
                    }
                }

                hasPermission = hasPermissionInAllProjects;
            }
            else if (organizationId.HasValue)
            {
                hasPermission = await orgRolePermissionService.PermissionInOrg(
                    userId,
                    organizationId.Value,
                    authAttr.Action,
                    authAttr.Resource
                );
            }
            else if (allowWithoutContext)
            {
                hasPermission = true;
            }

            if (!hasPermission)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Forbidden: User role does not have required permissions in organization or project(s)"
                });
                return;
            }
        }

        await _next(context);
    }
}