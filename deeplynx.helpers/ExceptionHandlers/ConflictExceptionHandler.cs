using deeplynx.helpers.exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers.ExceptionHandlers;

/// <summary>
/// <see cref="IExceptionHandler"/> implementation for handling global uncaught exceptions from type
/// <see cref="DependencyDeletionException"/>, returning a consistent status code
/// <see cref="StatusCodes.Status409Conflict"/> and a standard response body
/// <see cref="ProblemDetails"/> (<see href="https://www.rfc-editor.org/rfc/rfc7807.html" />).
/// In non-Development environments the response detail is sanitized to avoid leaking internal exception messages.
/// </summary>
public class ConflictExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<ConflictExceptionHandler> _logger;

    public ConflictExceptionHandler(
        IProblemDetailsService problemDetailsService,
        IHostEnvironment hostEnvironment,
        ILogger<ConflictExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DependencyDeletionException)
            return false;

        _logger.LogWarning(exception, "Conflict on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        // For security purposes, sanitize the error message returned in production environments
        var detail = _hostEnvironment.IsDevelopment()
            ? exception.Message
            : "The request conflicts with the current state of the resource.";

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                Title = "Conflict",
                Status = StatusCodes.Status409Conflict,
                Detail = detail
            }
        });
    }
}
