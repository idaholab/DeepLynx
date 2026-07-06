using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using deeplynx.models.ResponseDTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using deeplynx.helpers.Cache;
using System.Runtime.CompilerServices;

namespace deeplynx.business;

public class FileBusiness
{
    private readonly IClassBusiness _classBusiness;
    private readonly DeeplynxContext _context;
    private readonly IDataSourceBusiness _dataSourceBusiness;
    private readonly IFileBusinessFactory _factory;
    private readonly long _recommendedChunkSize;
    private readonly IRecordBusiness _recordBusiness;
    private readonly IOlapBusiness _olapBusiness;
    private readonly IInsightBusiness _insightBusiness;
    private readonly IObjectStorageBusiness _objectStorageBusiness;
    private readonly ILogger<FileBusiness> _logger;


    // NOTE: Chunked upload methods currently only support filesystem storage.
    // When Azure/S3 chunked uploads are needed, refactor these methods to 
    // delegate to storage-specific implementations (IFileBusiness interface).
    public FileBusiness(
        DeeplynxContext context,
        IFileBusinessFactory factory,
        IDataSourceBusiness dataSourceBusiness,
        IClassBusiness classBusiness,
        IRecordBusiness recordBusiness,
        IInsightBusiness insightBusiness,
        IOlapBusiness olapBusiness,
        IObjectStorageBusiness objectStorageBusiness,
        ILogger<FileBusiness> logger)
    {
        _context = context;
        _factory = factory;
        _dataSourceBusiness = dataSourceBusiness;
        _classBusiness = classBusiness;
        _recordBusiness = recordBusiness;
        _insightBusiness = insightBusiness;
        _olapBusiness = olapBusiness;
        _objectStorageBusiness = objectStorageBusiness;
        _logger = logger;

        var chunkSizeStr = Environment.GetEnvironmentVariable("RECOMMENDED_CHUNK_SIZE")
                           ?? throw new InvalidOperationException(
                               "RECOMMENDED_CHUNK_SIZE environment variable is not set");

        if (!long.TryParse(chunkSizeStr, out var chunkSize) || chunkSize <= 0)
            throw new InvalidOperationException("RECOMMENDED_CHUNK_SIZE must be a positive number");

        _recommendedChunkSize = chunkSize;
    }

