using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace deeplynx.business;

public class FileBusiness
{
    private readonly IClassBusiness _classBusiness;
    private readonly DeeplynxContext _context;
    private readonly IDataSourceBusiness _dataSourceBusiness;
    private readonly IFileBusinessFactory _factory;
    private readonly IObjectStorageBusiness _objectStorageBusiness;
    private readonly long _recommendedChunkSize;
    private readonly IRecordBusiness _recordBusiness;

    // NOTE: Chunked upload methods currently only support filesystem storage.
    // When Azure/S3 chunked uploads are needed, refactor these methods to 
    // delegate to storage-specific implementations (IFileBusiness interface).
    public FileBusiness(
        DeeplynxContext context,
        IFileBusinessFactory factory,
        IObjectStorageBusiness objectStorageBusiness,
        IDataSourceBusiness dataSourceBusiness,
        IClassBusiness classBusiness,
        IRecordBusiness recordBusiness)
    {
        _context = context;
        _factory = factory;
        _objectStorageBusiness = objectStorageBusiness;
        _dataSourceBusiness = dataSourceBusiness;
        _classBusiness = classBusiness;
        _recordBusiness = recordBusiness;

        // Initialize recommended chunk size from environment variable
        var chunkSizeStr = Environment.GetEnvironmentVariable("RECOMMENDED_CHUNK_SIZE")
                           ?? throw new InvalidOperationException(
                               "RECOMMENDED_CHUNK_SIZE environment variable is not set");

        if (!long.TryParse(chunkSizeStr, out var chunkSize) || chunkSize <= 0)
            throw new InvalidOperationException("RECOMMENDED_CHUNK_SIZE must be a positive number");

        _recommendedChunkSize = chunkSize;
    }

