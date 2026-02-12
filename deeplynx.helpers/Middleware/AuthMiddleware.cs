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

        var authAttributes = endpoint.Metadata
            .GetOrderedMetadata<AuthAttribute>();

        if (!authAttributes.Any())
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

        int? organizationId = null;
        var projectIds = new List<int>();

        var routeOrgId = context.GetRouteValue("organizationId")?.ToString();
        if (!string.IsNullOrEmpty(routeOrgId) && int.TryParse(routeOrgId, out var tempOrgId))
            organizationId = tempOrgId;

        var routeProjectId = context.GetRouteValue("projectId")?.ToString();
        if (!string.IsNullOrEmpty(routeProjectId) && int.TryParse(routeProjectId, out var tempProjectId))
            projectIds.Add(tempProjectId);

        if (context.Request.Query.TryGetValue("projectIds", out var queryProjectIds))
            foreach (var idValue in queryProjectIds)
                if (!string.IsNullOrEmpty(idValue))
                {
                    var ids = idValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var id in ids)
                        if (int.TryParse(id.Trim(), out var parsedId) && !projectIds.Contains(parsedId))
                            projectIds.Add(parsedId);
                }

        if (!isSysAdmin && !organizationId.HasValue && !projectIds.Any())
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
                { error = "Forbidden: Non-admin users require organization or project context" });
            return;
        }

        // Determine if any auth attribute is for unarchive/restore operations
        var firstAuthAttr = authAttributes.FirstOrDefault();
        var includeArchived = firstAuthAttr?.IncludeArchived ?? false;

        long? capturedOrgId = null;
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