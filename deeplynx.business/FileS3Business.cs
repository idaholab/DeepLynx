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

    public async Task<string> UpdateFile(RecordResponseDto record, IFormFile file)
    {
        return "";
    }

    public async Task<FileStreamResult> DownloadFile(RecordResponseDto record)
    {
        // Create a simple stub with empty content
        var emptyStream = new MemoryStream();
        return new FileStreamResult(emptyStream, "application/octet-stream")
        {
            FileDownloadName = "stub-file.txt"
        };
    }

    public async Task<bool> DeleteFile(RecordResponseDto record)
    {
        return true;
    }
}