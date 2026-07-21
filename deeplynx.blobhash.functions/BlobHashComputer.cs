using System.Security.Cryptography;

namespace deeplynx.blobhash.functions;

public sealed class BlobHashComputer
{
    public async Task<string> ComputeSha256HexAsync(Stream blobStream, CancellationToken cancellationToken)
    {
        if (blobStream.CanSeek)
            blobStream.Position = 0;

        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(blobStream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
