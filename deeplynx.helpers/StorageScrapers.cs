using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using deeplynx.models;
using System.Text.Json.Nodes;

namespace deeplynx.helpers;

public static class StorageScrapers
{
    /// <summary>
    /// Scrapes a file system object storage.
    /// </summary>
    /// <param name="mountPath">Root path to scan (Config.MountPath)</param>
    /// <param name="objectStorageId">The ID of the object storage being scraped</param>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public static List<CreateRecordRequestDto> ScrapeFileSystem(string mountPath, long objectStorageId)
    {
        if (!Directory.Exists(mountPath))
            throw new DirectoryNotFoundException($"Mount path '{mountPath}' does not exist.");

        var records = new List<CreateRecordRequestDto>();

        foreach (var filePath in Directory.EnumerateFiles(mountPath, "*", SearchOption.AllDirectories))
        {
            var fileInfo = new FileInfo(filePath);
            var relativePath = Path.GetRelativePath(mountPath, filePath);

            var properties = new JsonObject
            {
                ["lastModified"] = fileInfo.LastWriteTimeUtc.ToString("o"),
                ["fullPath"] = filePath
            };

            records.Add(new CreateRecordRequestDto
            {
                Name = fileInfo.Name,
                // TODO: no natural source for Description on raw files; defaulting to relative path
                Description = relativePath,
                ObjectStorageId = objectStorageId,
                Uri = relativePath,
                Properties = properties,
                // TODO: relative path used as OriginalId; confirm this is stable/unique enough
                // (e.g. won't collide across re-scrapes, renames, etc.)
                OriginalId = relativePath,
                FileType = string.IsNullOrEmpty(fileInfo.Extension) ? null : fileInfo.Extension.TrimStart('.'),
                FileSize = fileInfo.Length
            });
        }

        return records;
    }

    /// <summary>
    /// Scrapes an Azure Blob object storage.
    /// </summary>
    /// <param name="config">Config.AzureObjectConfig</param>
    /// <param name="objectStorageId">The ID of the object storage being scraped</param>
    public static async Task<List<CreateRecordRequestDto>> ScrapeAzureBlob(AzureObjectConfigDto config, long objectStorageId)
    {
        if (string.IsNullOrWhiteSpace(config.AzureConnectionString))
            throw new InvalidOperationException("AzureObjectConfig is missing a connection string.");
        if (string.IsNullOrWhiteSpace(config.AzureContainerName))
            throw new InvalidOperationException("AzureObjectConfig is missing a container name.");

        var containerClient = new BlobContainerClient(config.AzureConnectionString, config.AzureContainerName);

        var records = new List<CreateRecordRequestDto>();

        await foreach (var blobItem in containerClient.GetBlobsAsync())
        {
            var properties = new JsonObject
            {
                ["lastModified"] = blobItem.Properties.LastModified?.ToString("o"),
                ["contentType"] = blobItem.Properties.ContentType,
                ["etag"] = blobItem.Properties.ETag?.ToString()
            };

            var extension = Path.GetExtension(blobItem.Name);

            records.Add(new CreateRecordRequestDto
            {
                Name = Path.GetFileName(blobItem.Name),
                // TODO: no natural source for Description; defaulting to blob name (full virtual path)
                Description = blobItem.Name,
                ObjectStorageId = objectStorageId,
                Uri = blobItem.Name,
                Properties = properties,
                // TODO: blob name used as OriginalId; confirm this is stable/unique enough
                OriginalId = blobItem.Name,
                FileType = string.IsNullOrEmpty(extension) ? null : extension.TrimStart('.'),
                FileSize = blobItem.Properties.ContentLength ?? 0
            });
        }

        return records;
    }

    /// <summary>
    /// Scrapes an S3 object storage.
    /// </summary>
    /// <param name="awsConnectionString">Config.AwsConnectionString, e.g. s3://bucket/prefix?region=...&accessKey=...&secretKey=...</param>
    /// <param name="objectStorageId">The ID of the object storage being scraped</param>
    public static async Task<List<CreateRecordRequestDto>> ScrapeS3(string awsConnectionString, long objectStorageId)
    {
        var connection = S3ConnectionInfo.Parse(awsConnectionString);
        var region = Amazon.RegionEndpoint.GetBySystemName(connection.Region);

        using var s3Client = new AmazonS3Client(connection.AccessKey, connection.SecretKey, region);

        var records = new List<CreateRecordRequestDto>();
        string? continuationToken = null;

        do
        {
            var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = connection.BucketName,
                Prefix = connection.Prefix,
                ContinuationToken = continuationToken
            });

            foreach (var s3Object in response.S3Objects)
            {
                if (s3Object.Key.EndsWith('/') && s3Object.Size == 0)
                    continue;

                var properties = new JsonObject
                {
                    ["lastModified"] = s3Object.LastModified?.ToString("o"),
                    ["etag"] = s3Object.ETag,
                    ["storageClass"] = s3Object.StorageClass?.Value
                };

                var extension = Path.GetExtension(s3Object.Key);

                records.Add(new CreateRecordRequestDto
                {
                    Name = Path.GetFileName(s3Object.Key),
                    // TODO: no natural source for Description; defaulting to S3 key
                    Description = s3Object.Key,
                    ObjectStorageId = objectStorageId,
                    Uri = s3Object.Key,
                    Properties = properties,
                    // TODO: S3 key used as OriginalId; confirm this is stable/unique enough
                    OriginalId = s3Object.Key,
                    FileType = string.IsNullOrEmpty(extension) ? null : extension.TrimStart('.'),
                    FileSize = s3Object.Size ?? 0
                });
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuationToken != null);

        return records;
    }
}


public class S3ConnectionInfo
{
    public string BucketName { get; set; } = null!;
    public string? Prefix { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string AccessKey { get; set; } = null!;
    public string SecretKey { get; set; } = null!;

    /// <summary>
    /// Parses a string like: s3://test-bucket/path?region=us-west-2&accessKey=xxx&secretKey=xxx
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
            throw new InvalidOperationException("The configured AWS connection string is not a valid S3 URI.", ex);
        }

        if (!string.Equals(uri.Scheme, "s3", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Expected an 's3://' URI but got scheme '{uri.Scheme}'.");

        var bucketName = uri.Host;
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new InvalidOperationException("AwsConnectionString is missing a bucket name.");

        var prefix = uri.AbsolutePath.Trim('/');

        var queryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var query = uri.Query.TrimStart('?');
        if (!string.IsNullOrEmpty(query))
        {
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(parts[0]);
                var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                queryParams[key] = value;
            }
        }

        if (!queryParams.TryGetValue("accessKey", out var accessKey) || string.IsNullOrWhiteSpace(accessKey))
            throw new InvalidOperationException("AwsConnectionString is missing 'accessKey'.");

        if (!queryParams.TryGetValue("secretKey", out var secretKey) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("AwsConnectionString is missing 'secretKey'.");

        queryParams.TryGetValue("region", out var region);

        return new S3ConnectionInfo
        {
            BucketName = bucketName,
            Prefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix,
            Region = string.IsNullOrWhiteSpace(region) ? "us-east-1" : region,
            AccessKey = accessKey,
            SecretKey = secretKey
        };
    }
}