using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace deeplynx.business;

public class FileAzureBusiness: IFileBusiness
{
    /// <summary>
    /// Uploads a file to azure object storage instance specified in the object storage config
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="datasourceId"></param>
    /// <param name="objectStorageConfig"></param>
    /// <param name="file"></param>
    /// <param name="guid"></param>
    /// <returns></returns>
    public async Task<string> UploadFile(long organizationId, long organizationId, long projectId, long datasourceId, ObjectStorageConfigDto objectStorageConfig,
        IFormFile file, Guid guid)
    {
        if (objectStorageConfig.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure connection string is null");
        }

        var fileName = $"organization_{organizationId}/project_{projectId}/datasource_{datasourceId}/{guid}_{file.FileName}";

        // Get a reference to the container
        var container = new BlobContainerClient(objectStorageConfig.AzureObjectConfig.AzureConnectionString, objectStorageConfig.AzureObjectConfig.AzureContainerName);
        await container.CreateIfNotExistsAsync();

        // Get a reference to a blob (using the original filename from the uploaded file)
        var blob = container.GetBlobClient(fileName);

        // Upload the IFormFile
        await using var stream = file.OpenReadStream(); 
        await blob.UploadAsync(stream, overwrite: true);
        
        return fileName;
    }

    /// <summary>
    /// Replaces old file with a new one in Azure Object Storage
    /// </summary>
    /// <param name="record"></param>
    /// <param name="objectStorageConfig"></param>
    /// <param name="file"></param>
    /// <param name="guid"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="Exception"></exception>
    public async Task<string> UpdateFile(RecordResponseDto record, ObjectStorageConfigDto? objectStorageConfig,  IFormFile file, Guid guid)
    {
        if (record.Uri == null)
        {
            throw new ArgumentException("Record Uri is null");
        }
    
        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure configuration object is null");
        }
    
        var container = new BlobContainerClient(objectStorageConfig.AzureObjectConfig.AzureConnectionString, objectStorageConfig.AzureObjectConfig.AzureContainerName);
        if (!await container.ExistsAsync())
        {
            throw new InvalidOperationException("Azure Object Storage container does not exist");
        }

        var oldBlob = container.GetBlobClient(record.Uri);
    
        if (!await oldBlob.ExistsAsync())
        {
            throw new FileNotFoundException($"File not found: {record.Uri}");
        }
    
        var newFileName = $"organization_{record.OrganizationId}/projects_{record.ProjectId}/datasource_{record.DataSourceId}/{guid}_{file.FileName}";
        var newBlob = container.GetBlobClient(newFileName);
        
        // try-catch to try and revert to original state on failure
        try
        {
            // Upload new file FIRST
            await using var stream = file.OpenReadStream();
            await newBlob.UploadAsync(stream, overwrite: true);

            // Only delete old file after successful upload
            await oldBlob.DeleteAsync();

            return newFileName;
        }
        catch (Exception ex)
        {
            await newBlob.DeleteIfExistsAsync();

            throw new Exception($"Failed to update file: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Downloads a file from Azure Object Storage
    /// </summary>
    /// <param name="record"></param>
    /// <param name="objectStorageConfig"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<FileStreamResult> DownloadFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig)
    {
        if (record.Uri == null)
        {
            throw new ArgumentException("Record Uri is null");
        }
        
        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure connection string is null");
        }
        
        var container = new BlobContainerClient(objectStorageConfig.AzureObjectConfig.AzureConnectionString, objectStorageConfig.AzureObjectConfig.AzureContainerName);
        if (!await container.ExistsAsync())
        {
            throw new InvalidOperationException("Azure Object Storage container does not exist");
        }

        var blob = container.GetBlobClient(record.Uri);

        if (!await blob.ExistsAsync())
        {
            throw new FileNotFoundException($"File not found: {record.Uri}");
        }
        
        // Detect file type
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(record.Uri, out var contentType))
        {
            contentType = "application/octet-stream"; // Default fallback
        }
        
        var memoryStream = new MemoryStream();
        try
        {
            await blob.DownloadToAsync(memoryStream);
            memoryStream.Position = 0;
            return new FileStreamResult(memoryStream, contentType)
            {
                FileDownloadName = record.Name
            };
        }
        catch
        {
            // explicit memory disposal so we do not rely on garbage cleanup after error
            await memoryStream.DisposeAsync();
            throw;
        }
    }
    
