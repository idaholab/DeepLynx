using System.Security.Cryptography;

namespace deeplynx.helpers;

public static class Sha256HashHelper
{
    public static async Task<string> ComputeHexAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}