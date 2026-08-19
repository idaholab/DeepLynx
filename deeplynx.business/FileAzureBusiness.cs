using System.IO.Compression;
using System.IO.Pipelines;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using deeplynx.datalayer.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using deeplynx.helpers;
using Microsoft.EntityFrameworkCore;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.ComponentModel;

namespace deeplynx.business;

public class FileAzureBusiness : IFileBusiness
{
    private readonly DeeplynxContext _context;
    private readonly EncryptionHelper _encryptionHelper;

    public FileAzureBusiness(
        DeeplynxContext context,
        EncryptionHelper encryptionHelper)
    {
        _context = context;
        _encryptionHelper = encryptionHelper;
    }

    public async Task<string?> CalculateFileContentHash(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        await using var stream = file.OpenReadStream();
        return await Sha256HashHelper.ComputeHexAsync(stream, cancellationToken);
    }

    public async Task<string?> CalculateStoredFileContentHash(
        string fileUri,
        ObjectStorageConfigDto objectStorageConfig,
        CancellationToken cancellationToken = default)
    {
        if (objectStorageConfig.AzureObjectConfig == null)
            throw new ArgumentException("Azure configuration object is null");

        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);
        var blob = container.GetBlobClient(fileUri);
        var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);

        await using var stream = download.Value.Content;
        return await Sha256HashHelper.ComputeHexAsync(stream, cancellationToken);
    }

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
    public async Task<string> UploadFile(
    long organizationId,
    long projectId,
    long datasourceId,
    ObjectStorageConfigDto objectStorageConfig,
    IFormFile file,
    Guid guid)
    {
        if (objectStorageConfig.AzureObjectConfig == null)
            throw new ArgumentException("AzureObjectConfig is null");

        var azureConfig = objectStorageConfig.AzureObjectConfig;

        if (string.IsNullOrWhiteSpace(azureConfig.AzureConnectionString))
            throw new ArgumentException("Azure connection string is null or empty");

        if (string.IsNullOrWhiteSpace(azureConfig.AzureContainerName))
            throw new ArgumentException("Azure container name is null or empty");

        var baseFilePath = azureConfig.AzureFilePath ?? string.Empty;

        if (!SanitizeFilePath.IsValidFilePath(baseFilePath))
            throw new ArgumentException("Invalid Azure file path. Allowed characters are letters (a-z, A-Z), numbers (0-9), and '/'.");

        var filePath = string.IsNullOrEmpty(baseFilePath)
            ? $"organization_{organizationId}/project_{projectId}/datasource_{datasourceId}/{guid}_{file.FileName}"
            : $"{baseFilePath.TrimEnd('/')}/{guid}_{file.FileName}";



        var containerClient = new BlobContainerClient(azureConfig.AzureConnectionString, azureConfig.AzureContainerName);
        await containerClient.CreateIfNotExistsAsync();

        var blobClient = containerClient.GetBlobClient(filePath);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, overwrite: true);

        return filePath;
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
    public async Task<string> UpdateFile(RecordResponseDto record, ObjectStorageConfigDto? objectStorageConfig, IFormFile file, Guid guid)
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

        var newFileName = $"organization_{record.OrganizationId}/project_{record.ProjectId}/datasource_{record.DataSourceId}/{guid}_{file.FileName}";
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
    ///     Creates an Azure Blob Container
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the object storage belongs</param>
    /// <param name="containerName">The name of the container</param>
    /// <param name="connectionString">The connection string to connect to Azure</param>
    /// <param name="isDefault">Specifies whether the resulting obj storage DTO should be default</param>
    /// <param name="existingContainer">Specifies whether the container exists already</param>
    public async Task<CreateObjectStorageRequestDto> CreateContainer(
        long organizationId,
        string containerName,
        string? connectionString,
        bool isDefault = false,
        bool existingContainer = false)
    {
        const int maxContainerNameLength = 63;

        if (containerName.Length > maxContainerNameLength || containerName.Length < 3)
            throw new Exception("Generated container name does not comply with Azure Blob storage naming rules.");

        BlobServiceClient blobServiceClient;
        string effectiveConnectionString;

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            effectiveConnectionString = connectionString;
        }
        else
        {
            var defaultObjectStorage = await _context.ObjectStorages
                .Where(os => os.OrganizationId == organizationId && os.ProjectId == null && os.Default && os.Type == "azure_object")
                .FirstOrDefaultAsync() ?? throw new KeyNotFoundException("No default Azure object storage found for the organization.");

            var azureConfig = DeserializeAndDecryptConfig(defaultObjectStorage.ConfigEncrypted);

            if (azureConfig == null || string.IsNullOrWhiteSpace(azureConfig.AzureObjectConfig?.AzureConnectionString))
                throw new Exception("Invalid or missing Azure configuration in the default object storage.");

            effectiveConnectionString = azureConfig.AzureObjectConfig.AzureConnectionString;
        }

        if (!existingContainer)
        {
            blobServiceClient = new BlobServiceClient(effectiveConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            await containerClient.CreateIfNotExistsAsync();
        }

        var objectStorageName = ContainerName.UniqueContainerNameFromString(containerName);

        var newObjectStorageDto = new CreateObjectStorageRequestDto
        {
            Name = objectStorageName,
            Config = new ObjectStorageConfigDto
            {
                AzureObjectConfig = new AzureObjectConfigDto
                {
                    AzureConnectionString = effectiveConnectionString,
                    AzureContainerName = containerName,
                }
            },
            Default = isDefault
        };

        return newObjectStorageDto;
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

        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        if (!await container.ExistsAsync())
        {
            throw new InvalidOperationException("Azure Object Storage container does not exist");
        }

        var blob = container.GetBlobClient(record.Uri);

        if (!await blob.ExistsAsync())
        {
            throw new FileNotFoundException($"File not found: {record.Uri}");
        }

        // Get blob properties for content length
        var properties = await blob.GetPropertiesAsync();
        var contentLength = properties.Value.ContentLength;

        // Detect file type
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(record.Uri, out var contentType))
        {
            contentType = "application/octet-stream"; // Default fallback
        }

        // Download the blob content as a stream
        var downloadResponse = await blob.DownloadStreamingAsync();

        // Return with content length for progress tracking
        return new FileStreamResultWithLength(downloadResponse.Value.Content, contentType, contentLength)
        {
            FileDownloadName = record.Name,
            EnableRangeProcessing = true
        };
    }

    /// <summary>
    /// Download an Appended File
    /// </summary>
    /// <param name="record"></param>
    /// <param name="objectStorageConfig"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<FileStreamResult> DownloadAppendedFile(
        RecordResponseDto record,
        ObjectStorageConfigDto objectStorageConfig,
        CancellationToken cancellationToken = default)
    {
        const long MaxBufferedFileSize = 10 * 1024 * 1024; // 10 MB

        if (record.Uri == null)
            throw new ArgumentException("Record Uri is null");
        if (objectStorageConfig.AzureObjectConfig == null)
            throw new ArgumentException("Azure configuration is null");

        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        if (!await container.ExistsAsync(cancellationToken))
            throw new InvalidOperationException("Azure Object Storage container does not exist");

        var prefix = record.Uri;
        var pipe = new Pipe();

        string lastFolderName = Path.GetFileName(record.Uri.TrimEnd('/', '\\'));

        int underscoreIndex = lastFolderName.LastIndexOf('_');
        string suffix = underscoreIndex >= 0 && underscoreIndex < lastFolderName.Length - 1
            ? lastFolderName.Substring(underscoreIndex + 1)
            : string.Empty;

        string zipFileName = suffix + ".zip";

        _ = Task.Run(async () =>
        {
            Exception? error = null;
            try
            {
                await using var pipeStream = pipe.Writer.AsStream(leaveOpen: true);
                using var archive = new ZipArchive(pipeStream, ZipArchiveMode.Create, leaveOpen: true);

                // Bounded channel between the concurrent downloader and the sequential zip writer.
                // Items carry EITHER buffered content (small blobs) OR just the blob name
                // (large blobs, streamed by the consumer at write time).
                // Worst-case buffered memory ~= capacity * MaxBufferedFileSize.
                var channel = Channel.CreateBounded<(string EntryName, byte[]? Content, string? BlobName)>(
                    new BoundedChannelOptions(64)
                    {
                        FullMode = BoundedChannelFullMode.Wait, // producer waits when channel is full
                        SingleReader = true,                    // only the consumer reads
                        SingleWriter = false                    // multiple download tasks write
                    });

                // Producer: enumerate blobs lazily. Small blobs are downloaded
                // concurrently and buffered; large blobs are enqueued as name-only
                // items so their bytes never sit in memory.
                var producer = Task.Run(async () =>
                {
                    try
                    {
                        await Parallel.ForEachAsync(
                            container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken),
                            new ParallelOptions
                            {
                                MaxDegreeOfParallelism = 32,
                                CancellationToken = cancellationToken
                            },
                            async (blobItem, ct) =>
                            {
                                var entryName = blobItem.Name[prefix!.Length..];
                                if (string.IsNullOrEmpty(entryName)) return;

                                var length = blobItem.Properties.ContentLength ?? long.MaxValue;

                                if (length <= MaxBufferedFileSize)
                                {
                                    // Small blob: buffer it. For tiny files the bottleneck
                                    // is per-request latency, so downloading them with
                                    // high concurrency here is the big win.
                                    var blobClient = container.GetBlobClient(blobItem.Name);
                                    var download = await blobClient.DownloadContentAsync(ct);
                                    await channel.Writer.WriteAsync((entryName, download.Value.Content.ToArray(), null), ct);
                                }
                                else
                                {
                                    // Large blob: defer the download; the consumer will
                                    // stream it directly into the zip entry.
                                    await channel.Writer.WriteAsync((entryName, null, blobItem.Name), ct);
                                }
                            });

                        // Signal consumer that no more blobs are coming
                        channel.Writer.Complete();
                    }
                    catch (Exception ex)
                    {
                        // Propagate the error to the consumer so it doesn't wait forever
                        channel.Writer.Complete(ex);
                        throw;
                    }
                }, cancellationToken);

                // Consumer: write blobs to the zip archive one at a time.
                // ZipArchive is not thread-safe — sequential writes are required.
                var consumer = Task.Run(async () =>
                {
                    await foreach (var (entryName, content, blobName) in channel.Reader.ReadAllAsync(cancellationToken))
                    {
                        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                        await using var entryStream = entry.Open();

                        if (content is not null)
                        {
                            await entryStream.WriteAsync(content, cancellationToken);
                        }
                        else
                        {
                            // Large blob: stream from Azure straight into the zip
                            // entry — never fully materialized in memory.
                            var blobClient = container.GetBlobClient(blobName!);
                            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
                            await using var blobStream = response.Value.Content;
                            await blobStream.CopyToAsync(entryStream, cancellationToken);
                        }
                    }
                }, cancellationToken);

                // Wait for both to finish — if either throws, the exception surfaces here
                await Task.WhenAll(producer, consumer);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                await pipe.Writer.CompleteAsync(error);
            }
        }, cancellationToken);

        return new FileStreamResult(pipe.Reader.AsStream(), "application/zip")
        {
            FileDownloadName = zipFileName,
            EnableRangeProcessing = false
        };
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

        var blobName = record.Uri.TrimStart('/');

        var blobClient = containerClient.GetBlobClient(blobName);

        // Verify blob exists
        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException($"File not found: {record.Uri}");
        }

        // Check if the blob client can generate SAS URI
        if (!blobClient.CanGenerateSasUri)
        {
            await DownloadFile(record, objectStorageConfig);
            return "Cannot Create SAS URI";
        }

        // Create SAS builder with read permissions
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = objectStorageConfig.AzureObjectConfig.AzureContainerName,
            BlobName = blobName,
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


    public async Task<Guid> StartUpload(long organizationId, long projectId, long datasourceId, ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure configuration is null");
        }

        // Generate a unique upload ID for this session
        var uploadId = Guid.NewGuid();

        // Verify container exists
        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        await container.CreateIfNotExistsAsync();

        return uploadId;
    }

    /// <summary>
    /// Uploads a single chunk directly as a block to the Block Blob
    /// </summary>
    public async Task UploadChunk(long organizationId, long projectId, long datasourceId, long chunkNumber,
        string uploadId, ObjectStorageConfigDto objectStorageConfig, IFormFile chunk)
    {
        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure configuration is null");
        }

        if (chunk == null || chunk.Length == 0)
        {
            throw new ArgumentException("No chunk data provided");
        }

        // The blob name that will eventually hold the complete file
        // We stage blocks to this blob without committing yet
        var blobName = $"organization_{organizationId}/project_{projectId}/datasource_{datasourceId}/uploads/{uploadId}";

        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        if (!await container.ExistsAsync())
        {
            throw new InvalidOperationException("Azure Object Storage container does not exist");
        }

        // Get BlockBlobClient for direct block operations
        var blockBlobClient = container.GetBlockBlobClient(blobName);

        try
        {
            // Generate a base64-encoded block ID (must be consistent and under 64 bytes)
            // Using zero-padded chunk number to ensure proper ordering
            var blockId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"block-{chunkNumber:D10}"));

            // Stage the block directly from the chunk stream
            // This uploads the chunk as an uncommitted block
            await using var stream = chunk.OpenReadStream();
            await blockBlobClient.StageBlockAsync(blockId, stream);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload chunk {chunkNumber}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Completes the chunked upload by committing all staged blocks into a single Block Blob
    /// </summary>
    public async Task<string> CompleteUpload(long organizationId, long projectId, long datasourceId,
        ObjectStorageConfigDto objectStorageConfig, FileUploadCompleteRequestDto request, Guid guid)
    {
        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure configuration is null");
        }

        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        if (!await container.ExistsAsync())
        {
            throw new InvalidOperationException("Azure Object Storage container does not exist");
        }

        // The temporary blob where blocks were staged
        var tempBlobName = $"organization_{organizationId}/project_{projectId}/datasource_{datasourceId}/uploads/{request.UploadId}";
        var tempBlockBlobClient = container.GetBlockBlobClient(tempBlobName);

        // Final blob name following your naming convention
        var finalBlobName = $"organization_{organizationId}/project_{projectId}/datasource_{datasourceId}/{guid}_{request.FileName}";
        var finalBlockBlobClient = container.GetBlockBlobClient(finalBlobName);

        try
        {
            // Create a list of block IDs in the correct order
            var blockIds = new List<string>();
            for (int i = 0; i < request.TotalChunks; i++)
            {
                var blockId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"block-{i:D10}"));
                blockIds.Add(blockId);
            }

            // Get the list of uncommitted blocks to verify all chunks are present
            var blockList = await tempBlockBlobClient.GetBlockListAsync(BlockListTypes.Uncommitted);
            var uncommittedBlocks = blockList.Value.UncommittedBlocks.ToList();

            if (uncommittedBlocks.Count != request.TotalChunks)
            {
                throw new InvalidOperationException(
                    $"Missing chunks. Expected {request.TotalChunks}, found {uncommittedBlocks.Count} uncommitted blocks");
            }

            // Commit all blocks to create the final blob at the temp location
            await tempBlockBlobClient.CommitBlockListAsync(blockIds);

            // Copy the committed blob to the final location with proper naming
            var copyOperation = await finalBlockBlobClient.StartCopyFromUriAsync(tempBlockBlobClient.Uri);

            // Wait for copy to complete (usually instant for same storage account)
            await copyOperation.WaitForCompletionAsync();

            // Delete the temporary blob after successful copy
            await tempBlockBlobClient.DeleteIfExistsAsync();

            return finalBlobName;
        }
        catch (Exception ex)
        {
            // Clean up on failure
            await finalBlockBlobClient.DeleteIfExistsAsync();
            await tempBlockBlobClient.DeleteIfExistsAsync();

            throw new InvalidOperationException($"Failed to complete upload: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cancels an in-progress chunked upload and cleans up uncommitted blocks
    /// </summary>
    public async Task CancelUpload(long organizationId, long projectId, long dataSourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure configuration is null");
        }

        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        if (!await container.ExistsAsync())
        {
            return; // Nothing to clean up if container doesn't exist
        }

        // The temporary blob where blocks were staged
        var tempBlobName = $"organization_{organizationId}/project_{projectId}/datasource_{dataSourceId}/uploads/{uploadId}";
        var blockBlobClient = container.GetBlockBlobClient(tempBlobName);

        // Delete the blob - this automatically removes all uncommitted blocks associated with it
        await blockBlobClient.DeleteIfExistsAsync();
    }

    /// <summary>
    /// Gets the total storage size in bytes for files matching the given prefix in Azure Blob Storage.
    /// </summary>
    /// <param name="prefix">The blob prefix to search (e.g., "organization_1/project_2/")</param>
    /// <param name="objectStorageConfig">Azure storage configuration</param>
    /// <returns>Total bytes used by blobs matching the prefix</returns>
    public async Task<long> GetStorageSize(string prefix, ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.AzureObjectConfig == null)
            return 0;

        long totalSize = 0;

        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        if (!await container.ExistsAsync())
            return 0;

        await foreach (var blobItem in container.GetBlobsAsync(
                           prefix: string.IsNullOrEmpty(prefix) ? null : prefix))
        {
            if (blobItem.Properties.ContentLength.HasValue)
            {
                totalSize += blobItem.Properties.ContentLength.Value;
            }
        }

        return totalSize;
    }

    /// <summary>
    /// Builds the Azure-specific blob prefix.
    /// Azure uses the format: organization_{id}/project_{id}/
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="projectId">Optional project ID</param>
    /// <returns>Azure blob prefix string</returns>
    public string BuildPrefix(long organizationId, long? projectId)
    {
        // Azure format: organization_1/project_2/
        if (projectId.HasValue)
            return $"organization_{organizationId}/project_{projectId.Value}/";
        else
            return $"organization_{organizationId}/";
    }

    /// <summary>
    ///     Return the size of a given file. Used to backfill records for files that didn't get file size set on upload.
    /// </summary>
    /// <param name="fileUri">URI of the file whose size is to be measured</param>
    /// <param name="objectStorageConfig">object storage configuration for reaching URI</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<long> GetFileSize(string fileUri, ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.AzureObjectConfig == null)
            throw new Exception("Azure configuration not set in object storage");

        if (string.IsNullOrWhiteSpace(fileUri))
            throw new ArgumentException("File URI is not specified.");

        try
        {
            // Create BlobContainerClient with connection string
            var containerClient = new BlobContainerClient(
                objectStorageConfig.AzureObjectConfig.AzureConnectionString,
                objectStorageConfig.AzureObjectConfig.AzureContainerName);

            // Get blob client reference
            var blobClient = containerClient.GetBlobClient(fileUri);

            // check for properties- will throw if blob doesn't exist
            var properties = await blobClient.GetPropertiesAsync();

            return properties.Value.ContentLength;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get size for file {fileUri}: {ex.Message}");
        }
    }

    /// <summary>
    ///     Same as GetFileSize, but using a batch operation to reduce azure API calls.
    /// </summary>
    /// <param name="fileUris">URIs of the files whose size is to be measured</param>
    /// <param name="objectStorageConfig">object storage config for reaching URIs</param>
    /// <returns></returns>
    /// <exception cref="Exception">Returned if object storage is null</exception>
    public async Task<Dictionary<string, long>> GetFileSizesBatch(
        List<string> fileUris,
        ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.AzureObjectConfig == null)
            throw new Exception("Azure configuration not set in object storage");

        var results = new Dictionary<string, long>();
        var requestedUris = fileUris.ToHashSet();

        // Create BlobContainerClient with connection string
        var containerClient = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        if (!await containerClient.ExistsAsync())
            return results;

        // Get all blobs at once with their properties
        await foreach (var blob in containerClient.GetBlobsAsync())
        {
            if (requestedUris.Contains(blob.Name) && blob.Properties.ContentLength.HasValue)
            {
                results[blob.Name] = blob.Properties.ContentLength.Value;
            }

            // exit once all requested files are found
            if (results.Count == requestedUris.Count)
                break;
        }

        return results;
    }

    public async Task<Guid> CreateUploadTus(long organizationId, long projectId, long realDataSourceId,
        ObjectStorageConfigDto objectStorageConfig, long uploadLength, string fileName)
    {
        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure configuration is null");
        }

        // Generate a unique upload ID for this session
        var uploadId = Guid.NewGuid();

        // Verify container exists
        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        await container.CreateIfNotExistsAsync();

        var blobName = $"organization_{organizationId}/project_{projectId}/datasource_{realDataSourceId}/uploads/{uploadId}";

        await container.GetBlockBlobClient(blobName).UploadAsync(
            new MemoryStream(Array.Empty<byte>()),
            new BlobUploadOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["filename"] = fileName,
                    ["uploadLength"] = uploadLength.ToString()
                }
            });

        return uploadId;
    }

    public async Task<long> GetUploadOffset(long organizationId, long projectId, long realDataSourceId, string uploadId, ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.AzureObjectConfig == null)
            throw new Exception("Azure configuration not set in object storage");

        if (string.IsNullOrWhiteSpace(uploadId))
            throw new ArgumentException("Upload ID is not specified.");

        try
        {
            var containerClient = new BlobContainerClient(
                objectStorageConfig.AzureObjectConfig.AzureConnectionString,
                objectStorageConfig.AzureObjectConfig.AzureContainerName);

            if (!await containerClient.ExistsAsync())
                return 0;


            var blobName = $"organization_{organizationId}/project_{projectId}/datasource_{realDataSourceId}/uploads/{uploadId}";

            var blobClient = containerClient.GetBlockBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
                return 0;

            var properties = await blobClient.GetPropertiesAsync();

            try
            {
                var blockList = await blobClient.GetBlockListAsync(BlockListTypes.Uncommitted);

                return blockList.Value.UncommittedBlocks.Sum(block => block.SizeLong);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return 0;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get upload offset for upload {uploadId}: {ex.Message}", ex);
        }
    }

    public async Task<long> GetUploadLength(long organizationId, long projectId, long realDataSourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.AzureObjectConfig == null)
            throw new Exception("Azure configuration not set in object storage");

        if (string.IsNullOrWhiteSpace(uploadId))
            throw new ArgumentException("Upload ID is not specified.");

        try
        {
            var containerClient = new BlobContainerClient(
                objectStorageConfig.AzureObjectConfig.AzureConnectionString,
                objectStorageConfig.AzureObjectConfig.AzureContainerName);

            if (!await containerClient.ExistsAsync())
                return 0;


            var blobName = $"organization_{organizationId}/project_{projectId}/datasource_{realDataSourceId}/uploads/{uploadId}";

            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
                return 0;

            var properties = await blobClient.GetPropertiesAsync();
            var uploadLength = long.Parse(properties.Value.Metadata["uploadLength"]);

            return uploadLength;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get upload offset for upload {uploadId}: {ex.Message}", ex);
        }
    }

    public async Task<long> UploadPartTus(long organizationId, long projectId, long realDataSourceId, string uploadId,
        long uploadOffset, ObjectStorageConfigDto objectStorageConfig, System.IO.Stream uploadBody)
    {
        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure configuration is null");
        }

        if (uploadBody == null)
        {
            throw new ArgumentException("No upload Body data provided");
        }

        await using var bufferedUploadBody = new MemoryStream();
        await uploadBody.CopyToAsync(bufferedUploadBody);

        if (bufferedUploadBody.Length == 0)
        {
            throw new ArgumentException("No upload Body data provided");
        }

        var bytesUploaded = bufferedUploadBody.Length;
        bufferedUploadBody.Position = 0;

        // The blob name that will eventually hold the complete file
        // We stage blocks to this blob without committing yet
        var blobName = $"organization_{organizationId}/project_{projectId}/datasource_{realDataSourceId}/uploads/{uploadId}";

        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        if (!await container.ExistsAsync())
        {
            throw new InvalidOperationException("Azure Object Storage container does not exist");
        }

        // Get BlockBlobClient for direct block operations
        var blockBlobClient = container.GetBlockBlobClient(blobName);

        try
        {
            // Generate a base64-encoded block ID (must be consistent and under 64 bytes)
            // Using zero-padded upload offset number to ensure proper ordering
            var blockId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"block-{uploadOffset:D10}"));


            // This uploads the stream as an uncommitted block
            await blockBlobClient.StageBlockAsync(blockId, bufferedUploadBody);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload offset at {uploadOffset}: {ex.Message}", ex);
        }


        return uploadOffset + bytesUploaded;
    }

    public async Task<string> CompleteUploadTus(long organizationId, long projectId, long datasourceId,
        ObjectStorageConfigDto objectStorageConfig, string uploadId, Guid guid, string fileName)
    {
        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure configuration is null");
        }

        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        if (!await container.ExistsAsync())
        {
            throw new InvalidOperationException("Azure Object Storage container does not exist");
        }

        // The temporary blob where blocks were staged
        var tempBlobName = $"organization_{organizationId}/project_{projectId}/datasource_{datasourceId}/uploads/{uploadId}";
        var tempBlockBlobClient = container.GetBlockBlobClient(tempBlobName);

        // Final blob name following your naming convention
        var finalBlobName = $"organization_{organizationId}/project_{projectId}/datasource_{datasourceId}/{guid}_{fileName}";
        var finalBlockBlobClient = container.GetBlockBlobClient(finalBlobName);

        try
        {
            // Create a list of block IDs in the correct order
            var blockList = await tempBlockBlobClient.GetBlockListAsync(BlockListTypes.Uncommitted);
            var blockIds = blockList.Value.UncommittedBlocks
                .OrderBy(block => long.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(block.Name)).Replace("block-", "")))
                .Select(block => block.Name)
                .ToList();

            var uncommittedBlocks = blockList.Value.UncommittedBlocks.ToList();

            // Commit all blocks to create the final blob at the temp location
            await tempBlockBlobClient.CommitBlockListAsync(blockIds);

            // Copy the committed blob to the final location with proper naming
            var copyOperation = await finalBlockBlobClient.StartCopyFromUriAsync(tempBlockBlobClient.Uri);

            // Wait for copy to complete (usually instant for same storage account)
            await copyOperation.WaitForCompletionAsync();

            // Delete the temporary blob after successful copy
            await tempBlockBlobClient.DeleteIfExistsAsync();

            return finalBlobName;
        }
        catch (Exception ex)
        {
            // Clean up on failure
            await finalBlockBlobClient.DeleteIfExistsAsync();
            await tempBlockBlobClient.DeleteIfExistsAsync();

            throw new InvalidOperationException($"Failed to complete upload: {ex.Message}", ex);
        }
    }

    public async Task<string> GetFileNameTus(
        long organizationId,
        long projectId,
        long realDataSourceId,
        string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig?.AzureObjectConfig == null)
        {
            throw new ArgumentException("Azure configuration is null");
        }

        if (string.IsNullOrWhiteSpace(uploadId))
        {
            throw new ArgumentException("Upload ID is not specified.");
        }

        var container = new BlobContainerClient(
            objectStorageConfig.AzureObjectConfig.AzureConnectionString,
            objectStorageConfig.AzureObjectConfig.AzureContainerName);

        var blobName = $"organization_{organizationId}/project_{projectId}/datasource_{realDataSourceId}/uploads/{uploadId}";

        var blockBlobClient = container.GetBlockBlobClient(blobName);

        var properties = await blockBlobClient.GetPropertiesAsync();

        if (!properties.Value.Metadata.TryGetValue("filename", out var fileName))
        {
            throw new InvalidOperationException($"Filename metadata not found for upload {uploadId}");
        }

        return fileName;
    }

    private ObjectStorageConfigDto DeserializeAndDecryptConfig(string encryptedConfig)
    {
        return _encryptionHelper.DeserializeAndDecrypt<ObjectStorageConfigDto>(encryptedConfig);
    }

    /// <summary>
    /// Scrapes at most (batchSize * maxBatches) blobs from an Azure Blob storage, starting from the given cursor.
    /// </summary>
    /// <param name="config">Config.AzureObjectConfig</param>
    /// <param name="objectStorageId">The ID of the object storage being scraped</param>
    /// <param name="cursor">Continuation token from a previous call, or null to start from the beginning</param>
    /// <param name="batchSize">Number of records per batch</param>
    /// <param name="maxBatches">Maximum number of batches to process before returning</param>
    /// <param name="cancellationToken">Token checked between pages</param>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task<ScrapeResult> ScrapeAzureBlob(
        AzureObjectConfigDto config,
        long objectStorageId,
        string? cursor,
        int batchSize,
        int maxBatches,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.AzureConnectionString))
            throw new InvalidOperationException("AzureObjectConfig is missing a connection string.");
        if (string.IsNullOrWhiteSpace(config.AzureContainerName))
            throw new InvalidOperationException("AzureObjectConfig is missing a container name.");

        var containerClient = new BlobContainerClient(config.AzureConnectionString, config.AzureContainerName);

        var result = new ScrapeResult();
        var currentBatch = new List<CreateRecordRequestDto>(batchSize);
        var batchesCompleted = 0;
        string? continuationToken = cursor;

        var pageable = containerClient.GetBlobsAsync(cancellationToken: cancellationToken)
            .AsPages(continuationToken);

        await foreach (var page in pageable)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var blobItem in page.Values)
            {
                var properties = new JsonObject
                {
                    ["lastModified"] = blobItem.Properties.LastModified?.ToString("o"),
                    ["contentType"] = blobItem.Properties.ContentType,
                    ["etag"] = blobItem.Properties.ETag?.ToString()
                };

                var extension = Path.GetExtension(blobItem.Name);

                currentBatch.Add(new CreateRecordRequestDto
                {
                    Name = Path.GetFileName(blobItem.Name),
                    Description = blobItem.Name,
                    ObjectStorageId = objectStorageId,
                    Uri = blobItem.Name,
                    Properties = properties,
                    OriginalId = blobItem.Name,
                    FileType = string.IsNullOrEmpty(extension) ? null : extension.TrimStart('.'),
                    FileSize = blobItem.Properties.ContentLength ?? 0
                });

                if (currentBatch.Count >= batchSize)
                {
                    result.Records.AddRange(currentBatch);
                    currentBatch = new List<CreateRecordRequestDto>(batchSize);
                    batchesCompleted++;
                }
            }

            continuationToken = page.ContinuationToken;

            if (batchesCompleted >= maxBatches && !string.IsNullOrEmpty(continuationToken))
            {
                break;
            }

            if (string.IsNullOrEmpty(continuationToken))
            {
                break;
            }
        }

        if (currentBatch.Count > 0)
        {
            result.Records.AddRange(currentBatch);
        }

        result.NextCursor = string.IsNullOrEmpty(continuationToken) ? null : continuationToken;

        return result;
    }

    public async Task<ScrapeResult> ScrapeAsync(
        ObjectStorageDecryptedDto objectStorage,
        string? afterCursor,
        int batchSize,
        int maxBatches,
        CancellationToken cancellationToken = default)
    {
        return await ScrapeAzureBlob(
            objectStorage.Config.AzureObjectConfig
                ?? throw new InvalidOperationException("Azure Blob storage is missing its configuration."),
            objectStorage.Id,
            afterCursor,
            batchSize,
            maxBatches,
            cancellationToken);
    }
}

