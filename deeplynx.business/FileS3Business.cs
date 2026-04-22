using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.business;

public class FileS3Business:  IFileBusiness
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

    public async Task<FileStreamResult> DownloadFile(RecordResponseDto record,  ObjectStorageConfigDto objectStorageConfig)
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
}