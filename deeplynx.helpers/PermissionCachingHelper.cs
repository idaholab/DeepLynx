using deeplynx.datalayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers
{
    public static class PermissionCachingHelper
    {
        public static async Task InvalidateProjectPermissionsCache(long userId, long projectId, ILogger? logger = null)
        {
            try
            {
                await CacheService.Instance.DeleteByPrefixAsync($"projectpermission:{userId}:{projectId}:");
                await CacheService.Instance.DeleteByPrefixAsync($"projectpermittedids:{userId}:");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Cache overwrite for permissions failed for user {UserId}, project {ProjectId}", userId, projectId);
            }
        }

        public static async Task InvalidateProjectPermissionsCache(
            DeeplynxContext context,
            long projectId,
            long? userId,
            long? groupId,
            ILogger? logger = null)
        {
            if (userId.HasValue)
            {
                await InvalidateProjectPermissionsCache(userId.Value, projectId, logger);

                return;
            }

            if (!groupId.HasValue)
                return;

            var groupUserIds = await context.Groups
                .Where(g => g.Id == groupId.Value)
                .SelectMany(g => g.Users)
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var groupUserId in groupUserIds)
            {
                await InvalidateProjectPermissionsCache(groupUserId, projectId, logger);
            }
        }
    }
}