    /// <summary>
    /// Deletes a file from Azure Object Storage
    /// </summary>
    /// <param name="record"></param>
    /// <param name="objectStorageConfig"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<bool> DeleteFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure connection string is null");
        }

        // Get a reference to the container
        var container = new BlobContainerClient(objectStorageConfig.AzureObjectConfig.AzureConnectionString, objectStorageConfig.AzureObjectConfig.AzureContainerName);

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
    
    /// <summary>
    /// Generates a pre-signed URL (SAS token) for uploading a file directly to Azure Blob Storage
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="datasourceId"></param>
    /// <param name="objectStorageConfig"></param>
    /// <param name="fileName">The name of the file to be uploaded</param>
    /// <param name="guid">Unique identifier for the file</param>
    /// <param name="expirationHours">Hours until the SAS token expires (default: 24)</param>
    /// <returns>Pre-signed URL with SAS token for direct upload</returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<string> GenerateUploadUrl(
        long organizationId, 
        long projectId, 
        long datasourceId, 
        ObjectStorageConfigDto objectStorageConfig,
        string fileName, 
        Guid guid,
        int expirationHours = 24)
    {
        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure configuration is null");
        }

        var blobName = $"organization_{organizationId}/project_{projectId}/datasource_{datasourceId}/{guid}_{fileName}";

        // Create BlobContainerClient with connection string
        var containerClient = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString, 
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        // Ensure container exists
        await containerClient.CreateIfNotExistsAsync();

        // Get blob client reference
        var blobClient = containerClient.GetBlobClient(blobName);

        // Check if the blob client can generate SAS URI (requires Shared Key authentication)
        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException("BlobClient must be authorized with Shared Key credentials to generate SAS tokens");
        }

        // Create SAS builder with write and create permissions
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = objectStorageConfig.AzureObjectConfig.AzureContainerName,
            BlobName = blobName,
            Resource = "b", // "b" for blob
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Account for clock skew
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(expirationHours)
        };

        // Set permissions for upload (Write and Create)
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        // Generate the SAS URI
        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        return sasUri.ToString();
    }
    
    /// <summary>
    /// Generates a pre-signed URL (SAS token) for downloading a file directly from Azure Blob Storage
    /// </summary>
    /// <param name="record"></param>
    /// <param name="objectStorageConfig"></param>
    /// <param name="expirationHours">Hours until the SAS token expires (default: 1)</param>
    /// <returns>Pre-signed URL with SAS token for direct download</returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<string> GenerateDownloadUrl(
        RecordResponseDto record, 
        ObjectStorageConfigDto objectStorageConfig,
        int expirationHours = 1)
    {
        if (record.Uri == null)
        {
            throw new ArgumentException("Record Uri is null");
        }

        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure configuration is null");
        }

        // Create BlobContainerClient with connection string
        var containerClient = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString, 
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        // Verify container exists
        if (!await containerClient.ExistsAsync())
        {
            throw new InvalidOperationException("Azure Object Storage container does not exist");
        }

        // Get blob client reference
        var blobClient = containerClient.GetBlobClient(record.Uri);

        // Verify blob exists
        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"File not found: {record.Uri}");
        }

        // Check if the blob client can generate SAS URI
        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException("BlobClient must be authorized with Shared Key credentials to generate SAS tokens");
        }

        // Create SAS builder with read permissions
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = objectStorageConfig.AzureObjectConfig.AzureContainerName,
            BlobName = record.Uri,
            Resource = "b", // "b" for blob
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Account for clock skew
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(expirationHours)
        };

        // Set permissions for download (Read only)
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        // Generate the SAS URI
        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        return sasUri.ToString();
    }
}