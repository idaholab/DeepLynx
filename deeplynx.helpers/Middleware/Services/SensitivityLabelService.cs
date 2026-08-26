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

        // Labels in scope for the given organization/projects (org-level labels inherit into every project)
        var relevantLabels = _context.SensitivityLabels
            .Where(l => l.OrganizationId == organizationId
                        && (l.ProjectId == null || projectIds.Contains(l.ProjectId.Value)));

        // Labels for which this action is actually gated (non-archived definition present)
        var governedLabelIds = _context.SensitivityLabelPermissions
            .Where(p => p.Action == userAction && !p.IsArchived)
            .Select(p => p.LabelId);

        // Labels this user has been explicitly granted access to
        var grantedLabelIds = _context.UserSensitivityLabels
            .Where(u => u.UserId == currentUserId)
            .Select(u => u.LabelId);

        // A label is "authorized" for this action if it isn't gated for that action at all,
        // or the user has an explicit grant for it
        var authorizedLabelIds = await relevantLabels
            .Where(l => !governedLabelIds.Contains(l.Id) || grantedLabelIds.Contains(l.Id))
            .Select(l => l.Id)
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
    
    public async Task<List<long>> GetRecordCollectionSensitivityLabels(long recordCollectionId)
    {
        var labelIds = await _context.RecordCollections
            .Where(r => r.Id == recordCollectionId)
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