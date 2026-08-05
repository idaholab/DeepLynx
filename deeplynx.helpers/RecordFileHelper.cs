using deeplynx.datalayer.Models;
using deeplynx.helpers.Cache;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.helpers;

/// <summary>
/// Synchronizes actions for records and their files.
/// </summary>
public static class RecordFileHelper
{
    /// <summary>
    /// Attempts to delete the records' attached files.
    /// Records without files or with object storage deletion protection are skipped.
    /// </summary>
    /// <param name="records">The records to filter and search. Project must not be null</param>
    /// <param name="factory">The file business factory</param>
    /// <param name="objectStorageBusiness">Object storage for accessing the file where it's stored</param>
    /// <returns></returns>
    public static async Task TryDeleteFiles(
        IQueryable<Record> records, IFileBusinessFactory factory, IObjectStorageBusiness objectStorageBusiness)
    {
        var filtered = await FilterByDeleteEligibleFiles(records).ToListAsync();
        foreach (var r in filtered)
            await DeleteFile(r, factory, objectStorageBusiness);
        // Can be optimized by grouping context operations like accessing object storage separate
        // await Task.WhenAll(filtered.Select(r => DeleteFile(r, factory, objectStorageBusiness)));
    }

    private static async Task DeleteFile(Record record, IFileBusinessFactory factory, IObjectStorageBusiness objectStorageBusiness)
    {
        var objectStorage = await objectStorageBusiness.GetDecryptedObjectStorage(record.ObjectStorageId!.Value);
        var fileBusiness = factory.CreateFileBusiness(objectStorage.Type);

        var dto = new RecordResponseDto { Uri = record.Uri! };
        try
        {
            await fileBusiness.DeleteFile(dto, objectStorage.Config);
        }
        catch (FileNotFoundException)
        {
            // File could've been deleted/moved internally or record misconfigured
            // Either way, this is a delete attempt and should not break the whole program because of a minor misconfig
            // Would be nice to log the misconfigured file path, but exposing the URI requires special user privileges
        }

        await InvalidateProjectStorageSizeCache(record.ProjectId);
    }

    private static IQueryable<Record> FilterByDeleteEligibleFiles(IQueryable<Record> records) =>
        // ObjectStorage + Uri + FileType is a practical signal for a DeepLynx file upload.
        records
        .Include(r => r.ObjectStorage)
        .Where(r =>
            !string.IsNullOrWhiteSpace(r.Uri) &&
            !string.IsNullOrWhiteSpace(r.FileType) &&
            r.ObjectStorageId != null &&
            r.ObjectStorage != null &&
            r.ObjectStorage.FilesDeletable
        );

    // Invalidates the storage cache. To be called when deleting a file.
    private static Task<bool> InvalidateProjectStorageSizeCache(long projectId) =>
        CacheService.Instance.DeleteAsync(CacheKeys.ProjectStorageSize(projectId));
}
