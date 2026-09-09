using deeplynx.datalayer.Models;
using deeplynx.helpers.Cache;
using deeplynx.interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers;

// Helper class for permission related logic that needs to occur in the business layer
public class SensitivityLabelService : ISensitivityLabelService
{
    private readonly DeeplynxContext _context;
    private readonly ILogger<SensitivityLabelService>? _logger;

    public SensitivityLabelService(DeeplynxContext context, ILogger<SensitivityLabelService>? logger = null)
    {
        _context = context;
        _logger = logger;
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

        var distinctProjectIds = projectIds.Distinct().ToArray();
        var authorizedLabelIds = new HashSet<long>();
        var uncachedProjectIds = new List<long>();

        // Check cache before querying db
        foreach (long projectId in distinctProjectIds)
        {
            string cacheKey = CacheKeys.ProjectAuthorizedSensitivityLabels(projectId, currentUserId, userAction);

            try
            {
                List<long> cachedLabels = await CacheService.Instance.GetAsync<List<long>>(cacheKey);

                if (cachedLabels != null)
                {
                    authorizedLabelIds.UnionWith(cachedLabels);
                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache read failed for sensitivity labels, user {UserId}, project {ProjectId}", currentUserId, projectId);
            }

            uncachedProjectIds.Add(projectId);
            }

        if (uncachedProjectIds.Count == 0)
        {
            return authorizedLabelIds.ToList();
        }

        // Labels in scope for the given organization/projects (org-level labels inherit into every project)
        var relevantLabels = await _context.SensitivityLabels
            .Where(l => l.OrganizationId == organizationId
                        && (l.ProjectId == null || uncachedProjectIds.Contains(l.ProjectId.Value)))
            .Select(l => new { l.Id, l.ProjectId })
            .ToListAsync();

        // Labels for which this action is actually gated (non-archived definition present)
        var governedLabelIds = (await _context.SensitivityLabelPermissions
            .Where(p => p.Action == userAction && !p.IsArchived)
            .Select(p => p.LabelId)
            .ToListAsync())
            .ToHashSet();

        // Labels this user has been explicitly granted access to, either directly or through
        // a group they belong to
        var grantedLabelIds = _context.UserSensitivityLabels
            .Where(u => u.UserId == currentUserId)
            .Select(u => u.LabelId)
            .Union(_context.GroupSensitivityLabels
                .Where(g => g.Group.Users.Any(u => u.Id == currentUserId))
                .Select(g => g.LabelId));

        // A label is "authorized" for this action if it isn't gated for that action at all,
        // or the user has an explicit grant for it
        bool IsAuthorized(long labelId) => !governedLabelIds.Contains(labelId) || grantedLabelIds.Contains(labelId);

        var orgLevelAuthorized = relevantLabels
            .Where(l => l.ProjectId == null && IsAuthorized(l.Id))
            .Select(l => l.Id)
            .ToHashSet();

        foreach (long projectId in uncachedProjectIds)
        {
            var projectAuthorized = new HashSet<long>(orgLevelAuthorized);
            projectAuthorized.UnionWith(relevantLabels.Where(l => l.ProjectId == projectId && IsAuthorized(l.Id)).Select(l => l.Id));

            authorizedLabelIds.UnionWith(projectAuthorized);

            // Update the cache 
            string cacheKey = CacheKeys.ProjectAuthorizedSensitivityLabels(projectId, currentUserId, userAction);
            try
            {
                await CacheService.Instance.SetAsync(cacheKey, projectAuthorized.ToList(), (TimeSpan?)null);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Cache write failed for sensitivity labels, user {UserId}, project {ProjectId}", currentUserId, projectId);
            }
        }

        return authorizedLabelIds.ToList();
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

    /// <summary>
    /// Invalidates the authorized-labels cache  across every project the label is visible in 
    /// (its own project, or every project in the org for an org-level label). 
    /// Invalidates the cache for every user, or for a specific user if the userId is provided.
    /// </summary>
    public async Task InvalidateAuthorizedLabelsCache(long labelId, long? userId = null)
    {
        // Get IDs of all projects that would be affected by a sensitivity label mutation
        List<long> affectedProjects;

        var label = await _context.SensitivityLabels
            .Where(l => l.Id == labelId)
            .Select(l => new { l.ProjectId, l.OrganizationId })
            .FirstOrDefaultAsync();

        if (label == null)
        {
            return;
        }
        else
        {
            if (label.ProjectId.HasValue)
            {
                affectedProjects = new List<long> { label.ProjectId.Value };
            }
            else
            {
                affectedProjects = await _context.Projects
                    .Where(p => p.OrganizationId == label.OrganizationId)
                    .Select(p => p.Id)
                    .ToListAsync();
            }
        }

        // Invalidate the cache for all affected projects (scoped to userId if it is provided)
        foreach (var projectId in affectedProjects)
        {
            if (userId.HasValue)
            {
                try
                {
                    await CacheService.Instance.DeleteByPrefixAsync($"authorizedsensitivitylabels:{projectId}:{userId}:");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Cache prefix invalidation failed for label {LabelId}, user {UserId}, project {ProjectId}", labelId, userId, projectId);
                }
            }
            else
            {
                try
                {
                    await CacheService.Instance.DeleteByPrefixAsync($"authorizedsensitivitylabels:{projectId}:");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Cache prefix invalidation failed for label {LabelId}, project {ProjectId}", labelId, projectId);
                }
            }
        }
    }
}