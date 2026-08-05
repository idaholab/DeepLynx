using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace deeplynx.business;

public class FileS3Business : IFileBusiness
{
    public async Task<string> UploadFile(long organizationId, long projectId, long datasourceId, ObjectStorageConfigDto objectStorageConfig,
        IFormFile file, Guid guid)
    {
        return "";
    }

    public async Task<string> UpdateFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig, IFormFile file, Guid guid)
    {
        return "";
    }

    public async Task<FileStreamResult> DownloadFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig)
    {
        // Create a simple stub with empty content
        var emptyStream = new MemoryStream();
        return new FileStreamResult(emptyStream, "application/octet-stream")
        {
            FileDownloadName = "stub-file.txt"
        };
    }

    public async Task<FileStreamResult> DownloadAppendedFile(
        RecordResponseDto record,
        ObjectStorageConfigDto objectStorageConfig,
        CancellationToken cancellationToken = default)
    {
        // Create a simple stub with empty content
        var emptyStream = new MemoryStream();
        return new FileStreamResult(emptyStream, "application/octet-stream")
        {
            FileDownloadName = "stub-file.txt"
        };
    }

    public async Task<bool> DeleteFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig)
    {
        return true;
    }

    public async Task<string> GenerateDownloadUrl(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig,
        int expirationHours = 1)
    {
        throw new NotImplementedException("Generate download urls is not implemented for filesystem");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectStorageConfig">Config allowing chunking to be set up and tested</param>
    public async Task<Guid> StartUpload(long organizationId, long projectId, long datasourceId, ObjectStorageConfigDto objectStorageConfig)
    {
        return new Guid();
    }

    public async Task UploadChunk(long organizationId, long projectId, long datasourceId, long chunkNumber, string uploadId,
        ObjectStorageConfigDto objectStorageConfig, IFormFile chunk)
    {

    }

    public async Task<string> CompleteUpload(long organizationId, long projectId, long datasourceId,
        ObjectStorageConfigDto objectStorageConfig, FileUploadCompleteRequestDto request, Guid guid)
    {
        return "";
    }

    public async Task CancelUpload(long organizationId, long projectId, long dataSourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
    }

    public async Task<long> GetStorageSize(string prefix, ObjectStorageConfigDto objectStorageConfig)
    {
        return 0;
    }

    public string BuildPrefix(long organizationId, long? projectId)
    {
        return "";
    }

    public async Task<long> GetFileSize(string fileUri, ObjectStorageConfigDto objectStorageConfig)
    {
        return 0;
    }

    public async Task<Guid> CreateUploadTus(long organizationId, long projectId, long realDataSourceId,
        ObjectStorageConfigDto objectStorageConfig, long uploadLength, string fIlename)
    {
        return Guid.Empty;
    }

    public async Task<long> GetUploadOffset(long organizationId, long projectId, long realDataSourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
        return 0;
    }

    public async Task<long> GetUploadLength(long organizationId, long projectId, long realDataSourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
        return 0;
    }

    public async Task<long> UploadPartTus(long organizationId, long projectId, long realDataSourceId, string uploadId,
        long uploadOffset, ObjectStorageConfigDto objectStorageConfig, System.IO.Stream uploadBody)
    {
        return 0;
    }

    public async Task<string> CompleteUploadTus(long organizationId, long projectId, long datasourceId,
        ObjectStorageConfigDto objectStorageConfig, string uploadId, Guid guid, string fileName)
    {
        return "";
    }

    public async Task<string> GetFileNameTus(
        long organizationId,
        long projectId,
        long realDataSourceId,
        string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
        return "";
    }

    /// <summary>
    /// Scrapes at most (batchSize * maxBatches) objects from an S3 storage, starting from the given cursor.
    /// </summary>
    /// <param name="awsConnectionString">Config.AwsConnectionString, e.g. s3://bucket/prefix?region=...&accessKey=...&secretKey=...</param>
    /// <param name="objectStorageId">The ID of the object storage being scraped</param>
    /// <param name="cursor">Continuation token from a previous call, or null to start from the beginning</param>
    /// <param name="batchSize">Number of records per batch (matches BulkCreateRecords batch size upstream)</param>
    /// <param name="maxBatches">Maximum number of batches to process before returning, bounding this call's duration</param>
    /// <param name="cancellationToken">Token checked between pages</param>
    public static async Task<ScrapeResult> ScrapeS3(
        string awsConnectionString,
        long objectStorageId,
        string? cursor,
        int batchSize,
        int maxBatches,
        CancellationToken cancellationToken = default)
    {
        return new ScrapeResult();
    }

    public async Task<ScrapeResult> ScrapeAsync(
        ObjectStorageDecryptedDto objectStorage,
        string? afterCursor,
        int batchSize,
        int maxBatches,
        CancellationToken cancellationToken = default)
    {
        return await ScrapeS3(
            objectStorage.Config.AwsConnectionString
                ?? throw new InvalidOperationException("S3 storage is missing its connection string."),
            objectStorage.Id,
            afterCursor,
            batchSize,
            maxBatches,
            cancellationToken);
    }
}