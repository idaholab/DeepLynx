using deeplynx.helpers.exceptions;
using deeplynx.models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers.ExceptionHandlers;

/// <summary>
/// <see cref="IExceptionHandler"/> implementation for <see cref="OauthException"/>. Writes the
/// RFC 6749 OAuth error body (<see cref="OauthErrorResponseDto"/>) directly, since that wire shape
/// differs from the RFC 7807 <c>ProblemDetails</c> envelope used by the other global handlers.
/// </summary>
public class OauthExceptionHandler(ILogger<OauthExceptionHandler> logger) : IExceptionHandler
{
    private readonly ILogger<OauthExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not OauthException oauthException)
            return false;

        _logger.LogWarning(oauthException, "OAuth error: {ErrorCode}", oauthException.ErrorCode);

        httpContext.Response.StatusCode = oauthException.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(new OauthErrorResponseDto
        {
            Error = oauthException.ErrorCode,
            ErrorDescription = oauthException.Message
        }, cancellationToken);

        return true;
    }
}
