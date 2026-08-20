namespace deeplynx.helpers.Cache;

public class CacheKeys
{
    public static string ProjectStorageSize(long projectId)
    {
        return $"project:{projectId}:storage_size";
    }

    public static string SysAdmin(long userId) {
        return $"sysadmin:{userId}";
    }

    public static string OrgAdmin(long userId, long organizationId) 
    {
        return $"orgadmin:{userId}:{organizationId}";  
    }

    public static string OrgMember(long userId, long organizationId) 
    {
        return $"orgmember:{userId}:{organizationId}";
    }
    
    public static string ProjectAdmin(long userId, long organizationId, List<long> projectIds)
    {
        var idsPart = string.Join(",", projectIds.OrderBy(id => id));
        return $"projectadmin:{userId}:{organizationId}:{idsPart}";
    }
}