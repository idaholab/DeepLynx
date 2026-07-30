using Azure.Storage.Blobs;
using Amazon.S3;
using Amazon.S3.Model;
using deeplynx.models;
using System.Text.Json.Nodes;
using System.Runtime.CompilerServices;

namespace deeplynx.helpers;

// =====================================================================
// Result of one bounded scrape call: at most (batchSize * maxBatches) records,
// plus a cursor to resume from and a flag indicating whether the whole
// storage has been fully scraped.
// =====================================================================
public class ScrapeResult
{
    public List<CreateRecordRequestDto> Records { get; set; } = new();

    /// <summary>
    /// Opaque cursor to pass back in on the next call to resume where this one left off.
    /// Null when the entire storage has been fully scraped — matches the BackfillFileSizes
    /// convention where a null lastRecordId signals the client to stop looping.
    /// </summary>
    public string? NextCursor { get; set; }
}

// =====================================================================
// Parsed shape of an "s3://bucket/prefix?region=...&accessKey=...&secretKey=..." string
// =====================================================================
public class S3ConnectionInfo
{
    public string BucketName { get; set; } = null!;
    public string? Prefix { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string AccessKey { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Parses a string like: s3://test-bucket/path?region=us-west-2&accessKey=xxx&secretKey=xxx&serviceUrl=http%3A%2F%2Fhost.docker.internal%3A9100
    /// </summary>
    public static S3ConnectionInfo Parse(string awsConnectionString)
    {
        Uri uri;

        try
        {
            uri = new Uri(awsConnectionString);
        }
        catch (UriFormatException ex)
        {
            // Avoid logging the entire connection string because it contains credentials.
            throw new InvalidOperationException(
                "AwsConnectionString is not a valid URI.",
                ex);
        }

        if (!string.Equals(
                uri.Scheme,
                "s3",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Expected an 's3://' URI but got scheme '{uri.Scheme}'.");
        }

        var bucketName = uri.Host;

        if (string.IsNullOrWhiteSpace(bucketName))
        {
            throw new InvalidOperationException(
                "AwsConnectionString is missing a bucket name.");
        }

        var prefix = uri.AbsolutePath.Trim('/');

        var queryParams = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        var query = uri.Query.TrimStart('?');

        if (!string.IsNullOrEmpty(query))
        {
            foreach (var pair in query.Split(
                         '&',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);

                var key = Uri.UnescapeDataString(parts[0]);

                var value = parts.Length > 1
                    ? Uri.UnescapeDataString(parts[1])
                    : string.Empty;

                queryParams[key] = value;
            }
        }

        if (!queryParams.TryGetValue(
                "accessKey",
                out var accessKey) ||
            string.IsNullOrWhiteSpace(accessKey))
        {
            throw new InvalidOperationException(
                "AwsConnectionString is missing 'accessKey'.");
        }

        if (!queryParams.TryGetValue(
                "secretKey",
                out var secretKey) ||
            string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "AwsConnectionString is missing 'secretKey'.");
        }

        queryParams.TryGetValue("region", out var region);
        queryParams.TryGetValue("serviceUrl", out var serviceUrl);

        if (!string.IsNullOrWhiteSpace(serviceUrl) &&
            !Uri.TryCreate(
                serviceUrl,
                UriKind.Absolute,
                out var parsedServiceUrl))
        {
            throw new InvalidOperationException(
                "AwsConnectionString contains an invalid 'serviceUrl'.");
        }

        return new S3ConnectionInfo
        {
            BucketName = bucketName,
            Prefix = string.IsNullOrWhiteSpace(prefix)
                ? null
                : prefix,
            Region = string.IsNullOrWhiteSpace(region)
                ? "us-east-1"
                : region,
            AccessKey = accessKey,
            SecretKey = secretKey,
            ServiceUrl = string.IsNullOrWhiteSpace(serviceUrl)
                ? null
                : serviceUrl.TrimEnd('/')
        };
    }
}

// =====================================================================
// Scrapers — each takes its relevant slice of ObjectStorageConfigDto,
// plus the objectStorageId now that CreateRecordRequestDto needs it directly.
// =====================================================================
public static class StorageScrapers
{
    /// <summary>
    /// Scrapes at most (batchSize * maxBatches) files from a file system storage, starting
    /// after the given cursor (a relative path from a previous call, or null to start from
    /// the beginning). Files are processed in a deterministic sort order (ordinal string sort
    /// on relative path) so that resuming from a cursor is reliable across calls.
    /// </summary>
    /// <param name="mountPath">Root path to scan (Config.MountPath)</param>
    /// <param name="objectStorageId">The ID of the object storage being scraped</param>
    /// <param name="cursor">Relative path to resume after, from a previous call, or null to start from the beginning</param>
    /// <param name="batchSize">Number of records per batch</param>
    /// <param name="maxBatches">Maximum number of batches to process before returning</param>
    /// <param name="cancellationToken">Token checked periodically during the directory walk</param>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public static Task<ScrapeResult> ScrapeFileSystem(
        string mountPath,
        long objectStorageId,
        string? cursor,
        int batchSize,
        int maxBatches,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(mountPath))
            throw new DirectoryNotFoundException($"Mount path '{mountPath}' does not exist.");

        var result = new ScrapeResult();
        var recordsWanted = (long)batchSize * maxBatches;

        // NOTE: Directory.EnumerateFiles does not guarantee any particular order across
        // platforms/filesystems, so we materialize and sort explicitly. For very large trees
        // this means a full directory walk happens on every call even though only a slice is
        // returned — there's no way around this without maintaining our own index/cache of
        // the tree, since the filesystem itself offers no continuation token like S3/Azure do.
        var allRelativePaths = Directory.EnumerateFiles(mountPath, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(mountPath, p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var startIndex = 0;
        if (!string.IsNullOrEmpty(cursor))
        {
            // Resume just after the last path we returned previously.
            var cursorIndex = allRelativePaths.BinarySearch(cursor, StringComparer.Ordinal);
            startIndex = cursorIndex >= 0 ? cursorIndex + 1 : ~cursorIndex;
        }

        var slice = allRelativePaths.Skip(startIndex).Take((int)Math.Min(recordsWanted, int.MaxValue)).ToList();

        foreach (var relativePath in slice)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullPath = Path.Combine(mountPath, relativePath);
            var fileInfo = new FileInfo(fullPath);

            var properties = new JsonObject
            {
                ["lastModified"] = fileInfo.LastWriteTimeUtc.ToString("o")
            };

            result.Records.Add(new CreateRecordRequestDto
            {
                Name = fileInfo.Name,
                Description = "File scraped from filesystem",
                ObjectStorageId = objectStorageId,
                Uri = fullPath,
                Properties = properties,
                OriginalId = fullPath,
                FileType = string.IsNullOrEmpty(fileInfo.Extension) ? null : fileInfo.Extension.TrimStart('.'),
                FileSize = fileInfo.Length
            });
        }

        var reachedEnd = startIndex + slice.Count >= allRelativePaths.Count;
        result.NextCursor = reachedEnd ? null : slice.LastOrDefault();

        return Task.FromResult(result);
    }

    /// <summary>
    /// Scrapes at most (batchSize * maxBatches) blobs from an Azure Blob storage, starting
    /// from the given cursor (an Azure continuation token from a previous call, or null to
    /// start from the beginning).
    /// </summary>
    /// <param name="config">Config.AzureObjectConfig</param>
    /// <param name="objectStorageId">The ID of the object storage being scraped</param>
    /// <param name="cursor">Continuation token from a previous call, or null to start from the beginning</param>
    /// <param name="batchSize">Number of records per batch</param>
    /// <param name="maxBatches">Maximum number of batches to process before returning</param>
    /// <param name="cancellationToken">Token checked between pages</param>
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

        // AsPages() (unlike the simpler GetBlobsAsync() enumeration) exposes the actual
        // continuation token per page, which we need to make this resumable across calls.
        var pageable = containerClient.GetBlobsAsync(cancellationToken: cancellationToken)
            .AsPages(continuationToken);

        await foreach (var page in pageable)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Always finish the current page in full, same reasoning as the S3 scraper:
            // Azure continuation tokens only resume at page boundaries.
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

    /// <summary>
    /// Scrapes at most (batchSize * maxBatches) objects from an S3 storage, starting
    /// from the given cursor (an S3 ContinuationToken from a previous call, or null to start
    /// from the beginning).
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
        var connection = S3ConnectionInfo.Parse(awsConnectionString);
        AmazonS3Config clientConfig;

        if (!string.IsNullOrWhiteSpace(connection.ServiceUrl))
        {
            clientConfig = new AmazonS3Config
            {
                ServiceURL = connection.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = connection.Region
            };
        }
        else
        {
            clientConfig = new AmazonS3Config
            {
                RegionEndpoint =
                    Amazon.RegionEndpoint.GetBySystemName(connection.Region)
            };
        }

        using var s3Client = new AmazonS3Client(
            connection.AccessKey,
            connection.SecretKey,
            clientConfig);

        var result = new ScrapeResult();
        var currentBatch = new List<CreateRecordRequestDto>(batchSize);
        var batchesCompleted = 0;
        string? continuationToken = cursor;
        var shouldStop = false;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await s3Client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = connection.BucketName,
                    Prefix = connection.Prefix,
                    ContinuationToken = continuationToken
                },
                cancellationToken);

            foreach (var s3Object in response.S3Objects ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(s3Object.Key))
                    continue;

                if (s3Object.Key.EndsWith("/", StringComparison.Ordinal) &&
                    s3Object.Size == 0)
                {
                    continue;
                }

                var properties = new JsonObject
                {
                    ["lastModified"] = s3Object.LastModified?.ToString("o"),
                    ["etag"] = s3Object.ETag,
                    ["storageClass"] = s3Object.StorageClass?.Value
                };

                var extension = Path.GetExtension(s3Object.Key);

                currentBatch.Add(new CreateRecordRequestDto
                {
                    Name = Path.GetFileName(s3Object.Key),
                    Description = s3Object.Key,
                    ObjectStorageId = objectStorageId,
                    Uri = s3Object.Key,
                    Properties = properties,
                    OriginalId = s3Object.Key,
                    FileType = string.IsNullOrEmpty(extension)
                        ? null
                        : extension.TrimStart('.'),
                    FileSize = s3Object.Size ?? 0
                });

                if (currentBatch.Count >= batchSize)
                {
                    result.Records.AddRange(currentBatch);
                    currentBatch = new List<CreateRecordRequestDto>(batchSize);
                    batchesCompleted++;
                }
            }

            continuationToken =
                response.IsTruncated == true &&
                !string.IsNullOrWhiteSpace(response.NextContinuationToken)
                    ? response.NextContinuationToken
                    : null;

            if (batchesCompleted >= maxBatches &&
                continuationToken is not null)
            {
                shouldStop = true;
            }
        }
        while (continuationToken is not null && !shouldStop);

        // Flush any remaining partial batch
        if (currentBatch.Count > 0)
        {
            result.Records.AddRange(currentBatch);
        }

        result.NextCursor = continuationToken;
        return result;
    }
}