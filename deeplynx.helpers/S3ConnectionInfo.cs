namespace deeplynx.helpers;

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