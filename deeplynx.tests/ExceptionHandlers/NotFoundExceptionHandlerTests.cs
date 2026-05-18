using deeplynx.helpers.ExceptionHandlers;
using deeplynx.helpers.exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.ExceptionHandlers;

public class NotFoundExceptionHandlerTests
{
    private readonly Mock<IProblemDetailsService> _problemDetailsServiceMock;
    private readonly Mock<IHostEnvironment> _hostEnvironmentMock;
    private readonly Mock<ILogger<NotFoundExceptionHandler>> _loggerMock;

    public NotFoundExceptionHandlerTests()
    {
        _problemDetailsServiceMock = new Mock<IProblemDetailsService>();
        _problemDetailsServiceMock
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .ReturnsAsync(true);

        _hostEnvironmentMock = new Mock<IHostEnvironment>();
        _loggerMock = new Mock<ILogger<NotFoundExceptionHandler>>();
    }

    [Fact]
    public async Task TryHandleAsync_Returns404_WhenKeyNotFoundException()
    {
        // Arrange
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        var context = CreateHttpContext();
        var handler = new NotFoundExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new KeyNotFoundException("entity missing");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.Is<ProblemDetailsContext>(ctx =>
                ctx.ProblemDetails.Status == StatusCodes.Status404NotFound &&
                ctx.ProblemDetails.Title == "Not Found")),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_Returns404_WhenNoResultsException()
    {
        // Arrange
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        var context = CreateHttpContext();
        var handler = new NotFoundExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new NoResultsException("no results");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.Is<ProblemDetailsContext>(ctx =>
                ctx.ProblemDetails.Status == StatusCodes.Status404NotFound)),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_IncludesExceptionMessage_WhenDevelopmentEnvironment()
    {
        // Arrange
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        var expectedMessage = "Project with id 42 in org 7 not found";
        var context = CreateHttpContext();
        var handler = new NotFoundExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new KeyNotFoundException(expectedMessage);

        // Act
        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
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
        var handler = new NotFoundExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new KeyNotFoundException("Project with id 42 in org 7 not found");

        // Act
        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.Is<ProblemDetailsContext>(ctx =>
                ctx.ProblemDetails.Detail == "The requested resource was not found.")),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsFalse_WhenExceptionTypeNotHandled()
    {
        // Arrange
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        var context = CreateHttpContext();
        var handler = new NotFoundExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new ArgumentException("bad argument");

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
