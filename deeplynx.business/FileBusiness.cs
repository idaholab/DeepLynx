using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace deeplynx.business;

public class FileBusiness
{
    private readonly IClassBusiness _classBusiness;
    private readonly DeeplynxContext _context;
    private readonly IDataSourceBusiness _dataSourceBusiness;
    private readonly IFileBusinessFactory _factory;
    private readonly long _recommendedChunkSize;
    private readonly IRecordBusiness _recordBusiness;
    private readonly ISensitivityLabelService _sensitivityLabelService;

    // NOTE: Chunked upload methods currently only support filesystem storage.
    // When Azure/S3 chunked uploads are needed, refactor these methods to 
    // delegate to storage-specific implementations (IFileBusiness interface).
    public FileBusiness(
        DeeplynxContext context,
        IFileBusinessFactory factory,
        IDataSourceBusiness dataSourceBusiness,
        IClassBusiness classBusiness,
        IRecordBusiness recordBusiness)
    {
        _context = context;
        _factory = factory;
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
    /// <param name="metadataFile">(Optional) Metadata file to associate with the file upload</param>
    public async Task<RecordResponseDto> UploadFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        IFormFile file,
        List<long>? sensitivityLabelIds = null,
        IFormFile? metadataFile = null)
    {
        long realDataSourceId;
        if (file == null || file.Length == 0) throw new ArgumentException("File is required and cannot be empty.");
        file = new SanitizedFormFile(file);
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

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, objectStorageId);

        // Check config to confirm it is valid
        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null");

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var guid = Guid.NewGuid();

        var uri = await fileBusiness.UploadFile(organizationId, projectId, realDataSourceId, configData, file, guid);

        var fileClass = await _classBusiness.GetOrCreateClass(currentUserId, organizationId, projectId, "File");

        CreateRecordFileUploadRequestDto? metadata = null;

        if (metadataFile != null)
        {
            using (var reader = new StreamReader(metadataFile.OpenReadStream()))
            {
                var metadataJson = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(metadataJson))
                    throw new ArgumentException("Metadata file is empty or contains no content.");

                metadata = JsonSerializer.Deserialize<CreateRecordFileUploadRequestDto>(metadataJson)
                           ?? throw new InvalidOperationException("Failed to deserialize metadata file.");
            }

