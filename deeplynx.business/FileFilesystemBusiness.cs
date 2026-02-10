using deeplynx.datalayer.Models;
using deeplynx.helpers;
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
    public async Task<string> UpdateFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig, IFormFile file, Guid guid)
    {
        var filePath = record.Uri;

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is not specified in the record.");

        if (!File.Exists(filePath)) throw new FileNotFoundException("The file to update does not exist.", filePath);

        var directory = Path.GetDirectoryName(filePath);

        if (directory == null || !Directory.Exists(directory))
            throw new DirectoryNotFoundException("Directory not found.");

        var newFileName = $"{guid}_{file.FileName}";

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
    /// <param name="objectStorageConfig">The configuration data of the object storage</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<FileStreamResult> DownloadFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig)
    {
        var filePath = record.Uri;
        if (filePath == null) throw new ArgumentNullException("File path/uri is not specified in the record.");
        var fileName = record.Name;

        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path must be provided.");

        if (!File.Exists(filePath)) throw new FileNotFoundException("The requested file does not exist.", filePath);

        // Get file info for size
        var fileInfo = new FileInfo(filePath);
        var contentLength = fileInfo.Length;

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Detect file type
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(filePath, out var contentType))
            contentType = "application/octet-stream"; // Default fallback

        return new FileStreamResultWithLength(stream, contentType, contentLength)
        {
            FileDownloadName = fileName,
            EnableRangeProcessing = true
        };
    }

    /// <summary>
    ///     Deletes a file from local file storage
    /// </summary>
    /// <param name="record">Record that contains file info</param>
    /// <param name="objectStorageConfig">Contains the config info</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<bool> DeleteFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig)
    {
        var filePath = record.Uri;
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is not specified in the record.");

        if (!File.Exists(filePath)) throw new FileNotFoundException("The file to update does not exist.", filePath);

        File.Delete(filePath);
        
        var directory = Path.GetDirectoryName(filePath);

        if (objectStorageConfig.MountPath == null)
            throw new Exception("File system mount path not set in object storage");

        // Normalize paths for comparison
        var normalizedBasePath = Path.GetFullPath(objectStorageConfig.MountPath).TrimEnd(Path.DirectorySeparatorChar);

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

    /// <summary>
    /// Sets up the file path before the chunks are uploaded there
    /// </summary>
    /// <param name="objectStorageConfig">Config allowing chunking to be set up and tested</param>
    public async Task<Guid> StartUpload(long organizationId, long projectId, long datasourceId, ObjectStorageConfigDto objectStorageConfig)
    {
        var uploadId = Guid.NewGuid();
        
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");
        
        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            uploadId.ToString()
        );
        Directory.CreateDirectory(uploadPath);
        
        return uploadId;
    }

    public async Task UploadChunk(long organizationId, long projectId, long datasourceId, long chunkNumber, string uploadId,
        ObjectStorageConfigDto objectStorageConfig, IFormFile chunk)
    {
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        // Use mount path from object storage config
        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            uploadId
        );
        var chunkFilePath = Path.Combine(uploadPath, $"{chunkNumber}.part");

        try
        {
            if (chunk == null || chunk.Length == 0)
                throw new ArgumentException("No chunk data provided");

            if (!Directory.Exists(uploadPath))
                throw new InvalidOperationException($"Upload session {uploadId} not found or expired");

            // Write chunk to disk
            await using var stream = new FileStream(chunkFilePath, FileMode.Create);
            await chunk.CopyToAsync(stream);
        }
        catch (Exception)
        {
            // Cleanup chunk file on failure
            if (File.Exists(chunkFilePath))
                File.Delete(chunkFilePath);

            throw;
        }
    }

    public async Task<string> CompleteUpload(long organizationId, long projectId, long datasourceId, ObjectStorageConfigDto objectStorageConfig, FileUploadCompleteRequestDto request, Guid guid)
    {
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        // Use mount path from object storage config
        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            request.UploadId
        );
        var mergedFileName = $"{request.UploadId}_{request.FileName}";
        var mergedFilePath = Path.Combine(uploadPath, mergedFileName);

        try
        {
            if (!Directory.Exists(uploadPath))
                throw new InvalidOperationException($"Upload session {request.UploadId} not found");

            // Merge all chunks into final file
            await using (var finalFileStream = new FileStream(mergedFilePath, FileMode.Create))
            {
                for (var i = 0; i < request.TotalChunks; i++)
                {
                    var chunkFilePath = Path.Combine(uploadPath, $"{i}.part");

                    if (!File.Exists(chunkFilePath))
                        throw new InvalidOperationException($"Missing chunk {i} of {request.TotalChunks}");

                    await using (var chunkStream = new FileStream(chunkFilePath, FileMode.Open))
                    {
                        await chunkStream.CopyToAsync(finalFileStream);
                    }

                    File.Delete(chunkFilePath); // Clean up chunk after merging
                }
            }
            

            // Create IFormFile from merged file for upload
            await using var fileStream = new FileStream(mergedFilePath, FileMode.Open, FileAccess.Read);
            var formFile = new FormFile(fileStream, 0, fileStream.Length, "file", request.FileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };

            var uri = await UploadFile(organizationId, projectId, datasourceId, objectStorageConfig, formFile,
                guid);

            // Clean up merged file and upload directory
            fileStream.Close();
            File.Delete(mergedFilePath);
            Directory.Delete(uploadPath, true);

            return uri;
        }
        catch
        {
            if (File.Exists(mergedFilePath))
                File.Delete(mergedFilePath);

            if (Directory.Exists(uploadPath))
                Directory.Delete(uploadPath, true);

            throw;
        }
    }

    public async Task CancelUpload(long organizationId, long projectId, long dataSourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{dataSourceId}",
            "uploads",
            uploadId
        );

        if (Directory.Exists(uploadPath))
            Directory.Delete(uploadPath, true);
    }
}