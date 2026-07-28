using deeplynx.models;

namespace deeplynx.interfaces;

public interface IMaintenanceBusiness
{
    Task<List<TimeseriesMigrationRecordDto>> GetTimeseriesMigrationRecords();

    Task<bool> ExportDuckDbTableToFile(long recordId);

    Task<long> ScrapeObjectStorageToCatalog(
        long objectStorageId,
        long currentUserId,
        long dataSourceId,
        List<long>? sensitivityLabelIds = null,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false);
}
