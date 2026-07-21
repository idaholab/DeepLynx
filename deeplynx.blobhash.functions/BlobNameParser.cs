using System.Text.RegularExpressions;

namespace deeplynx.blobhash.functions;

public sealed record ParsedBlobName(long OrganizationId, long ProjectId, string BlobName);

public sealed class BlobNameParser
{
    private static readonly Regex FinalBlobNamePattern = new(
        "^organization_(?<organizationId>\\d+)/projects?_(?<projectId>\\d+)/datasource_\\d+/(?<fileName>.+)$",
        RegexOptions.Compiled);

    public ParsedBlobName? ParseFinalBlobName(string blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
            return null;

        if (blobName.Contains("/uploads/", StringComparison.OrdinalIgnoreCase))
            return null;

        var match = FinalBlobNamePattern.Match(blobName);
        if (!match.Success)
            return null;

        return new ParsedBlobName(
            long.Parse(match.Groups["organizationId"].Value),
            long.Parse(match.Groups["projectId"].Value),
            blobName);
    }
}
