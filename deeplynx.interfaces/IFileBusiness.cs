using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.interfaces;

public interface IFileBusiness
{
    Task<string> UploadFile(long organizationId, long projectId, long datasourceId, ObjectStorageConfigDto objectStorageConfig, IFormFile file, Guid guid);
    Task<string> UpdateFile(RecordResponseDto record, IFormFile file);
    Task<FileStreamResult> DownloadFile(RecordResponseDto record);
    Task<bool> DeleteFile(RecordResponseDto record);
}