using deeplynx.models;

namespace deeplynx.interfaces;

public interface IMaintenanceBusiness
{
    Task<List<TimeseriesMigrationRecordDto>> GetTimeseriesMigrationRecords();

    Task<bool> ExportDuckDbTableToFile(long recordId);

    Task<ScrapeObjectStorageResponseDto> ScrapeObjectStorageToCatalog(
        long objectStorageId,
        long currentUserId,
        string? afterCursor = null,
        int batchSize = 500,
        int maxBatches = 5,
        List<long>? sensitivityLabelIds = null,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false,
        CancellationToken cancellationToken = default);
}
