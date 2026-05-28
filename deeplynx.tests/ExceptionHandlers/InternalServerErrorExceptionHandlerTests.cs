using System.ComponentModel.DataAnnotations;
using deeplynx.helpers.ExceptionHandlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.ExceptionHandlers;

public class InternalServerErrorExceptionHandlerTests
{
    private readonly Mock<IProblemDetailsService> _problemDetailsServiceMock;
    private readonly Mock<IHostEnvironment> _hostEnvironmentMock;
    private readonly Mock<ILogger<InternalServerErrorExceptionHandler>> _loggerMock;

    public InternalServerErrorExceptionHandlerTests()
    {
        _problemDetailsServiceMock = new Mock<IProblemDetailsService>();
        _problemDetailsServiceMock
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .ReturnsAsync(true);

        _hostEnvironmentMock = new Mock<IHostEnvironment>();
        _loggerMock = new Mock<ILogger<InternalServerErrorExceptionHandler>>();
    }

    [Fact]
    public async Task TryHandleAsync_Returns500_WhenAnyException()
    {
        // Arrange
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        var context = CreateHttpContext();
        var handler = new InternalServerErrorExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new InvalidOperationException("boom");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.Is<ProblemDetailsContext>(ctx =>
                ctx.ProblemDetails.Status == StatusCodes.Status500InternalServerError &&
                ctx.ProblemDetails.Title == "Internal Server Error")),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_IncludesExceptionMessage_WhenDevelopmentEnvironment()
    {
        // Arrange
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        var expectedMessage = "internal stack trace details";
        var context = CreateHttpContext();
        var handler = new InternalServerErrorExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new Exception(expectedMessage);

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.Is<ProblemDetailsContext>(ctx =>
                ctx.ProblemDetails.Detail == expectedMessage)),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsGenericMessage_WhenProductionEnvironment()
    {
        // Arrange
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        var context = CreateHttpContext();
        var handler = new InternalServerErrorExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new Exception("internal stack trace details");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.Is<ProblemDetailsContext>(ctx =>
                ctx.ProblemDetails.Detail == "An unexpected error occurred.")),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsTrue_AlwaysHandlesException()
    {
        // Arrange
        // The fallback handler must never decline an exception, regardless of type.
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        var context = CreateHttpContext();
        var handler = new InternalServerErrorExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new ValidationException("would normally be caught upstream");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
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
