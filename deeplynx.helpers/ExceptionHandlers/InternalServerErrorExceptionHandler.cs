using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers.ExceptionHandlers;

/// <summary>
/// <see cref="IExceptionHandler"/> implementation acting as the global fallback for any uncaught
/// <see cref="Exception"/> not matched by a more specific handler, returning a consistent status
/// code <see cref="StatusCodes.Status500InternalServerError"/> and a standard response body
/// <see cref="ProblemDetails"/> (<see href="https://www.rfc-editor.org/rfc/rfc7807.html" />).
/// In non-Development environments the response detail is sanitized to avoid leaking internal exception messages.
/// </summary>
public class InternalServerErrorExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<InternalServerErrorExceptionHandler> _logger;

    public InternalServerErrorExceptionHandler(
        IProblemDetailsService problemDetailsService,
        IHostEnvironment hostEnvironment,
        ILogger<InternalServerErrorExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // For security purposes, sanitize the error message returned in production environments
        var detail = _hostEnvironment.IsDevelopment()
            ? exception.Message
            : "An unexpected error occurred.";

        await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Internal Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = detail
            }
        });

        return true;
    }
}
