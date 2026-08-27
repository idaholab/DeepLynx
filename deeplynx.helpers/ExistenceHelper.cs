using deeplynx.datalayer.Models;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using deeplynx.helpers.Cache;

namespace deeplynx.helpers
{
    public static class ExistenceHelper
    {
        private static readonly TimeSpan DeletedUserCacheTtl = TimeSpan.FromMinutes(5);

        public static async Task EnsureUserExistsAsync(DeeplynxContext context, long userId, bool hideArchived = true)
        {
            var deletedCacheKey = CacheKeys.UserDeleted(userId);
            var cachedDeleted = await CacheService.Instance.GetAsync<bool?>(deletedCacheKey);
            if (cachedDeleted.HasValue && cachedDeleted.Value)
                throw new KeyNotFoundException($"User with id {userId} does not exist");

            var cacheKey = CacheKeys.UserArchivedStatus(userId);
            var cachedIsArchived = await CacheService.Instance.GetAsync<bool?>(cacheKey);

            bool userExists;
            bool isArchived;

            if (cachedIsArchived.HasValue)
            {
                userExists = true;
                isArchived = cachedIsArchived.Value;
            }
            else
            {
                var user = await context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.IsArchived })
                    .FirstOrDefaultAsync();

                userExists = user != null;

                if (userExists)
                {
                    isArchived = user!.IsArchived;
                    await CacheService.Instance.SetAsync(cacheKey, isArchived, (TimeSpan?)null);
                }
                else
                {
                    isArchived = false;
                }
            }

            if (!userExists)
                throw new KeyNotFoundException($"User with id {userId} does not exist");