    /// <summary>
    ///     Uploads file using specified object storage method
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">ID of the Organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">ID of the object storage method to use</param>
    /// <param name="file">file to upload</param>
    public async Task<RecordResponseDto> UploadFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        IFormFile file)
    {
        long realDataSourceId;
        if (file == null || file.Length == 0) throw new ArgumentException("File is required and cannot be empty.");
        if (dataSourceId.HasValue)
        {
            await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId.Value, projectId);
            realDataSourceId = dataSourceId.Value;
        }
        else
        {
            var defaultDataSource = await _dataSourceBusiness.GetDefaultDataSource(organizationId, projectId) ??
                                    throw new KeyNotFoundException("Default data source not found");
            realDataSourceId = defaultDataSource.Id;
        }

        ObjectStorage? objectStorage;

        if (objectStorageId is not null)
        {
            objectStorage = await _context.ObjectStorages.FirstOrDefaultAsync(os => os.Id == objectStorageId
                && os.ProjectId == projectId
                && !os.IsArchived
            );
        }
        else
        {
            var defaultObjectStorageResponseDto = await _objectStorageBusiness.GetDefaultObjectStorage(
                organizationId, projectId);
            objectStorage = await _context.ObjectStorages.FindAsync(defaultObjectStorageResponseDto.Id);
        }

        if (objectStorage is null) throw new KeyNotFoundException("No object storage found for project");

        // Check config to confirm it is valid (could be part of the object storage fetch)
        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null");

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var guid = Guid.NewGuid();

        var uri = await fileBusiness.UploadFile(organizationId, projectId, realDataSourceId, configData, file, guid);

        var fileClass = await _classBusiness.GetOrCreateClass(currentUserId, organizationId, projectId, "File");
        var recordRequest = new CreateRecordRequestDto
        {
            Properties = new JsonObject
            {
                ["fileType"] = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
            },
            Name = file.FileName,
            ObjectStorageId = objectStorage.Id,
            Description = file.FileName,
            OriginalId = guid.ToString(),
            Uri = uri,
            ClassId = fileClass.Id,
            ClassName = fileClass.Name,
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
        };

        // return the newly created metadata record for the file
        return await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, realDataSourceId,
            recordRequest);
    }

    /// <summary>
    ///     Relaces a file but uses the same guid for the file name
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="recordId">ID of record that contains the info of the file to replace</param>
    /// <param name="file">file to update to</param>
    public async Task<RecordResponseDto> UpdateFile(long currentUserId, long organizationId, long projectId,
        long recordId, IFormFile file)
    {
        var record = await _recordBusiness.GetRecord(organizationId, projectId, recordId, true);
        if (file == null || file.Length == 0) throw new ArgumentException("File is required and cannot be empty.");

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage =
            await _objectStorageBusiness.GetObjectStorage(organizationId, projectId, record.ObjectStorageId.Value,
                true);

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var uri = await fileBusiness.UpdateFile(record, file);

        var updateRecordRequest = new UpdateRecordRequestDto
        {
            Properties = new JsonObject
            {
                ["fileType"] = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
            },
            Name = file.FileName,
            Uri = uri,
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
        };
        return await _recordBusiness.UpdateRecord(currentUserId, organizationId, projectId, recordId,
            updateRecordRequest);
    }

    /// <summary>
    ///     Downloads file
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="recordId">ID of record that contains the info of the file to download</param>
    public async Task<FileStreamResult> DownloadFile(long organizationId, long projectId, long recordId)
    {
        var record = await _recordBusiness.GetRecord(organizationId, projectId, recordId, true);
        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");
        var objectStorage =
            await _objectStorageBusiness.GetObjectStorage(organizationId, projectId, record.ObjectStorageId.Value,
                true);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        return await fileBusiness.DownloadFile(record);
    }

    /// <summary>
    ///     Deletes a file
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="recordId">ID of record that contains the info of the file to delete</param>
    public async Task<bool> DeleteFile(long currentUserId, long organizationId, long projectId, long recordId)
    {
        var record = await _recordBusiness.GetRecord(organizationId, projectId, recordId, true);
        if (record == null) throw new KeyNotFoundException("Record not found");
        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");
        var objectStorage =
            await _objectStorageBusiness.GetObjectStorage(organizationId, projectId, record.ObjectStorageId.Value,
                true);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        await fileBusiness.DeleteFile(record);
        return await _recordBusiness.DeleteRecord(currentUserId, organizationId, projectId, recordId);
    }


    /// <summary>
    ///     Initializes a chunked upload session
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">ID of the Organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">ID of the object storage method to use</param>
    /// <param name="request">File upload initialization request</param>
    public async Task<FileUploadSessionResponseDto> StartUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadInitRequestDto request)
    {
        long realDataSourceId;
        if (dataSourceId.HasValue)
        {
            await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId.Value, projectId);
            realDataSourceId = dataSourceId.Value;
        }
        else
        {
            var defaultDataSource = await _dataSourceBusiness.GetDefaultDataSource(organizationId, projectId) ??
                                    throw new KeyNotFoundException("Default data source not found");
            realDataSourceId = defaultDataSource.Id;
        }

        ObjectStorage? objectStorage;
        if (objectStorageId is not null)
        {
            objectStorage = await _context.ObjectStorages.FirstOrDefaultAsync(os => os.Id == objectStorageId
                && os.ProjectId == projectId
                && !os.IsArchived
            );
        }
        else
        {
            var defaultObjectStorageResponseDto = await _objectStorageBusiness.GetDefaultObjectStorage(
                organizationId, projectId);
            objectStorage = await _context.ObjectStorages.FindAsync(defaultObjectStorageResponseDto.Id);
        }

        if (objectStorage is null) throw new KeyNotFoundException("No object storage found for project");

        // Get the config to extract mount path
        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null");
        if (configData.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        var uploadId = Guid.NewGuid().ToString();

        // Create directory for chunks based on storage type
        var uploadPath = Path.Combine(
            configData.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{realDataSourceId}",
            "uploads",
            uploadId
        );
        Directory.CreateDirectory(uploadPath);

        // Calculate total chunks needed
        var totalChunks = (int)Math.Ceiling((double)request.FileSize / _recommendedChunkSize);

        return new FileUploadSessionResponseDto
        {
            UploadId = uploadId,
            ChunkSize = _recommendedChunkSize,
            TotalChunks = totalChunks
        };
    }

    /// <summary>
    ///     Uploads a single chunk of a file
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">ID of the Organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">ID of the object storage method to use</param>
    /// <param name="chunk">File chunk from form</param>
    /// <param name="uploadId">The upload session ID from StartUpload</param>
    /// <param name="chunkNumber">The index for tracking the order to merge chunks together</param>
    public async Task<string> UploadChunk(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        IFormFile chunk,
        string uploadId,
        int chunkNumber)
    {
        // Resolve data source
        long realDataSourceId;
        if (dataSourceId.HasValue)
        {
            await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId.Value, projectId);
            realDataSourceId = dataSourceId.Value;
        }
        else
        {
            var defaultDataSource = await _dataSourceBusiness.GetDefaultDataSource(organizationId, projectId) ??
                                    throw new KeyNotFoundException("Default data source not found");
            realDataSourceId = defaultDataSource.Id;
        }

        // Resolve object storage to get mount path
        ObjectStorage? objectStorage;
        if (objectStorageId is not null)
        {
            objectStorage = await _context.ObjectStorages.FirstOrDefaultAsync(os => os.Id == objectStorageId
                && os.ProjectId == projectId
                && !os.IsArchived
            );
        }
        else
        {
            var defaultObjectStorageResponseDto = await _objectStorageBusiness.GetDefaultObjectStorage(
                organizationId, projectId);
            objectStorage = await _context.ObjectStorages.FindAsync(defaultObjectStorageResponseDto.Id);
        }

        if (objectStorage is null) throw new KeyNotFoundException("No object storage found for project");

        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null");
        if (configData.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        // Use mount path from object storage config
        var uploadPath = Path.Combine(
            configData.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{realDataSourceId}",
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

            return "success";
        }
        catch (Exception)
        {
            // Cleanup chunk file on failure
            if (File.Exists(chunkFilePath))
                File.Delete(chunkFilePath);

            throw;
        }
    }

    /// <summary>
    ///     Completes the upload by merging chunks and creating the file record
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">ID of the Organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">ID of the object storage method to use</param>
    /// <param name="request">File upload completion request</param>
    public async Task<RecordResponseDto> CompleteUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadCompleteRequestDto request)
    {
        // Resolve data source
        long realDataSourceId;
        if (dataSourceId.HasValue)
        {
            await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId.Value, projectId);
            realDataSourceId = dataSourceId.Value;
        }
        else
        {
            var defaultDataSource = await _dataSourceBusiness.GetDefaultDataSource(organizationId, projectId) ??
                                    throw new KeyNotFoundException("Default data source not found");
            realDataSourceId = defaultDataSource.Id;
        }

        // Resolve object storage
        ObjectStorage? objectStorage;
        if (objectStorageId is not null)
        {
            objectStorage = await _context.ObjectStorages.FirstOrDefaultAsync(os => os.Id == objectStorageId
                && os.ProjectId == projectId
                && !os.IsArchived
            );
        }
        else
        {
            var defaultObjectStorageResponseDto = await _objectStorageBusiness.GetDefaultObjectStorage(
                organizationId, projectId);
            objectStorage = await _context.ObjectStorages.FindAsync(defaultObjectStorageResponseDto.Id);
        }

        if (objectStorage is null) throw new KeyNotFoundException("No object storage found for project");

        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null");
        if (configData.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        // Use mount path from object storage config
        var uploadPath = Path.Combine(
            configData.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{realDataSourceId}",
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

            // Upload merged file to object storage using the existing file business logic
            var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
            var guid = Guid.NewGuid();

            // Create IFormFile from merged file for upload
            await using var fileStream = new FileStream(mergedFilePath, FileMode.Open, FileAccess.Read);
            var formFile = new FormFile(fileStream, 0, fileStream.Length, "file", request.FileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };

            var uri = await fileBusiness.UploadFile(organizationId, projectId, realDataSourceId, configData, formFile,
                guid);

            // Clean up merged file and upload directory
            fileStream.Close();
            File.Delete(mergedFilePath);
            Directory.Delete(uploadPath, true);

            // Create file record
            var fileClass = await _classBusiness.GetOrCreateClass(currentUserId, organizationId, projectId, "File");
            var recordRequest = new CreateRecordRequestDto
            {
                Properties = new JsonObject
                {
                    ["fileType"] = Path.GetExtension(request.FileName).TrimStart('.').ToLower(),
                    ["uploadedViaChunking"] = true,
                    ["originalUploadId"] = request.UploadId
                },
                Name = request.FileName,
                ObjectStorageId = objectStorage.Id,
                Description = $"File uploaded via chunked upload (session: {request.UploadId})",
                OriginalId = guid.ToString(),
                Uri = uri,
                ClassId = fileClass.Id,
                ClassName = fileClass.Name,
                FileType = Path.GetExtension(request.FileName).TrimStart('.').ToLower()
            };

            return await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, realDataSourceId,
                recordRequest);
        }
        catch (Exception)
        {
            // Cleanup on failure
            if (File.Exists(mergedFilePath))
                File.Delete(mergedFilePath);

            if (Directory.Exists(uploadPath))
                Directory.Delete(uploadPath, true);

            throw;
        }
    }

    /// <summary>
    ///     Cancels an in-progress upload and cleans up temporary files
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">ID of the Organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">ID of the object storage method to use</param>
    /// <param name="uploadId">ID of upload session to cancel</param>
    public async Task CancelUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        string uploadId)
    {
        // Resolve data source
        long realDataSourceId;
        if (dataSourceId.HasValue)
        {
            await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId.Value, projectId);
            realDataSourceId = dataSourceId.Value;
        }
        else
        {
            var defaultDataSource = await _dataSourceBusiness.GetDefaultDataSource(organizationId, projectId) ??
                                    throw new KeyNotFoundException("Default data source not found");
            realDataSourceId = defaultDataSource.Id;
        }

        ObjectStorage? objectStorage;
        if (objectStorageId is not null)
        {
            objectStorage = await _context.ObjectStorages.FirstOrDefaultAsync(os => os.Id == objectStorageId
                && os.ProjectId == projectId
                && !os.IsArchived
            );
        }
        else
        {
            var defaultObjectStorageResponseDto = await _objectStorageBusiness.GetDefaultObjectStorage(
                organizationId, projectId);
            objectStorage = await _context.ObjectStorages.FindAsync(defaultObjectStorageResponseDto.Id);
        }

        if (objectStorage is null) throw new KeyNotFoundException("No object storage found for project");

        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null");
        if (configData.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        var uploadPath = Path.Combine(
            configData.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{realDataSourceId}",
            "uploads",
            uploadId
        );

        if (Directory.Exists(uploadPath))
            Directory.Delete(uploadPath, true);

        await Task.CompletedTask;
    }
}