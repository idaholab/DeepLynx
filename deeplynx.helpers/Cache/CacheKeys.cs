namespace deeplynx.helpers.Cache;

public class CacheKeys
{
    public static string ProjectStorageSize(long projectId)
    {
        return $"project:{projectId}:storage_size";
    }

    public static string ProjectDataSourceCount(long projectId, bool hideArchived)
    {
        return $"project:{projectId}:data_source_count:hide_archived:{hideArchived}";
    }

    public static string OrganizationDataSourceCount(long organizationId, bool hideArchived)
    {
        return $"organization:{organizationId}:data_source_count:hide_archived:{hideArchived}";
    }

    public static string SystemDataSourceCount(bool hideArchived)
    {
        return $"system:data_source_count:hide_archived:{hideArchived}";
    }

    public static string UserArchivedStatus(long userId)
    {
        return $"user:{userId}:archived_status";
    }

    public static string UserDeleted(long userId)
    {
        return $"user:{userId}:deleted";
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