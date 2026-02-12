using deeplynx.helpers.Context;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace deeplynx.helpers;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SensitivityAttribute : Attribute
{
    public SensitivityAttribute(string action)
    {
        Action = action;
    }

    public string Action { get; set; }
}

public class SensitivityMiddleware
{
    private readonly RequestDelegate _next;
    
    // Sensitivity Labels on records can only be added during record creation
    // or updated through the "attach/unattach label" endpoints
    private static readonly HashSet<string> _createActions = new()
    {
        "write record",
        "upload file",
    };

    private static readonly HashSet<string> _readDeleteActions = new()
    {
        "read record",
        "download file",
        "delete record",
        "delete file"
    };
    
    private static readonly HashSet<string> _updateActions = new()
    {
        "update record",
        "update file"
    };

    public SensitivityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISensitivityLabelService sensitivityLabelService,
        IAdminService adminService)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await _next(context);
            return;
        }

        var sensitivityAttr = endpoint.Metadata.GetMetadata<SensitivityAttribute>();
        
        if (sensitivityAttr == null)
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
        if (isSysAdmin)
        {
            await _next(context);
            return;
        }
        
        var organizationId = UserContextStorage.OrganizationId;
        
        var isOrgAdmin = await adminService.OrgAdminCheck(userId, organizationId);
        if (isOrgAdmin)
        {
            await _next(context);
            return;
        }
        
        var projectIds = new List<long>();
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

        if (projectIds.Count > 0)
        {
            var isProjectAdmin = await adminService.ProjectAdminCheck(userId, organizationId, projectIds);
            if (isProjectAdmin)
            {
                await _next(context);
                return;
            }
        }

        var providedLabelIds = new List<long>();
        
        if (context.Request.Query.TryGetValue("sensitivityLabelId", out var queryLabelId))
        {
            if (long.TryParse(queryLabelId, out var labelId))
                providedLabelIds.Add(labelId);
        }
        
        if (context.Request.Query.TryGetValue("sensitivityLabelIds", out var queryLabelIds))
        {
            foreach (var idValue in queryLabelIds)
                if (!string.IsNullOrEmpty(idValue))
                {
                    var ids = idValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var id in ids)
                        if (long.TryParse(id.Trim(), out var parsedId) && !providedLabelIds.Contains(parsedId))
                            providedLabelIds.Add(parsedId);
                }
        }
        
        // For operations on existing records check user permissions
        long? recordId = null;
        var routeRecordId = context.GetRouteValue("recordId")?.ToString();
        if (!string.IsNullOrEmpty(routeRecordId) && long.TryParse(routeRecordId, out var tempRecordId))
            recordId = tempRecordId;

        // Get authorized labels for this user, organization, projects, and action
        var authorizedLabelIds = await sensitivityLabelService.GetAuthorizedSensitivityLabels(
            userId,
            organizationId,
            projectIds.ToArray(),
            sensitivityAttr.Action);

        // CREATE ACTIONS
        // Check if labels are required and if provided labels are authorized
        if (_createActions.Contains(sensitivityAttr.Action))
        {
            var projectId = projectIds.FirstOrDefault();
            var isLabelRequired = await sensitivityLabelService.IsSensitivityLabelRequired(
                organizationId,
                projectId > 0 ? projectId : null);

            // If labels are required but none provided, reject
            if (isLabelRequired && providedLabelIds.Count == 0)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new 
                { 
                    error = "Sensitivity label is required" 
                });
                return;
            }

            // Check if user has permission for the provided labels
            if (providedLabelIds.Count > 0)
            {
                var unauthorizedLabels = providedLabelIds.Except(authorizedLabelIds).ToList();
                if (unauthorizedLabels.Any())
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new 
                    { 
                        error = "User does not have permission to use one or more provided sensitivity labels",
                        unauthorizedLabelIds = unauthorizedLabels
                    });
                    return;
                }
            }
        }
        
        // READ & DELETE ACTIONS
        // Check if user has permission for existing labels on the record
        if (_readDeleteActions.Contains(sensitivityAttr.Action) && recordId != null)
        {
            var existingLabelIds = await sensitivityLabelService.GetRecordSensitivityLabels(recordId.Value);
            
            // If record has labels, user must have permission for ALL of them
            if (existingLabelIds.Count > 0)
            {
                var unauthorizedLabels = existingLabelIds.Except(authorizedLabelIds).ToList();
                if (unauthorizedLabels.Any())
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new 
                    { 
                        error = "User does not have permission to access record with current sensitivity labels",
                        unauthorizedLabelIds = unauthorizedLabels
                    });
                    return;
                }
            }
        }
      
        // UPDATE ACTIONS
        // Check both existing labels and any new labels being added
        if (_updateActions.Contains(sensitivityAttr.Action) && recordId != null)
        {
            var existingLabelIds = await sensitivityLabelService.GetRecordSensitivityLabels(recordId.Value);
            
            // User must have permission for existing labels to update the record
            if (existingLabelIds.Count > 0)
            {
                var unauthorizedExistingLabels = existingLabelIds.Except(authorizedLabelIds).ToList();
                if (unauthorizedExistingLabels.Any())
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new 
                    { 
                        error = "User does not have permission to update record with current sensitivity labels",
                        unauthorizedLabelIds = unauthorizedExistingLabels
                    });
                    return;
                }
            }
            
            // If new labels are being added, user must have permission for those too
            if (providedLabelIds.Count > 0)
            {
                var unauthorizedNewLabels = providedLabelIds.Except(authorizedLabelIds).ToList();
                if (unauthorizedNewLabels.Any())
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new 
                    { 
                        error = "User does not have permission to add one or more provided sensitivity labels",
                        unauthorizedLabelIds = unauthorizedNewLabels
                    });
                    return;
                }
            }
        }

        // All checks passed, proceed to next middleware
        await _next(context);
    }
}