/// <summary>
///     A semaphore that gates access by byte budget rather than slot count.
///     Allows concurrent acquires as long as total bytes in flight stays under the budget.
///     If a single acquire exceeds the budget but nothing is in flight, it proceeds anyway
///     to prevent deadlock when one part is larger than the entire budget.
/// </summary>
internal sealed class WeightedSemaphore
{
    private readonly long _maxBytes;
    private long _currentBytes;
    private readonly object _lock = new();
    private readonly Queue<(long Bytes, TaskCompletionSource Tcs)> _waiters = new();

    public WeightedSemaphore(long maxBytes) => _maxBytes = maxBytes;

    public Task AcquireAsync(long bytes, CancellationToken ct)
    {
        lock (_lock)
        {
            // Grant immediately if under budget or nothing else is in flight
            if (_currentBytes == 0 || _currentBytes + bytes <= _maxBytes)
            {
                _currentBytes += bytes;
                return Task.CompletedTask;
            }

            // Budget exhausted — queue a waiter and return its task
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Enqueue((bytes, tcs));

            // If the caller cancels, abandon the waiter without adjusting budget
            // (budget was never granted to it)
            ct.Register(() => tcs.TrySetCanceled(ct));

            return tcs.Task;
        }
    }

    public void Release(long bytes)
    {
        lock (_lock)
        {
            _currentBytes = Math.Max(0, _currentBytes - bytes);
            TryGrantWaiters();
        }
    }

    private void TryGrantWaiters()
    {
        while (_waiters.Count > 0)
        {
            var waiter = _waiters.Peek();

            // Discard waiters cancelled while waiting — no budget adjustment needed
            // since they never received budget in the first place
            if (waiter.Tcs.Task.IsCanceled)
            {
                _waiters.Dequeue();
                continue;
            }

            if (_currentBytes == 0 || _currentBytes + waiter.Bytes <= _maxBytes)
            {
                _waiters.Dequeue();
                _currentBytes += waiter.Bytes;
                waiter.Tcs.TrySetResult();
                // Keep trying — freed budget may accommodate additional waiters
            }
            else
            {
                break; // Next waiter needs more than available budget
            }
        }
    }
}