            if (hideArchived && isArchived)
                throw new KeyNotFoundException($"User with id {userId} does not exist");
        }

        /// <summary>
        ///     Sets the cached archived status for a user, with no expiration. Call this whenever
        ///     a mutation determines a user's archived status directly (create, archive, unarchive,
        ///     or an update that changes IsArchived), so the cache reflects the new state.
        /// </summary>
        public static Task SetUserArchivedStatusCache(long userId, bool isArchived)
        {
            return CacheService.Instance.SetAsync(CacheKeys.UserArchivedStatus(userId), isArchived, (TimeSpan?)null);
        }

        /// <summary>
        ///     Marks a user as not-existing in the cache with a short TTL. Used after a hard delete:
        ///     unlike the general "user doesn't exist" case (never cached, see summary above), we
        ///     know definitively that this specific ID was just deleted, so caching that briefly
        ///     avoids a burst of repeat DB lookups right after the delete without permanently
        ///     committing to caching a non-existent id indefinitely.
        ///
        ///     Also clears the no-TTL UserArchivedStatus entry for this id, if one exists. Without
        ///     this, a stale "exists, archived: false/true" entry could sit there indefinitely
        ///     (that key has no TTL by design) alongside the new short-TTL "deleted" entry - and
        ///     once the deleted-entry TTL expires, the stale permanent entry could be read as if
        ///     the user still existed.
        /// </summary>
        public static async Task SetUserDeletedCache(long userId)
        {
            // Represented as a distinct "deleted" cache entry rather than reusing the archived-status
            // key/shape, since deleted is a different state than archived (archived users still
            // exist and have a real IsArchived flag; deleted users don't exist at all).
            await CacheService.Instance.SetAsync(CacheKeys.UserDeleted(userId), true, DeletedUserCacheTtl);
            await CacheService.Instance.DeleteAsync(CacheKeys.UserArchivedStatus(userId));
        }

        private static readonly TimeSpan DeletedOrganizationCacheTtl = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Check if an organization exists
        /// </summary>
        /// <param name="context">DB context</param>
        /// <param name="organizationId">Org ID to check existence for</param>
        /// <param name="hideArchived">Boolean indicating whether to hide archived orgs</param>
        /// <exception cref="KeyNotFoundException">Returned if org doesn't exist</exception>
        public static async Task EnsureOrganizationExistsAsync(
            DeeplynxContext context,
            long organizationId,
            bool hideArchived = true)
        {
            var deletedCacheKey = CacheKeys.OrganizationDeleted(organizationId);
            var cachedDeleted = await CacheService.Instance.GetAsync<bool?>(deletedCacheKey);
            if (cachedDeleted.HasValue && cachedDeleted.Value)
                throw new KeyNotFoundException($"Organization with id {organizationId} does not exist");

            var cacheKey = CacheKeys.OrganizationArchivedStatus(organizationId);
            var cachedIsArchived = await CacheService.Instance.GetAsync<bool?>(cacheKey);

            bool organizationExists;
            bool isArchived;

            if (cachedIsArchived.HasValue)
            {
                organizationExists = true;
                isArchived = cachedIsArchived.Value;
            }
            else
            {
                var organization = await context.Organizations
                    .Where(o => o.Id == organizationId)
                    .Select(o => new { o.IsArchived })
                    .FirstOrDefaultAsync();

                organizationExists = organization != null;

                if (organizationExists)
                {
                    isArchived = organization!.IsArchived;
                    await CacheService.Instance.SetAsync(cacheKey, isArchived, (TimeSpan?)null);
                }
                else
                {
                    isArchived = false;
                }
            }

            if (!organizationExists)
                throw new KeyNotFoundException($"Organization with id {organizationId} does not exist");

            if (hideArchived && isArchived)
                throw new KeyNotFoundException($"Organization with id {organizationId} does not exist");
        }

        /// <summary>
        ///     Sets the cached archived status for an organization, with no expiration. Call this whenever
        ///     a mutation determines an organization's archived status directly (create, archive,
        ///     unarchive), so the cache reflects the new state.
        /// </summary>
        public static Task SetOrganizationArchivedStatusCache(long organizationId, bool isArchived)
        {
            return CacheService.Instance.SetAsync(CacheKeys.OrganizationArchivedStatus(organizationId), isArchived, (TimeSpan?)null);
        }

        /// <summary>
        ///     Marks an organization as not-existing in the cache with a short TTL, mirroring
        ///     SetUserDeletedCache. Also clears the no-TTL OrganizationArchivedStatus entry so it can't
        ///     outlive the short TTL and mislead a later read.
        /// </summary>
        public static async Task SetOrganizationDeletedCache(long organizationId)
        {
            await CacheService.Instance.SetAsync(CacheKeys.OrganizationDeleted(organizationId), true, DeletedOrganizationCacheTtl);
            await CacheService.Instance.DeleteAsync(CacheKeys.OrganizationArchivedStatus(organizationId));
        }

        public static async Task<ProjectResponseDto> EnsureProjectExistsAsync(
            DeeplynxContext context,
            long projectId,
            bool hideArchived = true)
        {
            // Try to get the cached list of projects
            var projectResponseList = await CacheService.Instance.GetAsync<List<ProjectResponseDto>>("projects");

            if (projectResponseList == null || projectResponseList.Count == 0)
            {
                // Cache is empty, so populate it
                var projectList = await context.Projects.ToListAsync();

                projectResponseList = projectList.Select(p => new ProjectResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Abbreviation = p.Abbreviation,
                    IsArchived = p.IsArchived,
                    LastUpdatedAt = p.LastUpdatedAt,
                    LastUpdatedBy = p.LastUpdatedBy,
                    OrganizationId = p.OrganizationId
                }).ToList();

                // Store the list in the cache
                await CacheService.Instance.SetAsync("projects", projectResponseList, TimeSpan.FromHours(1));
            }

            // Find the project by ID from the list
            var project = projectResponseList.FirstOrDefault(p => p.Id == projectId);

            if (project == null || hideArchived && project.IsArchived)
            {

                throw new KeyNotFoundException($"Project with id {projectId} not found.");
            }

            return project;
        }

        public static async Task EnsureDataSourceExistsForProjectAsync(
            DeeplynxContext context,
            long dataSourceId,
            long projectId,
            long organizationId,
            bool hideArchived = true)
        {
            var dataSourceExists = hideArchived
                ? await context.DataSources.AnyAsync(ds =>
                    ds.Id == dataSourceId &&
                    ds.OrganizationId == organizationId &&
                    (ds.ProjectId == projectId || ds.ProjectId == null) &&
                    ds.IsArchived == false)
                : await context.DataSources.AnyAsync(ds =>
                    ds.Id == dataSourceId &&
                    ds.OrganizationId == organizationId &&
                    (ds.ProjectId == projectId || ds.ProjectId == null));

            if (!dataSourceExists)
            {
                throw new KeyNotFoundException($"DataSource with id {dataSourceId} not found in project with id {projectId} and organization with id {organizationId}");
            }
        }

        public static async Task EnsureObjectStorageExistsAsync(
            DeeplynxContext context,
            long organizationId,
            long projectId,
            long objectStorageId,
            bool hideArchived = true)
        {
            var objectStorageExists = hideArchived
                ? await context.ObjectStorages.AnyAsync(os =>
                    os.Id == objectStorageId &&
                    os.OrganizationId == organizationId &&
                    (os.ProjectId == null || os.ProjectId == projectId) &&
                    os.IsArchived == false)
                : await context.ObjectStorages.AnyAsync(os =>
                    os.Id == objectStorageId &&
                    os.OrganizationId == organizationId &&
                    (os.ProjectId == null || os.ProjectId == projectId));

            if (!objectStorageExists)
            {
                throw new KeyNotFoundException($"Object Storage with id {objectStorageId} not found in project with id {projectId}");
            }
        }
    }
}