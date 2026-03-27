using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly IInsightBusiness _insightBusiness;

    // NOTE: Chunked upload methods currently only support filesystem storage.
    // When Azure/S3 chunked uploads are needed, refactor these methods to 
    // delegate to storage-specific implementations (IFileBusiness interface).
    public FileBusiness(
        DeeplynxContext context,
        IFileBusinessFactory factory,
        IDataSourceBusiness dataSourceBusiness,
        IClassBusiness classBusiness,
        IRecordBusiness recordBusiness,
        IInsightBusiness insightBusiness)
    {
        _context = context;
        _factory = factory;
        _dataSourceBusiness = dataSourceBusiness;
        _classBusiness = classBusiness;
        _recordBusiness = recordBusiness;
        _insightBusiness = insightBusiness;

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
    public async Task<RecordResponseDto> UploadFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        IFormFile file,
        List<long>? sensitivityLabelIds = null,
        IFormFile? metadataFile = null,
        bool embed = false,
        long? vlmConfigId = null,
        long? embeddingModelConfigId = null)
    {
        if (file == null || file.Length == 0) throw new ArgumentException("File is required and cannot be empty.");
        file = new SanitizedFormFile(file);

        var fileType = Path.GetExtension(file.FileName).TrimStart('.').ToLower();

        if (embed && !_insightBusiness.IsSupportedFile(fileType))
            throw new ArgumentException($"Embedding is not supported for file type '{fileType}'.");

        var realDataSourceId = await ResolveDataSourceId(organizationId, projectId, dataSourceId);
        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, objectStorageId);
        var configData = DeserializeConfig(objectStorage.Config);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        var guid = Guid.NewGuid();

        var uri = await fileBusiness.UploadFile(organizationId, projectId, realDataSourceId, configData, file, guid);

        var fileClass = await _classBusiness.GetOrCreateClass(currentUserId, organizationId, projectId, "File");

        CreateRecordFileUploadRequestDto? metadata = null;
        if (metadataFile != null)
        {
            using var reader = new StreamReader(metadataFile.OpenReadStream());
            var metadataJson = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(metadataJson))
                throw new ArgumentException("Metadata file is empty or contains no content.");

            metadata = JsonSerializer.Deserialize<CreateRecordFileUploadRequestDto>(metadataJson)
                       ?? throw new InvalidOperationException("Failed to deserialize metadata file.");

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
            FileType = fileType,
            Uri = uri,
            Embed = embed,
        };

        var createdRecord = await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId,
            realDataSourceId, recordRequest, sensitivityLabelIds);

        if (embed)
        {
            var vlmConfig = await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, vlmConfigId, "vlm");
            var embeddingModelConfig = await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");
            
            _insightBusiness.TriggerEmbedding(projectId, createdRecord.Id,
                            createdRecord.Uri!, vlmConfig, embeddingModelConfig);
        }
        
        return createdRecord;
    }

    /// <summary>
    ///     Replaces a file but uses the same guid for the file name
    /// </summary>
    public async Task<RecordResponseDto> UpdateFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        IFormFile file,
        long? vlmConfigId = null,
        long? embeddingModelConfigId = null)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (file == null || file.Length == 0) throw new ArgumentException("File is required and cannot be empty.");
        file = new SanitizedFormFile(file);

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, record.ObjectStorageId.Value);
        var configData = DeserializeConfig(objectStorage.Config);
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

        var updatedRecord = await _recordBusiness.UpdateRecord(currentUserId, organizationId, projectId, recordId,
            updateRecordRequest);

        if (record.Embedded)
        {
            var vlmConfig =
                await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, vlmConfigId, "vlm");
            var embeddingModelConfig = 
                await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");
            
            _insightBusiness.TriggerEmbedding(projectId, updatedRecord.Id, updatedRecord.Uri!, vlmConfig, embeddingModelConfig, overwrite: true);
        }
        
        return updatedRecord;
    }

    /// <summary>
    ///     Downloads a file
    /// </summary>
    public async Task<FileStreamResult> DownloadFile(long currentUserId, long organizationId, long projectId,
        long recordId)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, record.ObjectStorageId.Value);
        var configData = DeserializeConfig(objectStorage.Config);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        return await fileBusiness.DownloadFile(record, configData);
    }

    /// <summary>
    ///     Generates a download URL
    /// </summary>
    public async Task<string> GenerateDownloadURL(long currentUserId, long organizationId, long projectId,
        long recordId)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, record.ObjectStorageId.Value);
        var configData = DeserializeConfig(objectStorage.Config);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        return await fileBusiness.GenerateDownloadUrl(record, configData);
    }

    /// <summary>
    ///     Deletes a file
    /// </summary>
    public async Task<bool> DeleteFile(long currentUserId, long organizationId, long projectId, long recordId)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (record == null) throw new KeyNotFoundException("Record not found");
        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, record.ObjectStorageId.Value);
        var configData = DeserializeConfig(objectStorage.Config);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        await fileBusiness.DeleteFile(record, configData);

        return await _recordBusiness.DeleteRecord(currentUserId, organizationId, projectId, recordId);

        // Embeddings made by Insight that reference this record will be auto deleted
    }

    /// <summary>
    ///     Initializes a chunked upload session
    /// </summary>
    public async Task<FileUploadSessionResponseDto> StartUpload(
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadInitRequestDto request)
    {
        var realDataSourceId = await ResolveDataSourceId(organizationId, projectId, dataSourceId);
        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, objectStorageId);

        request.FileName = SanitizedFormFile.SanitizeFileName(request.FileName);

        var configData = DeserializeConfig(objectStorage.Config);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var uploadId = await fileBusiness.StartUpload(organizationId, projectId, realDataSourceId, configData);
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
    public async Task<string> UploadChunk(
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        IFormFile chunk,
        string uploadId,
        int chunkNumber)
    {
        var realDataSourceId = await ResolveDataSourceId(organizationId, projectId, dataSourceId);

        if (chunk == null) throw new ArgumentException("chunk cannot be null");
        chunk = new SanitizedFormFile(chunk);

        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, objectStorageId);
        var configData = DeserializeConfig(objectStorage.Config);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        await fileBusiness.UploadChunk(organizationId, projectId, realDataSourceId, chunkNumber, uploadId, configData,
            chunk);

        return "success";
    }

    /// <summary>
    ///     Completes the upload by merging chunks and creating the file record
    /// </summary>
    public async Task<RecordResponseDto> CompleteUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadCompleteRequestDto request,
        List<long>? sensitivityLabelIds = null,
        CreateRecordFileUploadRequestDto? metadata = null,
        bool embed = false,
        long? vlmConfigId = null,
        long? embeddingModelConfigId = null)
    {
        var realDataSourceId = await ResolveDataSourceId(organizationId, projectId, dataSourceId);
        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, objectStorageId);

        request.FileName = SanitizedFormFile.SanitizeFileName(request.FileName);

        var configData = DeserializeConfig(objectStorage.Config);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        var guid = Guid.NewGuid();

        var uri = await fileBusiness.CompleteUpload(organizationId, projectId, realDataSourceId, configData, request,
            guid);

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
            FileType = Path.GetExtension(request.FileName).TrimStart('.').ToLower(),
            Embed = embed,
        };

        var createdRecord = await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId,
            realDataSourceId, recordRequest, sensitivityLabelIds);
        
        if (embed)
        {
            var vlmConfig = await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, vlmConfigId, "vlm");
            var embeddingModelConfig = await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");
            
            _insightBusiness.TriggerEmbedding(projectId, createdRecord.Id,
                createdRecord.Uri!, vlmConfig, embeddingModelConfig);
        }

        return createdRecord;
    }

    /// <summary>
    ///     Cancels an in-progress upload and cleans up temporary files
    /// </summary>
    public async Task CancelUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        string uploadId)
    {
        var realDataSourceId = await ResolveDataSourceId(organizationId, projectId, dataSourceId);
        var objectStorage = await GetObjectStorageWithConfig(organizationId, projectId, objectStorageId);
        var configData = DeserializeConfig(objectStorage.Config);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        await fileBusiness.CancelUpload(organizationId, projectId, realDataSourceId, uploadId, configData);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<long> ResolveDataSourceId(long organizationId, long projectId, long? dataSourceId)
    {
        if (dataSourceId.HasValue)
        {
            await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId.Value, projectId);
            return dataSourceId.Value;
        }

        var defaultDataSource = await _dataSourceBusiness.GetDefaultDataSource(organizationId, projectId)
                                ?? throw new KeyNotFoundException("Default data source not found");
        return defaultDataSource.Id;
    }

    private async Task<ObjectStorage> GetObjectStorageWithConfig(long organizationId, long projectId,
        long? recordObjectStorageId)
    {
        ObjectStorage? objectStorage;
        if (recordObjectStorageId.HasValue)
        {
            objectStorage = _context.ObjectStorages.FirstOrDefault(os =>
                os.OrganizationId == organizationId &&
                (os.ProjectId == projectId || os.ProjectId == null) &&
                os.Id == recordObjectStorageId);

            if (objectStorage is null)
                throw new KeyNotFoundException(
                    $"No object storage found in your org/project with ID: {recordObjectStorageId}");
        }
        else
        {
            objectStorage = _context.ObjectStorages
                .Where(os => os.OrganizationId == organizationId &&
                             (os.ProjectId == projectId || os.ProjectId == null) &&
                             os.Default)
                .OrderByDescending(os => os.ProjectId.HasValue)
                .FirstOrDefault();

            if (objectStorage is null)
                throw new KeyNotFoundException("No default object storage found in your org/project");
        }

        return objectStorage;
    }

    private static ObjectStorageConfigDto DeserializeConfig(string config)
    {
        return JsonConvert.DeserializeObject<ObjectStorageConfigDto>(config)
               ?? throw new InvalidOperationException("Config data for object storage is null or invalid");
    }
}