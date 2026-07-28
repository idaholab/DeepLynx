using Asp.Versioning;

namespace deeplynx.api;

internal static class NexusApiVersions
{
    public static ApiVersion V1 { get; } = new(1);

    public static ApiVersion V2 { get; } = new(2);

    public static ApiVersion Default => V1;

    public static IReadOnlyList<ApiVersion> Supported { get; } =
    [
        V2
    ];

    public static IReadOnlyList<ApiVersion> Deprecated { get; } =
    [
        V1
    ];

    public static string DefaultOpenApiDocumentName => "v2";

    public static IReadOnlyList<string> OpenApiDocumentNames { get; } =
    [
        "v1",
        "v2"
    ];
}
