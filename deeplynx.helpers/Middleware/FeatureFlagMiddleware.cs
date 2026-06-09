using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.helpers;

/// <summary>
/// Requires insight to be enabled to function properly
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class InsightEnabledAttribute : Attribute
{
}

public class FeatureFlagMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IProblemDetailsService _problemDetailsService;

    public FeatureFlagMiddleware(RequestDelegate next, IProblemDetailsService problemDetailsService)
    {
        _next = next;
        _problemDetailsService = problemDetailsService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await _next(context);
            return;
        }

        // check for feature flag attributes
        var insightAttr = endpoint.Metadata.GetMetadata<InsightEnabledAttribute>();

        // if no feature flag attributes, continue. right now it's just the
        // one feature but keeping this check around for future features
        if (insightAttr == null) {
            await _next(context);
            return;
        }

        // HIDE_INSIGHT env var. Defaults to true if not supplied.
        var hideInsight = !bool.TryParse(Environment.GetEnvironmentVariable("HIDE_INSIGHT"), out var result) || result;
        if (insightAttr != null) // this is redundant now but won't be once more feature flags are added
        {
            if (hideInsight)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = new ProblemDetails
                    {
                        Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                        Title = "Forbidden",
                        Status = StatusCodes.Status403Forbidden,
                        Detail = "Insight features are disabled on this instance."
                    }
                });
                return;
            }

            await _next(context);
            return;
        }
    }
}
