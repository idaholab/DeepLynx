namespace deeplynx.helpers.Cache;

public class CacheKeys
{
    public static string ProjectStorageSize(long projectId)
    {
        return $"project:{projectId}:storage_size";
    }

    public static string UserExists(long userId, bool hideArchived)
    {
        return $"user:{userId}:exists:hide_archived:{hideArchived}";
    }
}