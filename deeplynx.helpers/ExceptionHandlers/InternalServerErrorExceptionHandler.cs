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
    private readonly ILogger<InternalServerErrorExceptionHandler> _logger;

    public InternalServerErrorExceptionHandler(
        IProblemDetailsService problemDetailsService,
        IHostEnvironment hostEnvironment,
        ILogger<InternalServerErrorExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
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

        var (statusCode, title, detail) = exception is InsightServiceException insightException
            ? GetInsightServiceErrorDetails(insightException)
            : GetInternalServerErrorDetails(exception);

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

    private static (int StatusCode, string Title, string Detail)
        GetInsightServiceErrorDetails(InsightServiceException exception)
    {
        var statusCode = exception.StatusCode.HasValue
            ? (int)exception.StatusCode.Value
            : StatusCodes.Status502BadGateway;

        return (
            statusCode,
            "Insight Service Error",
            exception.Message);
    }

    private static (int StatusCode, string Title, string Detail)
        GetInternalServerErrorDetails(Exception exception)
    {
        return (
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            exception.Message);
    }
}