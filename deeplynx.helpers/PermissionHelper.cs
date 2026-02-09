using deeplynx.datalayer.Models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.helpers;

// Helper class for permission related logic that needs to occur in the business layer
public class PermissionHelper
{
    /// <summary>
    /// Get authorized sensitivity labels for a single project
    /// </summary>
    public static async Task<List<long>> GetAuthorizedSensitivityLabels(
        DeeplynxContext _context, 
        long currentUserId, 
        long organizationId,
        long projectId, 
        string userAction)
    {
        // Delegate to the multi-project version
        return await GetAuthorizedSensitivityLabels(
            _context, 
            currentUserId, 
            organizationId, 
            new[] { projectId }, 
            userAction);
    }

    /// <summary>
    /// Get authorized sensitivity labels across multiple projects
    /// </summary>
    public static async Task<List<long>> GetAuthorizedSensitivityLabels(
        DeeplynxContext _context, 
        long currentUserId, 
        long organizationId,
        long[] projectIds, 
        string userAction)
    {
        if (userAction != "read" && userAction != "write")
            throw new ArgumentException("userAction must be 'read' or 'write'");

        if (projectIds == null || projectIds.Length == 0)
            return new List<long>();

        // Direct permissions (user directly assigned to project with a role)
        var directLabelIds = _context.ProjectMembers
            .Where(pm => pm.UserId == currentUserId
                         && projectIds.Any(id => id == pm.ProjectId)
                         && pm.Project.OrganizationId == organizationId
                         && pm.RoleId != null
                         && !pm.Role.IsArchived)
            .SelectMany(pm => pm.Role.Permissions)
            .Where(p => p.LabelId != null && p.Action == userAction && !p.IsArchived)
            .Select(p => p.LabelId.Value);

        // Group-based permissions (user is member of a group that has a role in the project)
        var groupLabelIds = _context.ProjectMembers
            .Where(pm => pm.GroupId != null
                         && projectIds.Any(id => id == pm.ProjectId)
                         && pm.Project.OrganizationId == organizationId
                         && pm.RoleId != null
                         && !pm.Role.IsArchived
                         && !pm.Group.IsArchived)
            .Where(pm => pm.Group.Users.Any(u => u.Id == currentUserId))
            .SelectMany(pm => pm.Role.Permissions)
            .Where(p => p.LabelId != null && p.Action == userAction && !p.IsArchived)
            .Select(p => p.LabelId.Value);

        // Combine and remove duplicates
        var authorizedLabelIds = await directLabelIds
            .Union(groupLabelIds)
            .Distinct()
            .ToListAsync();

        return authorizedLabelIds;
    }
}