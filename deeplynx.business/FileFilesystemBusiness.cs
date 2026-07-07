using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.IO.Pipelines;
using System.Threading.Channels;

namespace deeplynx.business;

public class FileFilesystemBusiness : IFileBusiness
{
    private readonly IClassBusiness _classBusiness;
    private readonly DeeplynxContext _context;
    private readonly IObjectStorageBusiness _objectStorageBusiness;
    private readonly IRecordBusiness _recordBusiness;

    public FileFilesystemBusiness(
        DeeplynxContext context,
        IObjectStorageBusiness objectStorageBusiness,
        IClassBusiness classBusiness,
        IRecordBusiness recordBusiness)
    {
        _context = context;
        _objectStorageBusiness = objectStorageBusiness;
        _classBusiness = classBusiness;
        _recordBusiness = recordBusiness;
    }

    /// <summary>
    ///     Uploads a file to the local file system
    /// </summary>
    /// <param name="organizationId">The organization the file is associated with</param>
    /// <param name="projectId">The project the file is associated with</param>
    /// <param name="dataSourceId">The data source the file is associated with</param>
    /// <param name="objectStorageConfig">The config containing the file path</param>
    /// <param name="file">The file the user wants to upload</param>
    /// <param name="guid">The unique identifier for file names</param>
    public async Task<string> UploadFile(
        long organizationId,
        long projectId,
        long dataSourceId,
        ObjectStorageConfigDto objectStorageConfig,
        IFormFile file,
        Guid guid
    )
    {
        // TODO: Cache these
        if (objectStorageConfig.MountPath == null)
            throw new Exception("File system mount path not set in object storage");

        var fileName = $"{guid}_{file.FileName}";

        // create a file path in the format <mountdir>/project_<id>/datasource_<id>/filename
        var filePath = Path.Combine(
            objectStorageConfig.MountPath,
            "org_" + organizationId,
            "project_" + projectId,
            "datasource_" + dataSourceId,
            fileName);
        // create the directory for the file if not exists
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ??
                                  throw new InvalidOperationException("error creating upload path."));

