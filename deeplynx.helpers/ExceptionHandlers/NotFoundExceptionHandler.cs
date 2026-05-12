using deeplynx.helpers.exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers.ExceptionHandlers;

/// <summary>
/// <see cref="IExceptionHandler"/> implementation for handling global uncaught exceptions from types
/// <see cref="KeyNotFoundException"/> and <see cref="NoResultsException"/>, returning a consistent
/// status code <see cref="StatusCodes.Status404NotFound"/> and a standard response body
/// <see cref="ProblemDetails"/> (<see href="https://www.rfc-editor.org/rfc/rfc7807.html" />).
/// In non-Development environments the response detail is sanitized to avoid leaking internal exception messages.
/// </summary>
public class NotFoundExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<NotFoundExceptionHandler> _logger;

    public NotFoundExceptionHandler(
        IProblemDetailsService problemDetailsService,
        IHostEnvironment hostEnvironment,
        ILogger<NotFoundExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not (KeyNotFoundException or NoResultsException))
            return false;

        _logger.LogWarning(exception, "Resource not found on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        // For security purposes, sanitize the error message returned in production environments
        var detail = _hostEnvironment.IsDevelopment()
            ? exception.Message
            : "The requested resource was not found.";

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = detail
            }
        });
    }
}
