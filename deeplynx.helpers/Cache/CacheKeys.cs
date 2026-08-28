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

    public static string OrganizationArchivedStatus(long organizationId)
    {
        return $"organization:{organizationId}:archived_status";
    }

    public static string OrganizationDeleted(long organizationId)
    {
        return $"organization:{organizationId}:deleted";
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
    
    public static string ProjectAdmin(long userId, long projectId)
    {
        return $"projectadmin:{userId}:{projectId}";
    }

    public static string OrgPermission(long userId, long organizationId, string action, string resource)
    {
        return $"orgpermission:{userId}:{organizationId}:{action}:{resource}";
    }

    public static string ProjectPermission(long userId, long projectId, string action, string resource)
    {
        return $"projectpermission:{userId}:{projectId}:{action}:{resource}";
    }

    public static string ProjectPermittedIds(long userId, string action, string resource)
    {
        return $"projectpermittedids:{userId}:{action}:{resource}";
    }
}