        // copy the file to its new location
        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return filePath;
    }

    /// <summary>
    ///     Replaces local file
    /// </summary>
    /// <param name="record">The record the file info is in</param>
    /// <param name="file">The file that the user wants to change the old file to</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public async Task<string> UpdateFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig, IFormFile file, Guid guid)
    {
        var filePath = record.Uri;

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is not specified in the record.");

        if (!File.Exists(filePath)) throw new FileNotFoundException("The file to update does not exist.", filePath);

        var directory = Path.GetDirectoryName(filePath);

        if (directory == null || !Directory.Exists(directory))
            throw new DirectoryNotFoundException("Directory not found.");

        var newFileName = $"{guid}_{file.FileName}";

        var updatedPath = Path.Combine(directory, newFileName);

        // Delete the original file
        File.Delete(filePath);

        //write new file
        await using (var stream = new FileStream(updatedPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return updatedPath;
    }

    /// <summary>
    ///     Downloads a file from local file storage
    /// </summary>
    /// <param name="record">The record that has the file info</param>
    /// <param name="objectStorageConfig">The configuration data of the object storage</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<FileStreamResult> DownloadFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig)
    {
        var filePath = record.Uri;
        if (filePath == null) throw new ArgumentNullException("File path/uri is not specified in the record.");
        var fileName = record.Name;

        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path must be provided.");

        if (!File.Exists(filePath)) throw new FileNotFoundException("The requested file does not exist.", filePath);

        // Get file info for size
        var fileInfo = new FileInfo(filePath);
        var contentLength = fileInfo.Length;

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Detect file type
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(filePath, out var contentType))
            contentType = "application/octet-stream"; // Default fallback

        return new FileStreamResultWithLength(stream, contentType, contentLength)
        {
            FileDownloadName = fileName,
            EnableRangeProcessing = true
        };
    }

    /// <summary>
    ///     Deletes a file from local file storage
    /// </summary>
    /// <param name="record">Record that contains file info</param>
    /// <param name="objectStorageConfig">Contains the config info</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<bool> DeleteFile(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig)
    {
        var filePath = record.Uri;
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is not specified in the record.");

        if (!File.Exists(filePath)) throw new FileNotFoundException("The file to update does not exist.", filePath);

        File.Delete(filePath);

        var directory = Path.GetDirectoryName(filePath);

        if (objectStorageConfig.MountPath == null)
            throw new Exception("File system mount path not set in object storage");

        // Normalize paths for comparison
        var normalizedBasePath = Path.GetFullPath(objectStorageConfig.MountPath).TrimEnd(Path.DirectorySeparatorChar);

        // deletes all empty directories up to but not including the base path
        while (!string.IsNullOrEmpty(directory) &&
               Directory.Exists(directory) &&
               !Path.GetFullPath(directory).Equals(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
            if (Directory.GetFileSystemEntries(directory).Length == 0)
            {
                Directory.Delete(directory);
                directory = Path.GetDirectoryName(directory);
            }
            else
            {
                break;
            }

        return true;
    }

    /// <summary>
    /// Downloads a file from Azure Object Storage
    /// </summary>
    /// <param name="record"></param>
    /// <param name="objectStorageConfig"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<FileStreamResult> DownloadAppendedFile(
        RecordResponseDto record,
        ObjectStorageConfigDto objectStorageConfig,
        CancellationToken cancellationToken = default)
    {
        const long MaxBufferedFileSize = 10 * 1024 * 1024; // 10 MB

        if (record.Uri == null)
            throw new ArgumentException("Record Uri is null");
        if (string.IsNullOrWhiteSpace(objectStorageConfig?.MountPath))
            throw new ArgumentException("Mounted path configuration is missing");

        var fullPath = record.Uri.StartsWith("/")
            ? record.Uri
            : "/" + record.Uri;

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Directory '{fullPath}' not found.");

        string lastFolderName = Path.GetFileName(record.Uri.TrimEnd('/', '\\'));
        string zipFileName = lastFolderName;
        int underscoreIndex = lastFolderName.LastIndexOf('_');
        if (underscoreIndex > 0)
        {
            zipFileName = lastFolderName.Substring(0, underscoreIndex);
        }
        zipFileName += ".zip";

        var pipe = new Pipe();

        _ = Task.Run(async () =>
        {
            Exception? error = null;
            try
            {
                await using var pipeStream = pipe.Writer.AsStream(leaveOpen: true);
                using var archive = new ZipArchive(pipeStream, ZipArchiveMode.Create, leaveOpen: true);

                // Bounded channel for producer-consumer coordination.
                // Items carry EITHER buffered content (small files) OR just a
                // file path (large files, streamed by the consumer at write time).
                // Worst-case buffered memory ~= capacity * MaxBufferedFileSize.
                var channel = Channel.CreateBounded<(string EntryName, byte[]? Content, string? FilePath)>(
                    new BoundedChannelOptions(128)
                    {
                        FullMode = BoundedChannelFullMode.Wait,
                        SingleReader = true,
                        SingleWriter = false
                    });

                // Producer: concurrently read small files into memory; enqueue
                // large files as path-only items so they are never fully buffered.
                var producer = Task.Run(async () =>
                {
                    try
                    {
                        await Parallel.ForEachAsync(
                            Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories),
                            new ParallelOptions
                            {
                                MaxDegreeOfParallelism = 32,
                                CancellationToken = cancellationToken
                            },
                            async (filePath, ct) =>
                            {
                                var entryName = Path.GetRelativePath(fullPath, filePath).Replace('\\', '/');
                                var length = new FileInfo(filePath).Length;

                                if (length <= MaxBufferedFileSize)
                                {
                                    // Small file: buffer raw bytes. Note: no
                                    // pre-deflating here — the ZipArchive entry
                                    // stream handles compression on write, so
                                    // pre-compressing would double-deflate.
                                    var bytes = await File.ReadAllBytesAsync(filePath, ct);
                                    await channel.Writer.WriteAsync((entryName, bytes, null), ct);
                                }
                                else
                                {
                                    // Large file: defer the read to the consumer.
                                    await channel.Writer.WriteAsync((entryName, null, filePath), ct);
                                }
                            });

                        channel.Writer.Complete();
                    }
                    catch (Exception ex)
                    {
                        channel.Writer.Complete(ex);
                        throw;
                    }
                }, cancellationToken);

                // Consumer: single sequential writer to the ZipArchive.
                var consumer = Task.Run(async () =>
                {
                    await foreach (var (entryName, content, filePath) in channel.Reader.ReadAllAsync(cancellationToken))
                    {
                        var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
                        await using var entryStream = entry.Open();

                        if (content is not null)
                        {
                            await entryStream.WriteAsync(content, cancellationToken);
                        }
                        else
                        {
                            // Stream the large file straight from disk into the
                            // zip entry — never fully materialized in memory.
                            await using var fileStream = new FileStream(
                                filePath!,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.Read,
                                bufferSize: 512 * 1024,
                                useAsync: true);

                            await fileStream.CopyToAsync(entryStream, cancellationToken);
                        }
                    }
                }, cancellationToken);

                // Await both producer and consumer
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


    public async Task<string> GenerateDownloadUrl(RecordResponseDto record, ObjectStorageConfigDto objectStorageConfig,
        int expirationHours = 1)
    {
        throw new NotImplementedException("Generate download urls is not implemented for filesystem");
    }

    /// <summary>
    /// Sets up the file path before the chunks are uploaded there
    /// </summary>
    /// <param name="objectStorageConfig">Config allowing chunking to be set up and tested</param>
    public async Task<Guid> StartUpload(long organizationId, long projectId, long datasourceId, ObjectStorageConfigDto objectStorageConfig)
    {
        var uploadId = Guid.NewGuid();

        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            uploadId.ToString()
        );
        Directory.CreateDirectory(uploadPath);

        return uploadId;
    }

    public async Task UploadChunk(long organizationId, long projectId, long datasourceId, long chunkNumber, string uploadId,
        ObjectStorageConfigDto objectStorageConfig, IFormFile chunk)
    {
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        // Use mount path from object storage config
        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            uploadId
        );
        var chunkFilePath = Path.Combine(uploadPath, $"{chunkNumber}.part");

        try
        {
            if (chunk == null || chunk.Length == 0)
                throw new ArgumentException("No chunk data provided");

            if (!Directory.Exists(uploadPath))
                throw new InvalidOperationException($"Upload session {uploadId} not found or expired");

            // Write chunk to disk
            await using var stream = new FileStream(chunkFilePath, FileMode.Create);
            await chunk.CopyToAsync(stream);
        }
        catch (Exception)
        {
            // Cleanup chunk file on failure
            if (File.Exists(chunkFilePath))
                File.Delete(chunkFilePath);

            throw;
        }
    }

    public async Task<string> CompleteUpload(long organizationId, long projectId, long datasourceId, ObjectStorageConfigDto objectStorageConfig, FileUploadCompleteRequestDto request, Guid guid)
    {
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        // Use mount path from object storage config
        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            request.UploadId
        );
        var mergedFileName = $"{request.UploadId}_{request.FileName}";
        var mergedFilePath = Path.Combine(uploadPath, mergedFileName);

        try
        {
            if (!Directory.Exists(uploadPath))
                throw new InvalidOperationException($"Upload session {request.UploadId} not found");

            // Merge all chunks into final file
            await using (var finalFileStream = new FileStream(mergedFilePath, FileMode.Create))
            {
                for (var i = 0; i < request.TotalChunks; i++)
                {
                    var chunkFilePath = Path.Combine(uploadPath, $"{i}.part");

                    if (!File.Exists(chunkFilePath))
                        throw new InvalidOperationException($"Missing chunk {i} of {request.TotalChunks}");

                    await using (var chunkStream = new FileStream(chunkFilePath, FileMode.Open))
                    {
                        await chunkStream.CopyToAsync(finalFileStream);
                    }

                    File.Delete(chunkFilePath); // Clean up chunk after merging
                }
            }


            // Create IFormFile from merged file for upload
            await using var fileStream = new FileStream(mergedFilePath, FileMode.Open, FileAccess.Read);
            var formFile = new FormFile(fileStream, 0, fileStream.Length, "file", request.FileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };

            var uri = await UploadFile(organizationId, projectId, datasourceId, objectStorageConfig, formFile,
                guid);

            // Clean up merged file and upload directory
            fileStream.Close();
            File.Delete(mergedFilePath);
            Directory.Delete(uploadPath, true);

            return uri;
        }
        catch
        {
            if (File.Exists(mergedFilePath))
                File.Delete(mergedFilePath);

            if (Directory.Exists(uploadPath))
                Directory.Delete(uploadPath, true);

            throw;
        }
    }

    public async Task CancelUpload(long organizationId, long projectId, long dataSourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{dataSourceId}",
            "uploads",
            uploadId
        );

        if (Directory.Exists(uploadPath))
            Directory.Delete(uploadPath, true);
    }

    /// <summary>
    /// Gets the total storage size in bytes for files matching the given prefix in the filesystem.
    /// </summary>
    /// <param name="prefix">The directory prefix to search (e.g., "org_1/project_2/")</param>
    /// <param name="objectStorageConfig">Filesystem storage configuration</param>
    /// <returns>Total bytes used by files in directories matching the prefix</returns>
    public async Task<long> GetStorageSize(string prefix, ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.MountPath == null)
            return 0;

        var directoryPath = string.IsNullOrEmpty(prefix)
            ? objectStorageConfig.MountPath
            : Path.Combine(objectStorageConfig.MountPath, prefix.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(directoryPath))
            return 0;

        long totalSize = 0;

        // EnumerateFiles is more efficient than GetFiles for large directories
        var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                totalSize += fileInfo.Length;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get size for file {file}: {ex.Message}");
            }
        }

        return totalSize;
    }

    /// <summary>
    /// Builds the filesystem-specific path prefix.
    /// Filesystem uses the format: org_{id}/project_{id}/
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="projectId">Optional project ID</param>
    /// <returns>Filesystem path prefix string</returns>
    public string BuildPrefix(long organizationId, long? projectId)
    {
        // Filesystem format: org_1/project_2/
        if (projectId.HasValue)
            return $"org_{organizationId}/project_{projectId.Value}/";
        else
            return $"org_{organizationId}/";
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
        // Kept for IFileBusiness interface compatability.
        // Filesystem records store the full file path in the uri.
        _ = objectStorageConfig;

        if (string.IsNullOrWhiteSpace(fileUri))
            throw new ArgumentException("File URI is not specified.");

        try
        {
            var fileInfo = new FileInfo(fileUri);
            return fileInfo.Length;
        }
        catch (FileNotFoundException ex)
        {
            throw new FileNotFoundException($"File {fileUri} not found", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get size for file {fileUri}: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates an upload in the storage space with a filelength var for the tus protocol.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="datasourceId"></param>
    /// <param name="objectStorageConfig"></param>
    /// <param name="uploadLength"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<Guid> CreateUploadTus(long organizationId, long projectId, long datasourceId,
        ObjectStorageConfigDto objectStorageConfig, long uploadLength, string fileName)
    {
        var uploadId = Guid.NewGuid();

        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            uploadId.ToString()
        );
        Directory.CreateDirectory(uploadPath);

        var metaPath = Path.Combine(uploadPath, "meta.json");
        await File.WriteAllTextAsync(metaPath, JsonConvert.SerializeObject(new { UploadLength = uploadLength, FileName = fileName }));

        return uploadId;
    }


    /// <summary>
    /// Gets the current upload offset/upload-progress for the tus protocol. 
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="datasourceId"></param>
    /// <param name="uploadId"></param>
    /// <param name="objectStorageConfig"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<long> GetUploadOffset(long organizationId, long projectId, long datasourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            uploadId
        );

        if (!Directory.Exists(uploadPath))
            throw new InvalidOperationException($"Upload session {uploadId} not found or expired");

        var filePath = Path.Combine(uploadPath, "data");

        if (!File.Exists(filePath))
            return 0;

        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length;
    }

    /// <summary>
    /// Gets the total declared length for the upload for the tus protocol.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="datasourceId"></param>
    /// <param name="uploadId"></param>
    /// <param name="objectStorageConfig"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<long> GetUploadLength(long organizationId, long projectId, long datasourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            uploadId
        );

        if (!Directory.Exists(uploadPath))
            throw new InvalidOperationException($"Upload session {uploadId} not found or expired");

        var metaPath = Path.Combine(uploadPath, "meta.json");

        if (!File.Exists(metaPath))
            throw new InvalidOperationException($"Metadata for upload session {uploadId} not found");

        var meta = JsonConvert.DeserializeObject<dynamic>(await File.ReadAllTextAsync(metaPath));
        return (long)meta.UploadLength;
    }

    /// <summary>
    /// Uploads a part for the tus protocol using a byte filestream, for the tus protocol.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="datasourceId"></param>
    /// <param name="uploadId"></param>
    /// <param name="uploadOffset"></param>
    /// <param name="objectStorageConfig"></param>
    /// <param name="uploadBody"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<long> UploadPartTus(long organizationId, long projectId, long datasourceId, string uploadId,
        long uploadOffset, ObjectStorageConfigDto objectStorageConfig, System.IO.Stream uploadBody)
    {
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            uploadId
        );

        if (!Directory.Exists(uploadPath))
            throw new InvalidOperationException($"Upload session {uploadId} not found or expired");

        var filePath = Path.Combine(uploadPath, "data");

        await using var stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        stream.Seek(uploadOffset, SeekOrigin.Begin);
        await uploadBody.CopyToAsync(stream);

        return stream.Position;
    }

    public async Task<string> CompleteUploadTus(long organizationId, long projectId, long datasourceId,
        ObjectStorageConfigDto objectStorageConfig, string uploadId, Guid guid, string fileName)
    {
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            uploadId
        );

        if (!Directory.Exists(uploadPath))
            throw new InvalidOperationException($"Upload session {uploadId} not found");

        var tusFilePath = Path.Combine(uploadPath, "data");

        if (!File.Exists(tusFilePath))
            throw new InvalidOperationException($"TUS upload file not found for upload {uploadId}");

        try
        {
            await using var fileStream = new FileStream(tusFilePath, FileMode.Open, FileAccess.Read);

            var formFile = new FormFile(fileStream, 0, fileStream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };

            var uri = await UploadFile(
                organizationId,
                projectId,
                datasourceId,
                objectStorageConfig,
                formFile,
                guid
            );

            fileStream.Close();

            Directory.Delete(uploadPath, true);

            return uri;
        }
        catch
        {
            if (Directory.Exists(uploadPath))
                Directory.Delete(uploadPath, true);

            throw;
        }
    }

    public async Task<string> GetFileNameTus(long organizationId, long projectId, long datasourceId, string uploadId,
        ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.MountPath == null)
            throw new InvalidOperationException("File system mount path not set in object storage");

        var uploadPath = Path.Combine(
            objectStorageConfig.MountPath,
            $"org_{organizationId}",
            $"project_{projectId}",
            $"datasource_{datasourceId}",
            "uploads",
            uploadId
        );

        if (!Directory.Exists(uploadPath))
            throw new InvalidOperationException($"Upload session {uploadId} not found or expired");

        var metaPath = Path.Combine(uploadPath, "meta.json");

        if (!File.Exists(metaPath))
            throw new InvalidOperationException($"Metadata for upload session {uploadId} not found");

        var meta = JsonConvert.DeserializeObject<dynamic>(await File.ReadAllTextAsync(metaPath));
        return (string)meta.FileName;
    }

}