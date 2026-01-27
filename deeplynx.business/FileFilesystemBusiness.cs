using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace deeplynx.business;

public class FileFilesystemBusiness : IFileBusiness
{
    private readonly IClassBusiness _classBusiness;
    private readonly DeeplynxContext _context;
    private readonly IObjectStorageBusiness _objectStorageBusiness;
    private readonly IRecordBusiness _recordBusiness;

    public FileFilesystemBusiness(
        DeeplynxContext context,
        IObjectStorageBusiness objectStorageBusiness,
        IClassBusiness classBusiness,
        IRecordBusiness recordBusiness)
    {
        _context = context;
        _objectStorageBusiness = objectStorageBusiness;
        _classBusiness = classBusiness;
        _recordBusiness = recordBusiness;
    }

    /// <summary>
    ///     Uploads a file to the local file system
    /// </summary>
    /// <param name="organizationId">The organization the file is associated with</param>
    /// <param name="projectId">The project the file is associated with</param>
    /// <param name="dataSourceId">The data source the file is associated with</param>
    /// <param name="objectStorageConfig">The config containing the file path</param>
    /// <param name="file">The file the user wants to upload</param>
    /// <param name="guid">The unique identifier for file names</param>
    public async Task<string> UploadFile(
        long organizationId,
        long projectId,
        long dataSourceId,
        ObjectStorageConfigDto objectStorageConfig,
        IFormFile file,
        Guid guid
    )
    {
        // TODO: Cache these
        if (objectStorageConfig.MountPath == null)
            throw new Exception("File system mount path not set in object storage");

        var fileName = $"{guid}_{file.FileName}";

        // create a file path in the format <mountdir>/project_<id>/datasource_<id>/filename
        var filePath = Path.Combine(
            objectStorageConfig.MountPath,
            "org_" + organizationId,
            "project_" + projectId,
            "datasource_" + dataSourceId,
            fileName);
        // create the directory for the file if not exists
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ??
                                  throw new InvalidOperationException("error creating upload path."));

        // copy the file to its new location
        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return filePath;
    }

    /// <summary>
    ///     Replaces local file
    /// </summary>
    /// <param name="record">The record the file info is in</param>
    /// <param name="file">The file that the user wants to change the old file to</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public async Task<string> UpdateFile(RecordResponseDto record, IFormFile file)
    {
        var filePath = record.Uri;

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is not specified in the record.");

        if (!File.Exists(filePath)) throw new FileNotFoundException("The file to update does not exist.", filePath);

        var directory = Path.GetDirectoryName(filePath);

        if (directory == null || !Directory.Exists(directory))
            throw new DirectoryNotFoundException("Directory not found.");

        var newFileName = $"{record.OriginalId}_{file.FileName}";

        var updatedPath = Path.Combine(directory, newFileName);

        // Delete the original file
        File.Delete(filePath);

        //write new file
        await using (var stream = new FileStream(updatedPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return updatedPath;
    }

    /// <summary>
    ///     Downloads a file from local file storage
    /// </summary>
    /// <param name="record">The record that has the file info</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<FileStreamResult> DownloadFile(RecordResponseDto record)
    {
        var filePath = record.Uri;
        if (filePath == null) throw new ArgumentNullException("File path/uri is not specified in the record.");
        var fileName = record.Name;

        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path must be provided.");

        if (!File.Exists(filePath)) throw new FileNotFoundException("The requested file does not exist.", filePath);

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Detect file type
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(filePath, out var contentType))
            contentType = "application/octet-stream"; // Default fallback

        return new FileStreamResult(stream, contentType)
        {
            FileDownloadName = fileName
        };
    }

    /// <summary>
    ///     Deletes a file from local file storage
    /// </summary>
    /// <param name="record">Record that contains file info</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<bool> DeleteFile(RecordResponseDto record)
    {
        var filePath = record.Uri;
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is not specified in the record.");

        if (!File.Exists(filePath)) throw new FileNotFoundException("The file to update does not exist.", filePath);

        File.Delete(filePath);

        var objectStorage = await _context.ObjectStorages.FirstOrDefaultAsync(os =>
            os.ProjectId == record.ProjectId && os.Id == record.ObjectStorageId && !os.IsArchived);

        if (objectStorage == null) throw new Exception("Object storage does not exist.");
        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);

        var directory = Path.GetDirectoryName(filePath);

        if (configData == null || configData.MountPath == null)
            throw new Exception("File system mount path not set in object storage");

        // Normalize paths for comparison
        var normalizedBasePath = Path.GetFullPath(configData.MountPath).TrimEnd(Path.DirectorySeparatorChar);

        // deletes all empty directories up to but not including the base path
        while (!string.IsNullOrEmpty(directory) &&
               Directory.Exists(directory) &&
               !Path.GetFullPath(directory).Equals(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
            if (Directory.GetFileSystemEntries(directory).Length == 0)
            {
                Directory.Delete(directory);
                directory = Path.GetDirectoryName(directory);
            }
            else
            {
                break;
            }

        return true;
    }
}