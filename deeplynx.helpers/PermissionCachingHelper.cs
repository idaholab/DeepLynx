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
    }
}