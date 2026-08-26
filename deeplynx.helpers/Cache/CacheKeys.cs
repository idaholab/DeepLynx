namespace deeplynx.helpers.Cache;

public class CacheKeys
{
    public static string ProjectStorageSize(long projectId)
    {
        return $"project:{projectId}:storage_size";
    }

    public static string UserArchivedStatus(long userId)
    {
        return $"user:{userId}:archived_status";
    }

    public static string UserDeleted(long userId)
    {
        return $"user:{userId}:deleted";
    }

    public static string OrganizationArchivedStatus(long organizationId)
    {
        return $"organization:{organizationId}:archived_status";
    }

    public static string OrganizationDeleted(long organizationId)
    {
        return $"organization:{organizationId}:deleted";
    }
}