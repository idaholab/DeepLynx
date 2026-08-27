namespace deeplynx.helpers.Cache;

public class CacheKeys
{
    public static string ProjectStorageSize(long projectId)
    {
        return $"project:{projectId}:storage_size";
    }

    public static string OrganizationDefaultObjectStorage(long organizationId)
    {
        return $"orgdefaultobjectstorage:{organizationId}";
    }

    public static string ProjectDefaultObjectStorage(long projectId)
    {
        return $"projectdefaultobjectstorage:{projectId}";
    }
}