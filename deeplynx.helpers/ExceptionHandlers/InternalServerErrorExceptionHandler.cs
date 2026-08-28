using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace deeplynx.helpers.ExceptionHandlers;

/// <summary>
/// Global fallback handler for uncaught exceptions that were not handled by a more specific
/// <see cref="IExceptionHandler"/>.
///
/// By default, uncaught exceptions are returned as HTTP 500 responses with sanitized details
/// outside Development. Insight service exceptions preserve their upstream HTTP status code
/// and message so more meaningful model/service errors are returned to clients.
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

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Unhandled exception on {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var statusCode = StatusCodes.Status500InternalServerError;
        var title = "Internal Server Error";
        string detail;

        if (exception is InsightServiceException insightException)
        {
            statusCode = insightException.StatusCode.HasValue
                ? (int)insightException.StatusCode.Value
                : StatusCodes.Status502BadGateway;

            title = "Insight Service Error";
            detail = insightException.Message;
        }
        else
        {
            var path = httpContext.Request.Path.Value ?? string.Empty;

            if (path.Contains("/promote") && path.Contains("/extractions"))
            {
                detail = exception.Message;
            }
            else if (_hostEnvironment.IsDevelopment())
            {
                detail = exception.Message;
            }
            else
            {
                detail = "An unexpected error occurred.";
            }
        }

        httpContext.Response.StatusCode = statusCode;

        await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = title,
                Status = statusCode,
                Detail = detail
            }
        });

        return true;
    }
}