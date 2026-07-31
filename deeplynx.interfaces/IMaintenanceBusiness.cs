using deeplynx.models;

namespace deeplynx.interfaces;

public interface IMaintenanceBusiness
{
    Task<List<TimeseriesMigrationRecordDto>> GetTimeseriesMigrationRecords();

    Task<bool> ExportDuckDbTableToFile(long recordId);

    Task<FileStorageMigrationResponseDto> MigrateFilesystemRecordsToAzure(
        FileStorageMigrationRequestDto request,
        CancellationToken cancellationToken = default);
}
