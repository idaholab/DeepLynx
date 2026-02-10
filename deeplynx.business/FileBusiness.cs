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
    /// <param name="sensitivityLabelIds">The IDs of the Sensitivity Labels that will be attached to the record</param>
    public async Task<RecordResponseDto> UploadFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        IFormFile file,
        List<long>? sensitivityLabelIds = null)
    {
        var sensitivityLabelsRequired =
            await PermissionHelper.SensitivityLabelRequired(_context, organizationId, projectId);

        if (sensitivityLabelsRequired && (sensitivityLabelIds == null || sensitivityLabelIds.Count == 0))
        {
            throw new InvalidOperationException("Sensitivity labels are required");
        }
        
        // if the user provides Sensitivity Labels ensure that the user is authorized to upload files
        if (sensitivityLabelIds?.Count > 0)
        {
            var authorizedLabelIds = await PermissionHelper.GetAuthorizedSensitivityLabels(
                _context, currentUserId, organizationId, projectId,"upload file");
            
            var hasAuthorization = sensitivityLabelIds.All(sl => authorizedLabelIds.Contains(sl));

            if (!hasAuthorization)
            {
                throw new UnauthorizedAccessException("You do not have upload file permissions for all provided sensitivity labels");
            }
        }
        
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
            objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, objectStorageId.Value);
        }
        else
        {
            // Works for now, but will need to change depending on how we are managing default org and project object
            // storages in the future
            objectStorage = _context.ObjectStorages.FirstOrDefault(os => os.OrganizationId == organizationId && 
                                                                         os.ProjectId == projectId && 
                                                                         os.Default);
        }

        if (objectStorage is null) throw new KeyNotFoundException("No object storage found for project");

        // Check config to confirm it is valid
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
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        // if record has sensitivity labels then ensure the user has update file permissions
        if (record.Labels.Count > 0)
        {
            var authorizedSensitivityLabelIds = await PermissionHelper.GetAuthorizedSensitivityLabels(
                _context, currentUserId, organizationId, projectId, "update file");

            var hasUpdateFilePermissions = record.Labels.All(l => authorizedSensitivityLabelIds.Contains(l.Id));

            if (!hasUpdateFilePermissions)
                throw new UnauthorizedAccessException(
                    $"You do not have update file permissions for all sensitivity labels on record {recordId}");
        }

        if (file == null || file.Length == 0) throw new ArgumentException("File is required and cannot be empty.");

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, record.ObjectStorageId.Value);
        
        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null or invalid");

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        
        var guid = Guid.NewGuid();

        var uri = await fileBusiness.UpdateFile(record, configData, file, guid);

        var updateRecordRequest = new UpdateRecordRequestDto
        {
            Properties = new JsonObject
            {
                ["fileType"] = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
            },
            OriginalId = guid.ToString(),
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
    /// <param name="currentUserId">ID of current user making the request</param>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="recordId">ID of record that contains the info of the file to download</param>
    public async Task<FileStreamResult> DownloadFile(long currentUserId, long organizationId, long projectId, long recordId)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        // If record has sensitivity labels then ensure the user has download file permissions
        if (record.Labels.Count > 0)
        {
            var authorizedSensitivityLabelIds = await PermissionHelper.GetAuthorizedSensitivityLabels(
                _context, currentUserId, organizationId, projectId, "download file");

            var hasDownloadFilePermissions = record.Labels.All(l => authorizedSensitivityLabelIds.Contains(l.Id));

            if (!hasDownloadFilePermissions)
                throw new UnauthorizedAccessException(
                    $"You do not have download file permissions for all sensitivity labels on record {recordId}");
        }

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");
        
        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, record.ObjectStorageId.Value);
        
        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null or invalid");
        
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        return await fileBusiness.DownloadFile(record, configData);
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
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        // If record has sensitivity labels then ensure the user has delete file permissions
        if (record.Labels.Count > 0)
        {
            var authorizedSensitivityLabelIds = await PermissionHelper.GetAuthorizedSensitivityLabels(
                _context, currentUserId, organizationId, projectId, "delete file");

            var hasDeleteFilePermissions = record.Labels.All(l => authorizedSensitivityLabelIds.Contains(l.Id));

            if (!hasDeleteFilePermissions)
                throw new UnauthorizedAccessException(
                    $"You do not have delete file permissions for all sensitivity labels on record {recordId}");
        }

        if (record == null) throw new KeyNotFoundException("Record not found");
        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");
        
        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, record.ObjectStorageId.Value);
        
        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null or invalid");
        
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        await fileBusiness.DeleteFile(record, configData);
        
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
    /// <param name="sensitivityLabelIds">The IDs of the Sensitivity Labels that will be attached to the record</param>
    public async Task<FileUploadSessionResponseDto> StartUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadInitRequestDto request,
        List<long>? sensitivityLabelIds = null)
    {
        var sensitivityLabelsRequired =
            await PermissionHelper.SensitivityLabelRequired(_context, organizationId, projectId);

        if (sensitivityLabelsRequired && (sensitivityLabelIds == null || sensitivityLabelIds.Count == 0))
        {
            throw new InvalidOperationException("Sensitivity labels are required");
        }
        
        // if the user provides Sensitivity Labels ensure that the user is authorized to upload files
        if (sensitivityLabelIds?.Count > 0)
        {
            var authorizedLabelIds = await PermissionHelper.GetAuthorizedSensitivityLabels(
                _context, currentUserId, organizationId, projectId,"upload file");
            
            var hasAuthorization = sensitivityLabelIds.All(sl => authorizedLabelIds.Contains(sl));

            if (!hasAuthorization)
            {
                throw new UnauthorizedAccessException("You do not have upload file permissions for all provided sensitivity labels");
            }
        }
        
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
        
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        
        var uploadId = await fileBusiness.StartUpload(organizationId, projectId, realDataSourceId, configData);

        // Calculate total chunks needed
        var totalChunks = (int)Math.Ceiling((double)request.FileSize / _recommendedChunkSize);

        return new FileUploadSessionResponseDto
        {
            UploadId = uploadId.ToString(),
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
    /// <param name="sensitivityLabelIds">The IDs of the Sensitivity Labels that will be attached to the record</param>
    public async Task<string> UploadChunk(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        IFormFile chunk,
        string uploadId,
        int chunkNumber,
        List<long>? sensitivityLabelIds = null)
    {
        var sensitivityLabelsRequired =
            await PermissionHelper.SensitivityLabelRequired(_context, organizationId, projectId);

        if (sensitivityLabelsRequired && (sensitivityLabelIds == null || sensitivityLabelIds.Count == 0))
        {
            throw new InvalidOperationException("Sensitivity labels are required");
        }
        
        // if the user provides Sensitivity Labels ensure that the user is authorized to upload files
        if (sensitivityLabelIds?.Count > 0)
        {
            var authorizedLabelIds = await PermissionHelper.GetAuthorizedSensitivityLabels(
                _context, currentUserId, organizationId, projectId,"upload file");
            
            var hasAuthorization = sensitivityLabelIds.All(sl => authorizedLabelIds.Contains(sl));

            if (!hasAuthorization)
            {
                throw new UnauthorizedAccessException("You do not have upload file permissions for all provided sensitivity labels");
            }
        }
        
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
        
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        await fileBusiness.UploadChunk(organizationId, projectId, realDataSourceId, chunkNumber, uploadId, configData, chunk);
        return "success";
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
    /// <param name="sensitivityLabelIds">The IDs of the Sensitivity Labels that will be attached to the record</param>
    public async Task<RecordResponseDto> CompleteUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadCompleteRequestDto request,
        List<long>? sensitivityLabelIds = null)
    {
        var sensitivityLabelsRequired =
            await PermissionHelper.SensitivityLabelRequired(_context, organizationId, projectId);

        if (sensitivityLabelsRequired && (sensitivityLabelIds == null || sensitivityLabelIds.Count == 0))
        {
            throw new InvalidOperationException("Sensitivity labels are required");
        }
        
        // if the user provides Sensitivity Labels ensure that the user is authorized to upload files
        if (sensitivityLabelIds?.Count > 0)
        {
            var authorizedLabelIds = await PermissionHelper.GetAuthorizedSensitivityLabels(
                _context, currentUserId, organizationId, projectId,"upload file");
            
            var hasAuthorization = sensitivityLabelIds.All(sl => authorizedLabelIds.Contains(sl));

            if (!hasAuthorization)
            {
                throw new UnauthorizedAccessException("You do not have upload file permissions for all provided sensitivity labels");
            }
        }
        
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
        
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        
        var guid =  Guid.NewGuid();
        
        var uri = await fileBusiness.CompleteUpload(organizationId, projectId, realDataSourceId, configData, request, guid);
        
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
        
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        await fileBusiness.CancelUpload(organizationId, projectId, realDataSourceId, uploadId, configData);

        await Task.CompletedTask;
    }

    private async Task<ObjectStorage> GetObjectStorageWithConfig(long organizationId, long projectId, long recordObjectStorageId)
    {
        var objectStorage = _context.ObjectStorages.FirstOrDefault(os => os.OrganizationId == organizationId && 
                                                                         (os.ProjectId == projectId || os.ProjectId == null) && 
                                                                         os.Id == recordObjectStorageId);
        if  (objectStorage is null) throw new KeyNotFoundException("No object storage found for project");
        
        return objectStorage;
    }
}