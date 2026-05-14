using deeplynx.helpers.ExceptionHandlers;
using deeplynx.helpers.exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.ExceptionHandlers;

public class ConflictExceptionHandlerTests
{
    private readonly Mock<IProblemDetailsService> _problemDetailsServiceMock;
    private readonly Mock<IHostEnvironment> _hostEnvironmentMock;
    private readonly Mock<ILogger<ConflictExceptionHandler>> _loggerMock;

    public ConflictExceptionHandlerTests()
    {
        _problemDetailsServiceMock = new Mock<IProblemDetailsService>();
        _problemDetailsServiceMock
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .ReturnsAsync(true);

        _hostEnvironmentMock = new Mock<IHostEnvironment>();
        _loggerMock = new Mock<ILogger<ConflictExceptionHandler>>();
    }

    [Fact]
    public async Task TryHandleAsync_Returns409_WhenDependencyDeletionException()
    {
        // Arrange
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        var context = CreateHttpContext();
        var handler = new ConflictExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new DependencyDeletionException("dependent records exist");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.Is<ProblemDetailsContext>(ctx =>
                ctx.ProblemDetails.Status == StatusCodes.Status409Conflict &&
                ctx.ProblemDetails.Title == "Conflict")),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_IncludesExceptionMessage_WhenDevelopmentEnvironment()
    {
        // Arrange
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
        var expectedMessage = "Cannot delete project 42: 17 records depend on it";
        var context = CreateHttpContext();
        var handler = new ConflictExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new DependencyDeletionException(expectedMessage);

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
        var handler = new ConflictExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new DependencyDeletionException("Cannot delete project 42: 17 records depend on it");

        // Act
        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.Is<ProblemDetailsContext>(ctx =>
                ctx.ProblemDetails.Detail == "The request conflicts with the current state of the resource.")),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsFalse_WhenInvalidOperationException()
    {
        // Arrange
        // Locks in the decision that built-in InvalidOperationException is NOT mapped
        // to 409; it must fall through to the InternalServerError fallback.
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        var context = CreateHttpContext();
        var handler = new ConflictExceptionHandler(
            _problemDetailsServiceMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
        var exception = new InvalidOperationException("invalid op");

        // Act
        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        _problemDetailsServiceMock.Verify(
            s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()),
            Times.Never);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsFalse_WhenExceptionTypeNotHandled()
    {
        // Arrange
        _hostEnvironmentMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        var context = CreateHttpContext();
        var handler = new ConflictExceptionHandler(
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
        context.Request.Method = "DELETE";
        context.Request.Path = "/api/v1/test";
        context.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        return context;
    }
}
