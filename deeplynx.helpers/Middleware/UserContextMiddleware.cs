using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers;

public class UserContextMiddleware
{
    private readonly ILogger<UserContextMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public UserContextMiddleware(
        RequestDelegate next,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<UserContextMiddleware> logger)
    {
        _next = next;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                _logger.LogInformation("User is authenticated, extracting user context");

                var authHeader = context.Request.Headers["Authorization"].ToString();
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    UserContextStorage.Token = authHeader["Bearer ".Length..].Trim();
                }

                // Log all claims for debugging
                var allClaims = context.User.Claims.Select(c => $"{c.Type}={c.Value}");
                _logger.LogInformation($"Available claims: {string.Join(", ", allClaims)}");

                // Try to extract email from multiple possible claim types
                var email = ClaimsEmailExtractor.ExtractEmail(context.User);

                if (!string.IsNullOrEmpty(email))
                {
                    _logger.LogInformation($"Email extracted: {email}");
                    UserContextStorage.Email = email;

                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<DeeplynxContext>();
                        var user = await dbContext.Users
                            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                        if (user != null)
                        {
                            UserContextStorage.UserId = user.Id;
                            UserContextStorage.AccountType = user.AccountType;

                            var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();
                            var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();

                            UserContextStorage.IsSysAdmin = await adminService.SysAdminCheck(user.Id);

                            var projectIds = ExtractProjectIds(context);

                            long? organizationIdFromRoute = ExtractOrganizationId(context);

                            long resolvedOrganizationId;

                            try
                            {
                                if (projectIds.Any())
                                {
                                    resolvedOrganizationId = await organizationService.ResolveOrganizationIdFromProjectsAsync(
                                        projectIds,
                                        organizationIdFromRoute
                                    );
                                }
                                else
                                {
                                    resolvedOrganizationId = await organizationService.CheckExistence(
                                        null,
                                        organizationIdFromRoute
                                    );
                                }
                            }
                            catch (Exception ex)
                            {
                                var logger = scope.ServiceProvider.GetRequiredService<ILogger<UserContextMiddleware>>();
                                logger.LogWarning(ex, "Organization resolution failed");

                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
                                return;
                            }

                            UserContextStorage.OrganizationId = resolvedOrganizationId;

                            UserContextStorage.IsOrgAdmin = await adminService.OrgAdminCheck(user.Id, resolvedOrganizationId);
                            UserContextStorage.IsOrgMember = await adminService.OrgMemberCheck(user.Id, resolvedOrganizationId);
                            UserContextStorage.IsProjectAdmin = projectIds.Any() &&
                                await adminService.ProjectAdminCheck(user.Id, resolvedOrganizationId, projectIds);
                        }

                        else
                        {
                            _logger.LogWarning($"User with email {email} not found in database");
                            UserContextStorage.UserId = 0;
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Could not extract email from claims");
                    UserContextStorage.Email = null;
                    UserContextStorage.UserId = 0;
                    UserContextStorage.IsOrgMember = false;
                }
            }
            else
            {
                _logger.LogInformation("User is not authenticated");
                UserContextStorage.Email = null;
                UserContextStorage.UserId = 0;
            }

            await _next(context);
        }
        finally
        {
            // Always clear after request completes
            UserContextStorage.Email = null;
            UserContextStorage.UserId = 0;
            UserContextStorage.IsSysAdmin = false;
            UserContextStorage.IsOrgAdmin = false;
            UserContextStorage.IsOrgMember = false;
            UserContextStorage.IsProjectAdmin = false;
        }
    }

    private static long? ExtractOrganizationId(HttpContext context)
    {
        var routeOrgId = context.GetRouteValue("organizationId")?.ToString();
        if (!string.IsNullOrEmpty(routeOrgId) && long.TryParse(routeOrgId, out var orgId))
            return orgId;
        return null;
    }

    private static List<long> ExtractProjectIds(HttpContext context)
    {
        var projectIds = new List<long>();

        var routeProjectId = context.GetRouteValue("projectId")?.ToString();
        if (!string.IsNullOrEmpty(routeProjectId) && long.TryParse(routeProjectId, out var parsedRouteId))
            projectIds.Add(parsedRouteId);

        if (context.Request.Query.TryGetValue("projectIds", out var queryProjectIds))
            foreach (var idValue in queryProjectIds)
                if (!string.IsNullOrEmpty(idValue))
                {
                    var ids = idValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var id in ids)
                        if (long.TryParse(id.Trim(), out var parsedId) && !projectIds.Contains(parsedId))
                            projectIds.Add(parsedId);
                }

        return projectIds;
    }
}
