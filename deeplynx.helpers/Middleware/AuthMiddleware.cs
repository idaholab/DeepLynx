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
/// Requires the user to be an organization administrator. Set <paramref name="unscoped"/>
/// when the route does not include an organization ID; the caller must then be a system admin or
/// an organization admin in at least one organization in the system.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class OrgAdminAttribute : Attribute
{
    public bool IncludeArchived { get; set; }
    public bool Unscoped { get; set; }

    public OrgAdminAttribute(bool includeArchived = false, bool unscoped = false)
    {
        IncludeArchived = includeArchived;
        Unscoped = unscoped;
    }
}

/// <summary>
/// Requires the user to be a project administrator of the project(s) in the route/query.
/// System administrators and organization administrators of the project's organization also pass.
/// The route (or <c>projectIds</c> query) must supply at least one project ID; the organization
/// is derived from the project, so an organization ID is not required on the route. Set
/// <paramref name="unscoped"/> when the route does not identify a specific project; the caller
/// must then be a system admin or a project admin of at least one project in the system.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class ProjectAdminAttribute : Attribute
{
    public bool IncludeArchived { get; set; }
    public bool Unscoped { get; set; }

    public ProjectAdminAttribute(bool includeArchived = false, bool unscoped = false)
    {
        IncludeArchived = includeArchived;
        Unscoped = unscoped;
    }
}

/// <summary>
/// Requires the user to be a member of the organization in the route (any role), an organization
/// admin, or a system admin. Use this for organization-scoped actions that every member should be
/// able to perform, such as creating a project.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class OrgMemberAttribute : Attribute
{
    public bool IncludeArchived { get; set; }

    public OrgMemberAttribute(bool includeArchived = false)
    {
        IncludeArchived = includeArchived;
    }
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

        var isSysAdmin = UserContextStorage.IsSysAdmin;

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
                if (orgAdminAttr.Unscoped)
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

            // IsOrgAdmin is pre-populated by UserContextMiddleware
            if (!UserContextStorage.IsOrgAdmin)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Forbidden: Organization administrator access required" });
                return;
            }

            await _next(context);
            return;
        }

        // Handle ProjectAdmin attribute
        if (projectAdminAttr != null)
        {
            if (!projectIds.Any())
            {
                if (projectAdminAttr.Unscoped)
                {
                    if (isSysAdmin || await adminService.ProjectAdminInSystemCheck(userId))
                    {
                        await _next(context);
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "Forbidden: Project administrator access required" });
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "Bad Request: Project ID required for project admin check" });
                return;
            }

            // Derive the organization from the project(s) so the admin checks have an org to scope to.
            foreach (var projectId in projectIds)
                capturedOrgId = await organizationService.CheckExistence(
                    projectId,
                    organizationId,
                    projectAdminAttr.IncludeArchived
                );

            if (capturedOrgId.HasValue)
                UserContextStorage.OrganizationId = capturedOrgId.Value;

            // System admins automatically pass
            if (isSysAdmin)
            {
                await _next(context);
                return;
            }

            // Organization admins of the project's organization pass (higher privilege than project admin)
            if (capturedOrgId.HasValue &&
                await adminService.OrgAdminCheck(userId, capturedOrgId.Value))
            {
                await _next(context);
                return;
            }

            // Project admins of all requested projects pass
            if (capturedOrgId.HasValue &&
                await adminService.ProjectAdminCheck(userId, capturedOrgId.Value, projectIds))
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Forbidden: Project administrator access required" });
            return;
        }

        // Handle OrgMember attribute
        if (orgMemberAttr != null)
        {
            if (!organizationId.HasValue)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "Bad Request: Organization ID required for organization membership check" });
                return;
            }

            // Check organization existence
            capturedOrgId = await organizationService.CheckExistence(
                null,
                organizationId,
                orgMemberAttr.IncludeArchived
            );

            if (capturedOrgId.HasValue)
                UserContextStorage.OrganizationId = capturedOrgId.Value;

            // System admins automatically pass
            if (isSysAdmin)
            {
                await _next(context);
                return;
            }

            // IsOrgMember (and IsOrgAdmin) are pre-populated by UserContextMiddleware
            if (!UserContextStorage.IsOrgMember && !UserContextStorage.IsOrgAdmin)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Forbidden: Organization membership required" });
                return;
            }

            await _next(context);
            return;
        }

        if (!isSysAdmin && !organizationId.HasValue && !projectIds.Any())
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
            // IsProjectAdmin is pre-populated by UserContextMiddleware
        }

        if (isSysAdmin)
        {
            await _next(context);
            return;
        }

        // Project admins bypass role-based permission checks for project-scoped endpoints.
        // Compute from the resolved org (route or derived from the project) so this also
        // works for project routes that do not carry an {organizationId} segment.
        if (projectIds.Any() && capturedOrgId.HasValue &&
            await adminService.ProjectAdminCheck(userId, capturedOrgId.Value, projectIds))
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