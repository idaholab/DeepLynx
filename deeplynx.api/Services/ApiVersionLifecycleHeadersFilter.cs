using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.Filters;

namespace deeplynx.api.Services;

internal sealed class ApiVersionLifecycleHeadersFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        ApplyHeaders(context.HttpContext.Response.Headers, context.HttpContext.GetRequestedApiVersion());
        await next();
    }

    internal static void ApplyHeaders(IHeaderDictionary headers, ApiVersion? requestedVersion)
    {
        if (!NexusApiVersions.V1.Equals(requestedVersion))
            return;

        headers["Deprecation"] =
            $"@{NexusApiVersions.V1DeprecatedOn.ToUnixTimeSeconds()}";
    }
}
