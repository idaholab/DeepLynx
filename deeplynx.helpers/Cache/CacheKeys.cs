namespace deeplynx.helpers.Cache;

public class CacheKeys
{
    public static string ProjectStorageSize(long projectId)
    {
        return $"project:{projectId}:storage_size";
    }
}