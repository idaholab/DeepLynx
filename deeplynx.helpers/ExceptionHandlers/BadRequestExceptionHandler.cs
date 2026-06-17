using System.ComponentModel.DataAnnotations;
using deeplynx.helpers.exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers.ExceptionHandlers;

/// <summary>
/// <see cref="IExceptionHandler"/> implementation for handling global uncaught exceptions from types
/// <see cref="ValidationException"/> and <see cref="InvalidRequestException"/>,
/// returning a consistent status code <see cref="StatusCodes.Status400BadRequest"/> and a standard response body
/// <see cref="ProblemDetails"/> (<see href="https://www.rfc-editor.org/rfc/rfc7807.html" />)
/// </summary>
/// <remarks>
/// NOTE: <see cref="ArgumentException"/> is intentionally NOT handled here, even though it logically maps to a 400.
/// An audit found that many existing <c>throw new ArgumentException</c> sites leak server-side details (env-var names,
/// internal Azure config state, folder paths) or use the wrong type entirely (should be <see cref="KeyNotFoundException"/>
/// or a 409-mapped conflict exception). Until those sites are remediated, <see cref="ArgumentException"/> is allowed to
/// fall through to the <see cref="InternalServerErrorExceptionHandler"/>, where the message is sanitized in non-Development
/// environments. Once the audit is cleared, add <see cref="ArgumentException"/> back to the type guard below.
///
/// See: <see href="https://nstinl.atlassian-us-gov-mod.net/wiki/spaces/DeepLynx/pages/5341621/Backend+Architecture+Review#ArgumentException-Misuse" />
/// </remarks>
public class BadRequestExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<BadRequestExceptionHandler> _logger;

    public BadRequestExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<BadRequestExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not (ValidationException or InvalidRequestException))
            return false;

        _logger.LogWarning(exception, "Bad request on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = BadRequestProblemDetailsFactory.Create(exception.Message)
        });
    }
}
