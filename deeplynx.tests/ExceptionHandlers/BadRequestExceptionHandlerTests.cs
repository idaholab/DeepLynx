using System.ComponentModel.DataAnnotations;
using deeplynx.helpers.ExceptionHandlers;
using deeplynx.helpers.exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.ExceptionHandlers;

public class BadRequestExceptionHandlerTests
{
    private readonly Mock<IProblemDetailsService> _problemDetailsServiceMock;
    private readonly Mock<ILogger<BadRequestExceptionHandler>> _loggerMock;

    public BadRequestExceptionHandlerTests()
    {
        _problemDetailsServiceMock = new Mock<IProblemDetailsService>();
        _problemDetailsServiceMock
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .ReturnsAsync(true);

        _loggerMock = new Mock<ILogger<BadRequestExceptionHandler>>();
    }

    [Fact]
    public async Task TryHandleAsync_Returns400_WhenValidationException()
    {
        // Arrange
        var context = CreateHttpContext();
        var handler = new BadRequestExceptionHandler(_problemDetailsServiceMock.Object, _loggerMock.Object);
        var exception = new ValidationException("validation failed");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.Is<ProblemDetailsContext>(ctx =>
                ctx.ProblemDetails.Status == StatusCodes.Status400BadRequest &&
                ctx.ProblemDetails.Title == "Bad Request" &&
                ctx.ProblemDetails.Detail == "validation failed")),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsFalse_WhenArgumentException()
    {
        // Arrange
        // ArgumentException is intentionally NOT handled here — it falls through to the
        // InternalServerErrorExceptionHandler until the throw-site audit is remediated.
        // See the remarks block on BadRequestExceptionHandler for context.
        var context = CreateHttpContext();
        var handler = new BadRequestExceptionHandler(_problemDetailsServiceMock.Object, _loggerMock.Object);
        var exception = new ArgumentException("bad argument");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.False(result);
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()),
            Times.Never);
    }

    [Fact]
    public async Task TryHandleAsync_Returns400_WhenInvalidRequestException()
    {
        // Arrange
        var context = CreateHttpContext();
        var handler = new BadRequestExceptionHandler(_problemDetailsServiceMock.Object, _loggerMock.Object);
        var exception = new InvalidRequestException("invalid request");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.Is<ProblemDetailsContext>(ctx =>
                ctx.ProblemDetails.Status == StatusCodes.Status400BadRequest)),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsFalse_WhenExceptionTypeNotHandled()
    {
        // Arrange
        var context = CreateHttpContext();
        var handler = new BadRequestExceptionHandler(_problemDetailsServiceMock.Object, _loggerMock.Object);
        var exception = new KeyNotFoundException("not found");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()),
            Times.Never);
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/v1/test";
        context.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        return context;
    }
}
