using Asp.Versioning;

namespace deeplynx.api;

internal static class NexusApiVersions
{
    public static ApiVersion Default { get; } = new(1);

    public static IReadOnlyList<ApiVersion> Supported { get; } =
    [
        new(1),
        new(2)
    ];

    public static string DefaultOpenApiDocumentName => "v1";

    public static IReadOnlyList<string> OpenApiDocumentNames { get; } =
    [
        "v1",
        "v2"
    ];
}
