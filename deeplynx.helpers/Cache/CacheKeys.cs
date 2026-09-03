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
    
    public static string ProjectRecordCount(long projectId, bool hideArchived)
    {
        return $"project:{projectId}:record_count:hide_archived:{hideArchived}";
    }

    public static string OrganizationRecordCount(long organizationId, bool hideArchived)
    {
        return $"organization:{organizationId}:record_count:hide_archived:{hideArchived}";
    }

    public static string SystemRecordCount(bool hideArchived)
    {
        return $"system:record_count:hide_archived:{hideArchived}";
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

    public static string ProjectArchivedStatus(long projectId)
    {
        return $"project:{projectId}:archived_status";
    }

    public static string ProjectDeleted(long projectId)
    {
        return $"project:{projectId}:deleted";
    }
    public static string SysAdmin(long userId)
    {
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

    public static string ObjectStorageStatus(long objectStorageId)
    {
        return $"objectstorage:{objectStorageId}:status";
    }

    public static string DataSourceStatus(long dataSourceId)
    {
        return $"datasource:{dataSourceId}:status";
    }

    public static string OrganizationDefaultObjectStorage(long organizationId)
    {
        return $"orgdefaultobjectstorage:{organizationId}";
    }

    public static string ProjectDefaultObjectStorage(long projectId)
    {
        return $"projectdefaultobjectstorage:{projectId}";
    }

    public static string OrganizationDefaultDataSource(long organizationId)
    {
        return $"orgdefaultdatasource:{organizationId}";
    }

    public static string ProjectDefaultDataSource(long projectId)
    {
        return $"projectdefaultdatasource:{projectId}";
    }

    public static string OrganizationDefaultAiModelConfig(long organizationId, string modelType)
    {
        return $"orgdefaultaimodelconfig:{organizationId}:{modelType}";
    }

    public static string ProjectDefaultAiModelConfig(long projectId, string modelType)
    {
        return $"projectdefaultaimodelconfig:{projectId}:{modelType}";
    }

    public static string RecordCountByDataSource(long projectId, long dataSourceId, bool hideArchived)
    {
        return $"recordcountbydatasource:{projectId}:{dataSourceId}:{hideArchived}";
    }

    public static string RecordCountByDataSourcePrefix(long projectId, long dataSourceId)
    {
        return $"recordcountbydatasource:{projectId}:{dataSourceId}:";
    }

    public static string ProjectPermission(long userId, long projectId, string action, string resource)
    {
        return $"projectpermission:{userId}:{projectId}:{action}:{resource}";
    }

    public static string ProjectPermittedIds(long userId, string action, string resource)
    {
        return $"projectpermittedids:{userId}:{action}:{resource}";
    }

    public static string ProjectAuthorizedSensitivityLabels(long projectId, long userId, string action)
    {
        string normalizedAction = action.Trim().ToLowerInvariant().Replace(' ', '-');
        return $"authorizedsensitivitylabels:{projectId}:{userId}:{normalizedAction}";
    }
}