    /// <summary>
    ///     Upload a File
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage method</param>
    /// <param name="file">The file to upload</param>
    /// <param name="sensitivityLabelIds">The IDs of the Sensitivity Labels that will be attached to the record</param>
    /// <param name="metadataFile">Optional metadata that will be appended to the created record</param>
    /// <param name="embed">Boolean value that determines if the file will be embedded by Insight</param>
    /// <param name="vlmConfigId">Optional ID of the VLM model that will be used by Insight if embed is set to true</param>
    /// <param name="embeddingModelConfigId">Optional ID of the Embedding model that will be used by Insight if embed is set to true</param>
    /// <returns>Record response DTO containing file information</returns>
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
        long? embeddingModelConfigId = null,
        string? userJwt = null)
    {
        if (file == null || file.Length == 0) throw new ArgumentException("File is required and cannot be empty.");
        file = new SanitizedFormFile(file);

        var fileType = Path.GetExtension(file.FileName).TrimStart('.').ToLower();

        if (embed && !_insightBusiness.IsSupportedFile(fileType))
            throw new ArgumentException($"Embedding is not supported for file type '{fileType}'.");

        var realDataSourceId = await ResolveDataSourceId(organizationId, projectId, dataSourceId);
        var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, objectStorageId);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        var guid = Guid.NewGuid();

        var uri = await fileBusiness.UploadFile(organizationId, projectId, realDataSourceId, objectStorage.Config, file, guid);

        var recordClass = await _classBusiness.GetOrCreateClass(currentUserId, organizationId, projectId, "File");

        var fileSize = file.Length;

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

        // Get file extension
        var fileExtension = Path.GetExtension(file.FileName).TrimStart('.').ToLower();

        // Initialize properties
        var properties = metadata?.Properties ?? new JsonObject();

        recordClass = await ExtractTabularRecordMetadata(
            currentUserId,
            organizationId,
            projectId,
            fileExtension,
            objectStorage.Type,
            objectStorage.Config,
            uri,
            properties,
            recordClass,
            () => file.OpenReadStream());

        var resolvedClass = await GetResolvedClass(organizationId, projectId, currentUserId, metadata, recordClass);

        var recordRequest = new CreateRecordRequestDto
        {
            Properties = properties,
            Name = metadata?.Name ?? file.FileName,
            ObjectStorageId = objectStorage.Id,
            Description = metadata?.Description ?? file.FileName,
            OriginalId = metadata?.OriginalId ?? guid.ToString(),
            ClassId = resolvedClass.Id,
            ClassName = resolvedClass.Name,
            FileType = fileType,
            Uri = uri,
            FileSize = fileSize
        };

        var createdRecord = await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId,
            realDataSourceId, recordRequest, sensitivityLabelIds, embed);

        if (embed)
        {
            var vlmConfig = await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, vlmConfigId, "vlm");
            var embeddingModelConfig = await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");

            _insightBusiness.TriggerEmbedding(projectId, createdRecord.Id,
                            createdRecord.Uri!, vlmConfig, embeddingModelConfig, userJwt);
        }

        await InvalidateProjectStorageSizeCache(projectId);

        return createdRecord;
    }

    /// <summary>
    ///     Update a File
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="recordId">The ID of the record that contains file information</param>
    /// <param name="file">The file to replace the old one</param>
    /// <param name="vlmConfigId">Optional ID of the VLM model that will be used by Insight if embed is set to true</param>
    /// <param name="embeddingModelConfigId">Optional ID of the Embedding model that will be used by Insight if embed is set to true</param>
    /// <returns>Record response DTO containing updated file information</returns>
    public async Task<RecordResponseDto> UpdateFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        IFormFile file,
        long? vlmConfigId = null,
        long? embeddingModelConfigId = null,
        string? userJwt = null)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (file == null || file.Length == 0) throw new ArgumentException("File is required and cannot be empty.");
        file = new SanitizedFormFile(file);

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(record.ObjectStorageId.Value);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        var guid = Guid.NewGuid();

        var uri = await fileBusiness.UpdateFile(record, objectStorage.Config, file, guid);

        var fileSize = file.Length;

        var updateRecordRequest = new UpdateRecordRequestDto
        {
            Properties = new JsonObject
            {
                ["fileType"] = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
            },
            Name = file.FileName,
            Uri = uri,
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLower(),
            FileSize = fileSize
        };

        var updatedRecord = await _recordBusiness.UpdateRecord(currentUserId, organizationId, projectId, recordId,
            updateRecordRequest);

        if (record.Embedded)
        {
            var vlmConfig =
                await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, vlmConfigId, "vlm");
            var embeddingModelConfig =
                await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");

            _insightBusiness.TriggerEmbedding(projectId, updatedRecord.Id, updatedRecord.Uri!, vlmConfig, embeddingModelConfig, userJwt, overwrite: true);
        }

        await InvalidateProjectStorageSizeCache(projectId);

        return updatedRecord;
    }

    /// <summary>
    ///     Download a File
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="recordId">The ID of the record that contains file information</param>
    /// <param name="isSysAdmin">Bool of whether or not the user is a system admin</param>
    /// <param name="isOrgAdmin">Bool of whether or not the user is an organization admin</param>
    /// <param name="isProjectAdmin">Bool of whether or not the user is a project admin</param>
    /// <returns>The file stream for download</returns>
    public async Task<FileStreamResult> DownloadFile(long currentUserId, long organizationId, long projectId,
        long recordId, bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true, isSysAdmin, isOrgAdmin, isProjectAdmin);

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(record.ObjectStorageId.Value);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        return await fileBusiness.DownloadFile(record, objectStorage.Config);
    }

    /// <summary>
    ///     Generate Download URL
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="recordId">The ID of the record that contains file information</param>
    /// <returns>The file stream for download</returns>
    public async Task<string> GenerateDownloadURL(long currentUserId, long organizationId, long projectId,
        long recordId)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(record.ObjectStorageId.Value);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        return await fileBusiness.GenerateDownloadUrl(record, objectStorage.Config);
    }

    /// <summary>
    ///     Delete a File
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="recordId">The ID of the record that contains file information</param>
    /// <returns>A message stating the file was successfully deleted.</returns>
    public async Task<bool> DeleteFile(long currentUserId, long organizationId, long projectId, long recordId)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (record == null) throw new KeyNotFoundException("Record not found");
        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(record.ObjectStorageId.Value);

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        await fileBusiness.DeleteFile(record, objectStorage.Config);

        //var deleted = await _recordBusiness.DeleteRecord(currentUserId, organizationId, projectId, recordId);

        await InvalidateProjectStorageSizeCache(projectId);

        return true;

        // Embeddings made by Insight that reference this record will be auto deleted
    }

    /// <summary>
    ///     Delete a File and not the Record
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="recordId">The ID of the record that contains file information</param>
    /// <returns>A message stating the file was successfully deleted.</returns>
    public async Task<bool> DeleteFileOnly(long currentUserId, long organizationId, long projectId, long recordId)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (record == null) throw new KeyNotFoundException("Record not found");
        if (record.ObjectStorageId == null) throw new KeyNotFoundException("Record needs an object storage id");

        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(record.ObjectStorageId.Value);

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var deleted = await fileBusiness.DeleteFile(record, objectStorage.Config);

        await InvalidateProjectStorageSizeCache(projectId);

        return deleted;

        // Embeddings made by Insight that reference this record will be auto deleted
    }

    /// <summary>
    ///     Start Chunked File Upload (For large files over 500MB)
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage method</param>
    /// <param name="request">File upload initialization request DTO</param>
    /// <returns>{UploadId, ChunkSize}</returns>
    public async Task<FileUploadSessionResponseDto> StartUpload(
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadInitRequestDto request)
    {
        var realDataSourceId = await ResolveDataSourceId(organizationId, projectId, dataSourceId);
        var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, objectStorageId);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId);

        request.FileName = SanitizedFormFile.SanitizeFileName(request.FileName);

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var uploadId = await fileBusiness.StartUpload(organizationId, projectId, realDataSourceId, objectStorage.Config);
        var totalChunks = (int)Math.Ceiling((double)request.FileSize / _recommendedChunkSize);

        return new FileUploadSessionResponseDto
        {
            UploadId = uploadId.ToString(),
            ChunkSize = _recommendedChunkSize,
            TotalChunks = totalChunks
        };
    }

    /// <summary>
    ///     Upload File Chunk
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage method</param>
    /// <param name="chunk">File chunk from form</param>
    /// <param name="uploadId">ID of upload session</param>
    /// <param name="chunkNumber">Chunk number (0-indexed)</param>
    /// <returns>{ChunkUploadStatus}</returns>
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


        var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, objectStorageId);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        await fileBusiness.UploadChunk(organizationId, projectId, realDataSourceId, chunkNumber, uploadId,
            objectStorage.Config, chunk);

        return "success";
    }

    /// <summary>
    ///     Complete Chunked File Upload
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user</param>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage method</param>
    /// <param name="request">File upload completion request DTO with optional metadata DTO</param>
    /// <param name="sensitivityLabelIds">Optional List of ID's for the Sensitivity Labels to be associated with this file/record</param>
    /// <param name="metadata">Additional metadata that will be appended to the record</param>
    /// <param name="embed">Optional boolean that determines if the file will be embedded by Insight</param>
    /// <param name="vlmConfigId">Optional ID of the VLM model that will be used by Insight if embed is set to true</param>
    /// <param name="embeddingModelConfigId">Optional ID of the Embedding model that will be used by Insight if embed is set to true</param>
    /// <returns>Record response DTO containing file information</returns>
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
        var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, objectStorageId);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId);

        request.FileName = SanitizedFormFile.SanitizeFileName(request.FileName);

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        var guid = Guid.NewGuid();

        var uri = await fileBusiness.CompleteUpload(organizationId, projectId, realDataSourceId,
            objectStorage.Config, request, guid);

        var fileExtension = Path.GetExtension(request.FileName).TrimStart('.').ToLower();
        var fileClass = await _classBusiness.GetOrCreateClass(currentUserId, organizationId, projectId, "File");
        var fileSize = await fileBusiness.GetFileSize(uri, objectStorage.Config);
        var properties = metadata?.Properties ?? new JsonObject
        {
            ["fileType"] = fileExtension,
            ["uploadedViaChunking"] = true,
            ["originalUploadId"] = request.UploadId
        };

        fileClass = await ExtractTabularRecordMetadata(
            currentUserId,
            organizationId,
            projectId,
            fileExtension,
            objectStorage.Type,
            objectStorage.Config,
            uri,
            properties,
            fileClass,
            objectStorage.Type == "filesystem" ? () => File.OpenRead(uri) : null);

        var resolvedClass = await GetResolvedClass(organizationId, projectId, currentUserId, metadata, fileClass);

        var recordRequest = new CreateRecordRequestDto
        {
            Properties = properties,
            Name = metadata?.Name ?? request.FileName,
            ObjectStorageId = objectStorage.Id,
            Description = metadata?.Description ?? $"File uploaded via chunked upload (session: {request.UploadId})",
            OriginalId = metadata?.OriginalId ?? guid.ToString(),
            Uri = uri,
            ClassId = resolvedClass.Id,
            ClassName = resolvedClass.Name,
            FileType = fileExtension,
            FileSize = fileSize
        };

        var createdRecord = await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId,
            realDataSourceId, recordRequest, sensitivityLabelIds, embedded: embed);

        if (embed)
        {
            var vlmConfig = await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, vlmConfigId, "vlm");
            var embeddingModelConfig = await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");

            _insightBusiness.TriggerEmbedding(projectId, createdRecord.Id,
                createdRecord.Uri!, vlmConfig, embeddingModelConfig);
        }

        await InvalidateProjectStorageSizeCache(projectId);

        return createdRecord;
    }

    /// <summary>
    ///     Cancel Chunked File Upload
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user</param>

    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage method</param>
    /// <param name="uploadId">ID of upload session to cancel</param>
    /// <returns>A message stating the upload was successfully cancelled</returns>
    public async Task CancelUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        string uploadId)
    {
        var realDataSourceId = await ResolveDataSourceId(organizationId, projectId, dataSourceId);
        var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, objectStorageId);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        await fileBusiness.CancelUpload(organizationId, projectId, realDataSourceId, uploadId, objectStorage.Config);
    }

    /// <summary>
    ///     Backfill records to include file size for any files pre-dating the file size capture feature.
    /// </summary>
    /// <param name="organizationId">ID of the organization in which to backfill file sizes</param>
    /// <param name="projectId">ID of the project in which to backfill file sizes</param>
    /// <exception cref="InvalidOperationException">Returned if org ID or project ID not supplied</exception>
    public async Task<BackfillFileSizesResponseDto> BackfillFileSizes(
        long? organizationId,
        long? projectId,
        long? afterRecordId = null,
        int batchSize = 500,
        int maxBatches = 5)
    {
        if (organizationId == null && projectId == null)
            throw new InvalidOperationException("At least one of organization or project must be specified.");

        if (batchSize <= 0)
            throw new ArgumentException("batchSize must be greater than zero.");

        if (maxBatches <= 0)
            throw new ArgumentException("maxBatches must be greater than zero.");

        var response = new BackfillFileSizesResponseDto();

        for (var batchNumber = 0; batchNumber < maxBatches; batchNumber++)
        {
            var toBackfill = _context.Records
                .Where(r => r.Uri != null && r.FileSize == null && !r.IsArchived);

            if (organizationId.HasValue)
                toBackfill = toBackfill.Where(r => r.OrganizationId == organizationId.Value);

            if (projectId.HasValue)
                toBackfill = toBackfill.Where(r => r.ProjectId == projectId.Value);

            if (afterRecordId.HasValue)
                toBackfill = toBackfill.Where(r => r.Id > afterRecordId.Value);

            var records = await toBackfill
                .OrderBy(r => r.Id)
                .Take(batchSize)
                .ToListAsync();

            if (records.Count == 0)
                break;

            response.Processed += records.Count;
            response.LastRecordId = records.Max(r => r.Id);

            var recordsByStorage = records
                .Where(r => r.ObjectStorageId != null)
                .GroupBy(r => r.ObjectStorageId!.Value)
                .ToList();

            var legacyFilesystemRecords = records
                .Where(r => r.ObjectStorageId == null)
                .ToList();

            foreach (var storageGroup in recordsByStorage)
            {
                var objectStorageId = storageGroup.Key;
                ObjectStorageDecryptedDto objectStorage;

                try
                {
                    objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(objectStorageId);
                }
                catch (Exception ex)
                {
                    response.Failed += storageGroup.Count();

                    _logger.LogWarning(
                        ex,
                        "Object storage {ObjectStorageId} not found or archived while backfilling file sizes",
                        storageGroup.Key);

                    continue;
                }

                var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

                // for azure, try batch operation first
                if (objectStorage.Type == "azure_object" && fileBusiness is FileAzureBusiness azureBusiness)
                {
                    try
                    {
                        var uris = storageGroup.Select(r => r.Uri!).ToList();
                        var sizes = await azureBusiness.GetFileSizesBatch(uris, objectStorage.Config);

                        foreach (var record in storageGroup)
                        {
                            if (record.Uri != null && sizes.TryGetValue(record.Uri, out var size))
                            {
                                record.FileSize = size;
                                response.Updated++;
                            }
                            else
                            {
                                response.Failed++;

                                _logger.LogWarning(
                                    "Failed to backfill size for record {RecordId}, project {ProjectId}, object storage {ObjectStorageId}, uri {Uri}",
                                    record.Id,
                                    record.ProjectId,
                                    record.ObjectStorageId,
                                    record.Uri);
                            }
                        }

                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Azure batch file size lookup failed for object storage {ObjectStorageId}; falling back to individual file size lookups",
                            storageGroup.Key);
                    }
                }
                // fall back to individual calls for non-Azure path or Azure batch fallback path
                foreach (var record in storageGroup)
                {
                    try
                    {
                        var fileSize = await fileBusiness.GetFileSize(record.Uri!, objectStorage.Config);
                        record.FileSize = fileSize;
                        response.Updated++;
                    }
                    catch (Exception ex)
                    {
                        response.Failed++;

                        _logger.LogWarning(
                            ex,
                            "Failed to backfill size for record {RecordId}, project {ProjectId}, object storage {ObjectStorageId}, uri {Uri} while individual calls",
                            record.Id,
                            record.ProjectId,
                            record.ObjectStorageId,
                            record.Uri);
                    }
                }
            }

            // Check filesystem records that exists, but object_storage_id is null
            foreach (var record in legacyFilesystemRecords)
            {
                try
                {
                    if (record.Uri != null && File.Exists(record.Uri))
                    {
                        record.FileSize = new FileInfo(record.Uri).Length;
                        response.Updated++;
                    }
                    else
                    {
                        response.Failed++;

                        _logger.LogWarning(
                            "Failed to backfill legacy filesystem size for record {RecordId}, project {ProjectId}, uri {Uri}; file does not exist",
                            record.Id,
                            record.ProjectId,
                            record.Uri);
                    }
                }
                catch (Exception ex)
                {
                    response.Failed++;

                    _logger.LogWarning(
                        ex,
                        "Failed to backfill legacy filesystem size for record {RecordId}, project {ProjectId}, uri {Uri}",
                        record.Id,
                        record.ProjectId,
                        record.Uri);
                }
            }

            var affectedProjectIds = records
                .Select(r => r.ProjectId)
                .Distinct()
                .ToList();

            // save progress after each batch so the backfill can resume if a later batch fails.
            await _context.SaveChangesAsync();

            foreach (var affectedProjectId in affectedProjectIds)
            {
                await InvalidateProjectStorageSizeCache(affectedProjectId);
            }

            afterRecordId = response.LastRecordId;

            if (records.Count < batchSize)
                break;
        }

        return response;
    }

    public async Task<TusFileUploadSessionResponseDto> CreateUploadTus(
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadInitRequestDto request)
    {
        var realDataSourceId = await ResolveDataSourceId(organizationId, projectId, dataSourceId);
        var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, objectStorageId);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId);

        request.FileName = SanitizedFormFile.SanitizeFileName(request.FileName);

        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var uploadId = await fileBusiness.CreateUploadTus(organizationId, projectId, realDataSourceId, objectStorage.Config, request.FileSize, request.FileName);

        return new TusFileUploadSessionResponseDto
        {
            UploadId = uploadId.ToString()
        };
    }

    public async Task<(long, long)> GetUploadOffsetTus(
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        string uploadId)
    {
        var realDataSourceId = await ResolveDataSourceId(organizationId, projectId, dataSourceId);

        var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, objectStorageId);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);
        var uploadOffset = await fileBusiness.GetUploadOffset(organizationId, projectId, realDataSourceId, uploadId,
        objectStorage.Config);
        var uploadLength = await fileBusiness.GetUploadLength(organizationId, projectId, realDataSourceId, uploadId,
        objectStorage.Config);

        return (uploadOffset, uploadLength);
    }

    public async Task<long> UploadPartTus(
    long organizationId,
    long projectId,
    long? dataSourceId,
    long? objectStorageId,
    string uploadId,
    long uploadOffset,
    long currentUserId,
    System.IO.Stream uploadBody,
    List<long>? sensitivityLabelIds = null,
    CreateRecordFileUploadRequestDto? metadata = null,
    bool embed = false,
    long? vlmConfigId = null,
    long? embeddingModelConfigId = null)
    {
        var realDataSourceId = await ResolveDataSourceId(organizationId, projectId, dataSourceId);

        if (uploadBody == null) throw new ArgumentException("uploadBody cannot be null");

        var realObjectStorageId = await ResolveObjectStorageId(organizationId, projectId, objectStorageId);
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(realObjectStorageId);
        var fileBusiness = _factory.CreateFileBusiness(objectStorage.Type);

        var newOffset = await fileBusiness.UploadPartTus(organizationId, projectId, realDataSourceId, uploadId, uploadOffset,
            objectStorage.Config, uploadBody);
        var uploadLength = await fileBusiness.GetUploadLength(organizationId, projectId, realDataSourceId, uploadId, objectStorage.Config);

        if (newOffset > uploadLength)
            throw new InvalidOperationException($"Upload offset {newOffset} exceeds declared Upload-Length {uploadLength}");
        if (newOffset == uploadLength)
        {
            var guid = Guid.NewGuid();
            var fileName = await fileBusiness.GetFileNameTus(organizationId, projectId, realDataSourceId, uploadId, objectStorage.Config);
            var uri = await fileBusiness.CompleteUploadTus(organizationId, projectId, realDataSourceId,
                objectStorage.Config, uploadId, guid, fileName);

            var fileExtension = Path.GetExtension(fileName).TrimStart('.').ToLower();
            var fileClass = await _classBusiness.GetOrCreateClass(currentUserId, organizationId, projectId, "File");
            var fileSize = await fileBusiness.GetFileSize(uri, objectStorage.Config);
            var properties = new JsonObject
            {
                ["fileType"] = fileExtension,
                ["uploadedViaChunking"] = true,
                ["originalUploadId"] = uploadId
            };

            fileClass = await ExtractTabularRecordMetadata(
                currentUserId,
                organizationId,
                projectId,
                fileExtension,
                objectStorage.Type,
                objectStorage.Config,
                uri,
                properties,
                fileClass,
                objectStorage.Type == "filesystem" ? () => File.OpenRead(uri) : null);
            var recordName = metadata?.Name ?? Path.GetFileName(fileName);
            if (recordName.Length > 100) recordName = recordName[..100];
            var resolvedClass = await GetResolvedClass(organizationId, projectId, currentUserId, metadata, fileClass);
            var recordRequest = new CreateRecordRequestDto
            {
                Properties = properties,
                Name = recordName,
                ObjectStorageId = objectStorage.Id,
                Description = metadata?.Description ?? $"File uploaded via chunked upload (session: {uploadId})",
                OriginalId = metadata?.OriginalId ?? guid.ToString(),
                Uri = uri,
                ClassId = resolvedClass.Id,
                ClassName = resolvedClass.Name,
                FileType = fileExtension,
                FileSize = fileSize
            };

            var createdRecord = await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId,
                realDataSourceId, recordRequest, sensitivityLabelIds, embedded: embed);

            await InvalidateProjectStorageSizeCache(projectId);

            if (embed)
            {
                var vlmConfig = await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, vlmConfigId, "vlm");
                var embeddingModelConfig = await _insightBusiness.ResolveModelConfig(currentUserId, organizationId, projectId, embeddingModelConfigId, "embedding");

                _insightBusiness.TriggerEmbedding(projectId, createdRecord.Id,
                    createdRecord.Uri!, vlmConfig, embeddingModelConfig);
            }
        }

        return newOffset;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static async Task InvalidateProjectStorageSizeCache(long projectId)
    {
        await CacheService.Instance.DeleteAsync(
            CacheKeys.ProjectStorageSize(projectId));
    }

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

    private async Task<long> ResolveObjectStorageId(long organizationId, long projectId, long? objectStorageId)
    {
        if (objectStorageId.HasValue)
        {
            // object storage could be org-level so just return object storage, don't check for project existence
            return objectStorageId.Value;
        }

        var defaultObjectStorage = await _objectStorageBusiness.GetDefaultObjectStorage(organizationId, projectId)
            ?? throw new KeyNotFoundException("Default object storage not found");
        return defaultObjectStorage.Id;
    }

    private async Task<ClassResponseDto> ExtractTabularRecordMetadata(
        long currentUserId,
        long organizationId,
        long projectId,
        string fileExtension,
        string objectStorageType,
        ObjectStorageConfigDto objectStorageConfig,
        string uri,
        JsonObject properties,
        ClassResponseDto defaultClass,
        Func<Stream>? openReadStream)
    {
        if (!IsTabularFileExtension(fileExtension))
            return defaultClass;

        if (openReadStream != null && !await HasNonWhitespaceContent(openReadStream))
            return defaultClass;

        var columns = await _olapBusiness.ExtractTabularColumns(objectStorageType, objectStorageConfig, uri);
        if (columns == null || columns.Count == 0)
            return defaultClass;

        properties["columns"] = columns;
        return await _classBusiness.GetOrCreateClass(currentUserId, organizationId, projectId, "Timeseries");
    }

    private static bool IsTabularFileExtension(string fileExtension)
    {
        return fileExtension == "csv" || fileExtension == "parquet";
    }

    private static async Task<bool> HasNonWhitespaceContent(Func<Stream> openReadStream)
    {
        await using var stream = openReadStream();
        using var reader = new StreamReader(stream);

        var buffer = new char[256];
        int charsRead;

        while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < charsRead; i++)
            {
                if (!char.IsWhiteSpace(buffer[i]))
                    return true;
            }
        }

        return false;
    }

    private async Task<ClassResponseDto> GetResolvedClass(long organizationId, long projectId, long currentUserId, CreateRecordFileUploadRequestDto? metadata, ClassResponseDto recordClass)
    {
        var providedClassId = metadata?.ClassId;
        var providedClassName = metadata?.ClassName;
        ClassResponseDto? resolvedClass = null;

        // If Class Id was provided
        if (providedClassId.HasValue)
        {
            resolvedClass = await _classBusiness.GetClass(organizationId, projectId, providedClassId.Value, true);
            if (resolvedClass is null)
            {
                throw new ArgumentException($"Class ID {providedClassId} does not exist in this project.");
            }
            if (!string.IsNullOrWhiteSpace(providedClassName) && resolvedClass.Name != providedClassName)
            {
                // Class Name was provided and doesn't match the Class Name from the Id
                throw new ArgumentException($"Class Name {providedClassName} does not match Class Id {providedClassId}. Expected {resolvedClass.Name}");
            }
        }
        // No Class Id provided, Class Name was provided
        else if (!string.IsNullOrWhiteSpace(providedClassName))
        {
            resolvedClass = await _classBusiness.GetOrCreateClass(currentUserId, organizationId, projectId, providedClassName);
        }
        // No Class Id or Name provided, set to default File or TimeSeries
        else
        {
            resolvedClass = recordClass;
        }
        return resolvedClass;
    }
}