            ValidationHelper.ValidateModel(metadata);
        }

        var recordRequest = new CreateRecordRequestDto
        {
            Properties = metadata?.Properties ?? new JsonObject
            {
                ["fileType"] = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
            },
            Name = metadata?.Name ?? file.FileName,
            ObjectStorageId = objectStorage.Id,
            Description = metadata?.Description ?? file.FileName,
            OriginalId = metadata?.OriginalId ?? guid.ToString(),
            ClassId = metadata?.ClassId ?? fileClass.Id,
            ClassName = metadata?.ClassName ?? fileClass.Name,
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLower(),
            Uri = uri
        };

        // return the newly created metadata record for the file
        return await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, realDataSourceId,
            recordRequest, sensitivityLabelIds);
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

        if (file == null || file.Length == 0) throw new ArgumentException("File is required and cannot be empty.");
        file = new SanitizedFormFile(file);

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, record.ObjectStorageId.Value);

        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null)
            throw new InvalidOperationException("Config data for object storage is null or invalid");

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var guid = Guid.NewGuid();

        var uri = await fileBusiness.UpdateFile(record, configData, file, guid);

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
    /// <param name="currentUserId">ID of current user making the request</param>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="recordId">ID of record that contains the info of the file to download</param>
    public async Task<FileStreamResult> DownloadFile(long currentUserId, long organizationId, long projectId,
        long recordId)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, record.ObjectStorageId.Value);

        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null)
            throw new InvalidOperationException("Config data for object storage is null or invalid");

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        return await fileBusiness.DownloadFile(record, configData);
    }

    /// <summary>
    ///     Generates a Download URL
    /// </summary>
    /// <param name="currentUserId">ID of current user making the request</param>
    /// <param name="organizationId">ID of the organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="recordId">ID of record that contains the info of the file to download</param>
    public async Task<string> GenerateDownloadURL(long currentUserId, long organizationId, long projectId,
        long recordId)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, record.ObjectStorageId.Value);

        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null)
            throw new InvalidOperationException("Config data for object storage is null or invalid");

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        return await fileBusiness.GenerateDownloadUrl(record, configData);
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

        if (record == null) throw new KeyNotFoundException("Record not found");
        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, record.ObjectStorageId.Value);

        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null)
            throw new InvalidOperationException("Config data for object storage is null or invalid");

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        await fileBusiness.DeleteFile(record, configData);

        return await _recordBusiness.DeleteRecord(currentUserId, organizationId, projectId, recordId);
    }


    /// <summary>
    ///     Initializes a chunked upload session
    /// </summary>
    /// <param name="organizationId">ID of the Organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">ID of the object storage method to use</param>
    /// <param name="request">File upload initialization request</param>
    public async Task<FileUploadSessionResponseDto> StartUpload(
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
        
        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, objectStorageId);
        //Sanitize filename
        request.FileName = SanitizedFormFile.SanitizeFileName(request.FileName);
        
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
    /// <param name="organizationId">ID of the Organization to which the project belongs</param>
    /// <param name="projectId">ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">ID of the object storage method to use</param>
    /// <param name="chunk">File chunk from form</param>
    /// <param name="uploadId">The upload session ID from StartUpload</param>
    /// <param name="chunkNumber">The index for tracking the order to merge chunks together</param>
    public async Task<string> UploadChunk(
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

        chunk = new SanitizedFormFile(chunk);


        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, objectStorageId);

        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null");

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        await fileBusiness.UploadChunk(organizationId, projectId, realDataSourceId, chunkNumber, uploadId, configData,
            chunk);
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
    /// <param name="metadata">Metadata DTO for the file to be uploaded</param>
    public async Task<RecordResponseDto> CompleteUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadCompleteRequestDto request,
        List<long>? sensitivityLabelIds = null,
        CreateRecordFileUploadRequestDto? metadata = null)
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

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, objectStorageId);
        
        //Sanitize filename
        request.FileName = SanitizedFormFile.SanitizeFileName(request.FileName);

        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);

        if (configData == null) throw new InvalidOperationException("Config data for object storage is null");

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var guid = Guid.NewGuid();

        var uri = await fileBusiness.CompleteUpload(organizationId, projectId, realDataSourceId, configData, request,
            guid);

        // Create file record
        var fileClass = await _classBusiness.GetOrCreateClass(currentUserId, organizationId, projectId, "File");
        var recordRequest = new CreateRecordRequestDto
        {
            Properties = metadata?.Properties ?? new JsonObject
            {
                ["fileType"] = Path.GetExtension(request.FileName).TrimStart('.').ToLower(),
                ["uploadedViaChunking"] = true,
                ["originalUploadId"] = request.UploadId
            },
            Name = metadata?.Name ?? request.FileName,
            ObjectStorageId = objectStorage.Id,
            Description = metadata?.Description ?? $"File uploaded via chunked upload (session: {request.UploadId})",
            OriginalId = metadata?.OriginalId ?? guid.ToString(),
            Uri = uri,
            ClassId = metadata?.ClassId ?? fileClass.Id,
            ClassName = metadata?.ClassName ?? fileClass.Name,
            FileType = Path.GetExtension(request.FileName).TrimStart('.').ToLower()
        };

        return await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, realDataSourceId,
            recordRequest, sensitivityLabelIds);
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

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, objectStorageId);

        var configData = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (configData == null) throw new InvalidOperationException("Config data for object storage is null");

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        await fileBusiness.CancelUpload(organizationId, projectId, realDataSourceId, uploadId, configData);

        await Task.CompletedTask;
    }

    private async Task<ObjectStorage> GetObjectStorageWithConfig(long organizationId, long projectId,
        long? recordObjectStorageId)
    {
        ObjectStorage? objectStorage;
        if (recordObjectStorageId.HasValue)
        {
            objectStorage = _context.ObjectStorages.FirstOrDefault(os => os.OrganizationId == organizationId &&
                                                                         (os.ProjectId == projectId ||
                                                                          os.ProjectId == null) &&
                                                                         os.Id == recordObjectStorageId);
            if (objectStorage is null)
                throw new KeyNotFoundException(
                    $"No object storage found in your org/project with ID: {recordObjectStorageId}");
        }
        else
        {
            objectStorage = _context.ObjectStorages.Where(os =>
                    os.OrganizationId == organizationId && (os.ProjectId == projectId || os.ProjectId == null) &&
                    os.Default)
                .OrderByDescending(os => os.ProjectId.HasValue)
                .FirstOrDefault();

            if (objectStorage is null)
                throw new KeyNotFoundException(
                    "No default object storage found in your org/project");
        }

        return objectStorage;
    }
}
