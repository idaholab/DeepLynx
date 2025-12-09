using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace deeplynx.business;

public class FileAzureBusiness: IFileBusiness
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="datasourceId"></param>
    /// <param name="objectStorageConfig"></param>
    /// <param name="file"></param>
    /// <param name="guid"></param>
    /// <returns></returns>
    public async Task<string> UploadFile(long organizationId, long projectId, long datasourceId, ObjectStorageConfigDto objectStorageConfig,
        IFormFile file, Guid guid)
    {
        if (objectStorageConfig.AzureConnectionString == null)
        {
            throw new ArgumentException("Azure connection string is null");
        }
        string containerName = "nexus-files";

        string fileName = $"organizations/{organizationId}/projects/{projectId}/datasources/{datasourceId}/{guid}_{file.FileName}";

        // Get a reference to the container
        BlobContainerClient container = new BlobContainerClient(objectStorageConfig.AzureConnectionString, containerName);
        await container.CreateIfNotExistsAsync();

        // Get a reference to a blob (using the original filename from the uploaded file)
        BlobClient blob = container.GetBlobClient(fileName);

        // Upload the IFormFile
        await using var stream = file.OpenReadStream(); 
        await blob.UploadAsync(stream, overwrite: true);
        
        
        await foreach (BlobItem blobItem in container.GetBlobsAsync())
        {
            Console.WriteLine($"Blob: {blobItem.Name}");
        }
        return fileName;
    }

    public async Task<string> UpdateFile(RecordResponseDto record,  IFormFile file)
    {
        return "";
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="record"></param>
    /// <param name="objectStorageConfig"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<FileStreamResult> DownloadFile(RecordResponseDto record, ObjectStorageConfigDto? objectStorageConfig)
    {
        if (record.Uri == null)
        {
            throw new ArgumentException("Record Uri is null");
        }
        
        if (objectStorageConfig?.AzureConnectionString == null)
        {
            throw new ArgumentException("Azure connection string is null");
        }
        
        string containerName = "nexus-files";
        
        BlobContainerClient container = new BlobContainerClient(objectStorageConfig.AzureConnectionString, containerName);
        if (!await container.ExistsAsync())
        {
            throw new FileNotFoundException($"Can not connect to container");
        }

        BlobClient blob = container.GetBlobClient(record.Uri);

        if (!await blob.ExistsAsync())
        {
            throw new FileNotFoundException($"File not found: {record.Uri}");
        }

        var memoryStream = new MemoryStream();
        await blob.DownloadToAsync(memoryStream);
        memoryStream.Position = 0;
        
        // Detect file type
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(record.Uri, out var contentType))
        {
            contentType = "application/octet-stream"; // Default fallback
        }
        // Create a simple stub with empty content
        return new FileStreamResult(memoryStream, contentType)
        {
            FileDownloadName = record.Name
        };
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="record"></param>
    /// <param name="objectStorageConfig"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<bool> DeleteFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.AzureConnectionString == null)
        {
            throw new ArgumentException("Azure connection string is null");
        }
    
        string containerName = "nexus-files";

        // Get a reference to the container
        BlobContainerClient container = new BlobContainerClient(objectStorageConfig.AzureConnectionString, containerName);

        if (!await container.ExistsAsync())
        {
            throw new FileNotFoundException("Can not connect to container");
        }

        // Get a reference to the blob using the uri from the record
        BlobClient blob = container.GetBlobClient(record.Uri);

        // Delete the blob if it exists
        var response = await blob.DeleteIfExistsAsync();
    
        // Returns true if the blob was deleted, false if it didn't exist
        return response.Value;
    }
}