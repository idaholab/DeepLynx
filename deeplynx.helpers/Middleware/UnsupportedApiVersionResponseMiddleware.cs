using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.helpers;

/// <summary>
///     Converts the empty 404 produced by endpoint routing for a well-formed but
///     unsupported URL-segment API version into a 400 Problem Details response.
/// </summary>
public sealed class UnsupportedApiVersionResponseMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IReadOnlyCollection<ApiVersion> _supportedVersions;

    public UnsupportedApiVersionResponseMiddleware(
        RequestDelegate next,
        IReadOnlyCollection<ApiVersion> supportedVersions)
    {
        _next = next;
        _supportedVersions = supportedVersions;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IProblemDetailsService problemDetailsService)
    {
        await _next(context);

        if (context.Response.HasStarted ||
            context.Response.StatusCode != StatusCodes.Status404NotFound ||
            !TryGetUnsupportedApiVersion(context.Request.Path, out var requestedVersion))
            return;

        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Unsupported API Version",
            Detail = $"API version '{requestedVersion}' is not supported."
        };
        problemDetails.Extensions["code"] = "UnsupportedApiVersion";

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });
    }

    private bool TryGetUnsupportedApiVersion(
        PathString path,
        out ApiVersion? requestedVersion)
    {
        requestedVersion = null;

        var segments = path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments is null ||
            segments.Length < 2 ||
            !string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) ||
            segments[1].Length < 2 ||
            char.ToLowerInvariant(segments[1][0]) != 'v' ||
            !ApiVersionParser.Default.TryParse(
                segments[1].AsSpan(1),
                out var parsedVersion))
            return false;

        requestedVersion = parsedVersion;
        return !_supportedVersions.Contains(parsedVersion);
    }
}
