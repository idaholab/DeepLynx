using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace deeplynx.blobhash.functions;

public sealed class BlobHashFunction
{
    private readonly BlobHashComputer _hashComputer;
    private readonly ILogger<BlobHashFunction> _logger;
    private readonly NexusBlobHashClient _nexusClient;
    private readonly BlobNameParser _parser;
    private readonly BlobHashSettings _settings;

    public BlobHashFunction(
        BlobHashSettings settings,
        BlobNameParser parser,
        BlobHashComputer hashComputer,
        NexusBlobHashClient nexusClient,
        ILogger<BlobHashFunction> logger)
    {
        _settings = settings;
        _parser = parser;
        _hashComputer = hashComputer;
        _nexusClient = nexusClient;
        _logger = logger;
    }

    [Function("HashAzureBlob")]
    public async Task Run(
        [BlobTrigger("%BLOB_HASH_CONTAINER%/{name}", Connection = "AzureWebJobsStorage")]
        Stream blobStream,
        string name,
        CancellationToken cancellationToken)
    {
        var parsedBlob = _parser.ParseFinalBlobName(name);
        if (parsedBlob == null)
        {
            _logger.LogInformation("Skipping blob hash for non-final Nexus blob path {BlobName}.", name);
            return;
        }

        var hashHex = await _hashComputer.ComputeSha256HexAsync(blobStream, cancellationToken);

        var callback = new NexusBlobHashCallbackRequest
        {
            ContainerName = _settings.BlobContainerName,
            BlobName = parsedBlob.BlobName,
            HashHex = hashHex,
            ContentLength = blobStream.CanSeek ? blobStream.Length : null
        };

        await _nexusClient.SendBlobHashAsync(
            parsedBlob.OrganizationId,
            parsedBlob.ProjectId,
            callback,
            cancellationToken);
    }
}
