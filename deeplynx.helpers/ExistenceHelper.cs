using deeplynx.datalayer.Models;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using deeplynx.helpers.Cache;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.AspNetCore.Http.Features;

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
        private static readonly TimeSpan DeletedProjectCacheTtl = TimeSpan.FromMinutes(5);

        public static async Task EnsureProjectExistsAsync(
            DeeplynxContext context,
            long projectId,
            bool hideArchived = true)
        {
            var deletedCacheKey = CacheKeys.ProjectDeleted(projectId);
            var cachedDeleted = await CacheService.Instance.GetAsync<bool?>(deletedCacheKey);
            if (cachedDeleted.HasValue && cachedDeleted.Value)
                throw new KeyNotFoundException($"Project with id {projectId} not found.");

            var cacheKey = CacheKeys.ProjectArchivedStatus(projectId);
            var cachedIsArchived = await CacheService.Instance.GetAsync<bool?>(cacheKey);

            bool projectExists;
            bool isArchived;

            if (cachedIsArchived.HasValue)
            {
                projectExists = true;
                isArchived = cachedIsArchived.Value;
            }
            else
            {
                var project = await context.Projects
                    .Where(p => p.Id == projectId)
                    .Select(p => new { p.IsArchived })
                    .FirstOrDefaultAsync();

                projectExists = project != null;

                if (projectExists)
                {
                    isArchived = project!.IsArchived;
                    await CacheService.Instance.SetAsync(cacheKey, isArchived, (TimeSpan?)null);
                }
                else
                {
                    isArchived = false;
                }
            }

            if (!projectExists)
                throw new KeyNotFoundException($"Project with id {projectId} not found.");

            if (hideArchived && isArchived)
                throw new KeyNotFoundException($"Project with id {projectId} not found.");
        }

        /// <summary>
        ///     Like EnsureProjectExistsAsync, but returns the full ProjectResponseDto for callers that
        ///     need more than a throw-or-not check (currently: MetricsBusiness.GetProjectStorageSize,
        ///     which needs OrganizationId to validate org/project ownership). Reuses the same
        ///     ProjectArchivedStatus / ProjectDeleted cache entries as EnsureProjectExistsAsync, but
        ///     always performs a DB fetch to build the DTO regardless of cache state.
        /// </summary>
        public static async Task<ProjectResponseDto> GetProjectExistsAsync(
            DeeplynxContext context,
            long projectId,
            bool hideArchived = true)
        {
            var deletedCacheKey = CacheKeys.ProjectDeleted(projectId);
            var cachedDeleted = await CacheService.Instance.GetAsync<bool?>(deletedCacheKey);
            if (cachedDeleted.HasValue && cachedDeleted.Value)
                throw new KeyNotFoundException($"Project with id {projectId} not found.");

            var project = await context.Projects
                .Where(p => p.Id == projectId)
                .FirstOrDefaultAsync();

            if (project == null)
                throw new KeyNotFoundException($"Project with id {projectId} not found.");

            if (hideArchived && project.IsArchived)
                throw new KeyNotFoundException($"Project with id {projectId} not found.");

            // Opportunistically populate the archived-status cache if it wasn't already set, so a
            // subsequent EnsureProjectExistsAsync call for the same id gets a cache hit.
            var cacheKey = CacheKeys.ProjectArchivedStatus(projectId);
            var cachedIsArchived = await CacheService.Instance.GetAsync<bool?>(cacheKey);
            if (!cachedIsArchived.HasValue)
                await CacheService.Instance.SetAsync(cacheKey, project.IsArchived, (TimeSpan?)null);

            return new ProjectResponseDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                Abbreviation = project.Abbreviation,
                IsArchived = project.IsArchived,
                LastUpdatedAt = project.LastUpdatedAt,
                LastUpdatedBy = project.LastUpdatedBy,
                OrganizationId = project.OrganizationId
            };
        }

        /// <summary>
        ///     Sets the cached archived status for a project, with no expiration. Call this whenever a
        ///     mutation determines a project's archived status directly (create, archive, unarchive).
        /// </summary>
        public static Task SetProjectArchivedStatusCache(long projectId, bool isArchived)
        {
            return CacheService.Instance.SetAsync(CacheKeys.ProjectArchivedStatus(projectId), isArchived, (TimeSpan?)null);
        }

        /// <summary>
        ///     Marks a project as not-existing in the cache with a short TTL, mirroring
        ///     SetUserDeletedCache / SetOrganizationDeletedCache. Also clears the no-TTL
        ///     ProjectArchivedStatus entry so it can't outlive the short TTL and mislead a later read.
        /// </summary>
        public static async Task SetProjectDeletedCache(long projectId)
        {
            await CacheService.Instance.SetAsync(CacheKeys.ProjectDeleted(projectId), true, DeletedProjectCacheTtl);
            await CacheService.Instance.DeleteAsync(CacheKeys.ProjectArchivedStatus(projectId));
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

        
        private static readonly TimeSpan _objectStorageCacheTtl = TimeSpan.FromHours(1);

        public static async Task EnsureObjectStorageExistsAsync(
            DeeplynxContext context,
            long organizationId,
            long projectId,
            long objectStorageId,
            bool hideArchived = true)
        {
            var cacheKey = CacheKeys.ObjectStorageStatus(objectStorageId);
            var cached = await CacheService.Instance.GetAsync<ObjectStorageCacheEntry>(cacheKey);

            ObjectStorageCacheEntry entry;

            if (cached != null)
            {
                entry = cached;
                if (entry.Status == ObjectStorageStatus.Deleted)
                    throw new KeyNotFoundException($"Object Storage with id {objectStorageId} not found.");
            }
            else
            {
                var objectStorage = await context.ObjectStorages
                    .Where(os => os.Id == objectStorageId)
                    .Select(os => new { os.OrganizationId, os.ProjectId, os.IsArchived })
                    .FirstOrDefaultAsync();

                if (objectStorage == null)
                    throw new KeyNotFoundException($"Object Storage with id {objectStorageId} not found.");

                entry = new ObjectStorageCacheEntry
                {
                    OrganizationId = objectStorage.OrganizationId,
                    ProjectId = objectStorage.ProjectId,
                    Status = objectStorage.IsArchived
                            ? ObjectStorageStatus.Archived
                            : ObjectStorageStatus.Active
                };

                await CacheService.Instance.SetAsync(cacheKey, entry, _objectStorageCacheTtl);
            }

            var belongsToScope =
                entry.OrganizationId == organizationId &&
                (entry.ProjectId == null || entry.ProjectId == projectId);

            if (!belongsToScope || entry.Status == ObjectStorageStatus.Deleted)
                throw new KeyNotFoundException($"Object Storage with id {objectStorageId} not found.");

            if (hideArchived && entry.Status == ObjectStorageStatus.Archived)
                throw new KeyNotFoundException($"Object Storage with id {objectStorageId} not found.");
        }
    }
}