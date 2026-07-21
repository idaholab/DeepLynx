namespace deeplynx.blobhash.functions;

public sealed class NexusTokenRequest
{
    public string ApiKey { get; init; } = null!;
    public string ApiSecret { get; init; } = null!;
    public double? ExpirationMinutes { get; init; }
}

public sealed class NexusBlobHashCallbackRequest
{
    public string ObjectStorageType { get; init; } = "azure_object";
    public string ContainerName { get; init; } = null!;
    public string BlobName { get; init; } = null!;
    public string HashAlgorithm { get; init; } = "SHA-256";
    public string HashHex { get; init; } = null!;
    public long? ContentLength { get; init; }
}
