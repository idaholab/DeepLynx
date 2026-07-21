namespace deeplynx.blobhash.functions;

public sealed class BlobHashSettings
{
    public string BlobContainerName { get; private init; } = null!;
    public string NexusBaseUrl { get; private init; } = null!;
    public string NexusApiKey { get; private init; } = null!;
    public string NexusApiSecret { get; private init; } = null!;
    public int NexusTokenExpirationMinutes { get; private init; }
    public int CallbackMaxAttempts { get; private init; }
    public TimeSpan CallbackBaseDelay { get; private init; }

    public static BlobHashSettings FromEnvironment()
    {
        return new BlobHashSettings
        {
            BlobContainerName = Required("BLOB_HASH_CONTAINER"),
            NexusBaseUrl = Required("NEXUS_BASE_URL").TrimEnd('/'),
            NexusApiKey = Required("NEXUS_API_KEY"),
            NexusApiSecret = Required("NEXUS_API_SECRET"),
            NexusTokenExpirationMinutes = PositiveInt("NEXUS_TOKEN_EXPIRATION_MINUTES", 55),
            CallbackMaxAttempts = PositiveInt("NEXUS_CALLBACK_MAX_ATTEMPTS", 5),
            CallbackBaseDelay = TimeSpan.FromSeconds(PositiveInt("NEXUS_CALLBACK_BASE_DELAY_SECONDS", 2))
        };
    }

    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{name} environment variable is required.");

        return value;
    }

    private static int PositiveInt(string name, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new InvalidOperationException($"{name} must be a positive integer.");
    }
}
