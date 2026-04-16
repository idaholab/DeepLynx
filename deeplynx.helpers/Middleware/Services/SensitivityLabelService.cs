using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.helpers;

// Helper class for permission related logic that needs to occur in the business layer
public class SensitivityLabelService : ISensitivityLabelService
{
    private readonly DeeplynxContext _context;

    public SensitivityLabelService(DeeplynxContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get authorized sensitivity labels for a single project
    /// </summary>
    public async Task<List<long>> GetAuthorizedSensitivityLabels(
        long currentUserId,
        long organizationId,
        long projectId,
        string userAction)
    {
        // Delegate to the multi-project version
        return await GetAuthorizedSensitivityLabels(
            currentUserId,
            organizationId,
            new[] { projectId },
            userAction);
    }

    /// <summary>
    /// Get authorized sensitivity labels across multiple projects
    /// </summary>
    public async Task<List<long>> GetAuthorizedSensitivityLabels(
        long currentUserId,
        long organizationId,
        long[] projectIds,
        string userAction)
    {
        var validActions = new List<string>
        {
            "write record", "upload file",
            "read record", "download file",
            "update record", "update file",
            "delete record", "delete file"
        };

        // if user action does not contain read, write, update, delete, upload, download, + file
        if (!validActions.Contains(userAction))
            throw new ArgumentException("User action must be read, write, update, delete, upload, or download");

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

    public async Task<bool> IsSensitivityLabelRequired(
        long organizationId,
        long? projectId)
    {
        // if org level check the organization
        var orgLevel = await _context.Organizations
            .Where(o => o.Id == organizationId)
            .Select(o => o.RequireSensitivityLabel)
            .FirstOrDefaultAsync();

        if (orgLevel) return true;

        // if no project ID is provided and orgLevel is false, return false
        if (projectId == null && !orgLevel) return false;

        // if project ID is provided and org level is false
        // check the project's "require_sensitivity_level" column value
        var projectLevel = await _context.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.RequireSensitivityLabel)
            .FirstOrDefaultAsync();

        // return result
        return projectLevel;
    }

    public async Task<List<long>> GetRecordSensitivityLabels(long recordId)
    {
        // Implement based on your data model
        // This is a placeholder - adjust according to your actual schema
        var labelIds = await _context.Records
            .Where(r => r.Id == recordId)
            .SelectMany(r => r.Labels)
            .Select(l => l.Id)
            .ToListAsync();

        return labelIds;
    }

    public async Task<HashSet<long>> FilterAuthorizedRecordIds(
    long currentUserId,
    long organizationId,
    long projectId,
    ICollection<long> recordIds,
    DeeplynxContext context)
    {
        if (recordIds.Count == 0)
            return [];

        var authorizedLabels = await GetAuthorizedSensitivityLabels(
            currentUserId, organizationId, projectId, "read record");

        var ids = await context.Records
            .Where(r => r.ProjectId == projectId
                    && r.OrganizationId == organizationId
                    && recordIds.Contains(r.Id)
                    && (r.Labels.Count == 0
                        || r.Labels.All(l => authorizedLabels.Contains(l.Id))))
            .Select(r => r.Id)
            .ToListAsync();

        return [.. ids];
    }
}