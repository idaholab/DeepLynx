using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.interfaces;

public interface IFileBusiness
{
    Task<string> UploadFile(long organizationId, long projectId, long datasourceId,
        ObjectStorageConfigDto objectStorageConfig, IFormFile file, Guid guid);

    Task<string> UpdateFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig, IFormFile file,
        Guid guid);

    Task<FileStreamResult> DownloadFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig);
    Task<bool> DeleteFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig);

    Task<string> GenerateDownloadUrl(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig,
        int expirationHours = 1);

    Task<Guid> StartUpload(long organizationId, long projectId, long datasourceId,
        ObjectStorageConfigDto objectStorageConfig);

    Task UploadChunk(long organizationId, long projectId, long datasourceId, long chunkNumber, string uploadId,
        ObjectStorageConfigDto objectStorageConfig, IFormFile chunk);
    Task<string> CompleteUpload(long organizationId, long projectId, long dataSourceId,
        ObjectStorageConfigDto objectStorageConfig, FileUploadCompleteRequestDto request, Guid guid);

    Task CancelUpload(long organizationId, long projectId, long dataSourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig);

    Task<long> GetStorageSize(string prefix, ObjectStorageConfigDto objectStorageConfig);

    string BuildPrefix(long organizationId, long? projectId);

    Task<long> GetFileSize(string fileUri, ObjectStorageConfigDto objectStorageConfig);
    Task<Guid> CreateUploadTus(long organizationId, long projectId, long realDataSourceId,
        ObjectStorageConfigDto objectStorageConfig, long uploadLength, string fileName);
    Task<long> GetUploadOffset(long organizationId, long projectId, long realDataSourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig);
    Task<long> GetUploadLength(long organizationId, long projectId, long realDataSourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig);
    Task<long> UploadPartTus(long organizationId, long projectId, long realDataSourceId, string uploadId, long uploadOffset,
        ObjectStorageConfigDto objectStorageConfig, System.IO.Stream uploadBody);
    Task<string> CompleteUploadTus(long organizationId, long projectId, long datasourceId,
        ObjectStorageConfigDto objectStorageConfig, string uploadId, Guid guid, string fileName);
    Task<string> GetFileNameTus(long organizationId, long projectId, long realDataSourceId,
        string uploadId, ObjectStorageConfigDto objectStorageConfig);
}