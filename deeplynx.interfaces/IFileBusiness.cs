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
}