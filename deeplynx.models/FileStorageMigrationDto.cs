using System.ComponentModel.DataAnnotations;

namespace deeplynx.models;

public class FileStorageMigrationRequestDto
{
    [Range(1, long.MaxValue)]
    public long OrganizationId { get; set; }

    [Range(1, long.MaxValue)]
    public long SourceObjectStorageId { get; set; }

    [Range(1, long.MaxValue)]
    public long TargetObjectStorageId { get; set; }

    public long? AfterRecordId { get; set; }

    public long? RecordId { get; set; }

    [Range(1, 100)]
    public int BatchSize { get; set; } = 25;

    // A caller must explicitly opt in to changing storage and database data.
    public bool DryRun { get; set; } = true;
}

public class FileStorageMigrationResponseDto
{
    public bool DryRun { get; set; }
    public int Scanned { get; set; }
    public int Migrated { get; set; }
    public int Failed { get; set; }
    public long? LastRecordId { get; set; }
    public List<FileStorageMigrationItemDto> Items { get; set; } = [];
}

public class FileStorageMigrationItemDto
{
    public long RecordId { get; set; }
    public string? SourceUri { get; set; }
    public string? DestinationUri { get; set; }
    public string Status { get; set; } = null!;
    public string? Error { get; set; }
}
