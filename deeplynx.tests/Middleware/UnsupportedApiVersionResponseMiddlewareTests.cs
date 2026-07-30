using Asp.Versioning;
using deeplynx.helpers;
using Microsoft.AspNetCore.Http;
using Moq;

namespace deeplynx.tests.Middleware;

[Collection("Test Suite Collection")]
public class UnsupportedApiVersionResponseMiddlewareTests
{
    private static readonly ApiVersion[] SupportedVersions =
    [
        new(1),
        new(2)
    ];

    [Fact]
    public async Task InvokeAsync_Returns400ProblemDetails_ForUnsupportedVersion()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v9/resource";

        ProblemDetailsContext? writtenContext = null;

        var problemDetailsService = new Mock<IProblemDetailsService>();
        problemDetailsService
            .Setup(service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(value => writtenContext = value)
            .ReturnsAsync(true);

        RequestDelegate next = httpContext =>
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        };

        var middleware = new UnsupportedApiVersionResponseMiddleware(next, SupportedVersions);

        await middleware.InvokeAsync(context, problemDetailsService.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.NotNull(writtenContext);
        Assert.Equal(StatusCodes.Status400BadRequest, writtenContext.ProblemDetails.Status);
        Assert.Equal("Unsupported API Version", writtenContext.ProblemDetails.Title);
        Assert.Equal(
            "UnsupportedApiVersion",
            Assert.IsType<string>(writtenContext.ProblemDetails.Extensions["code"]));

        problemDetailsService.Verify(
            service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()),
            Times.Once);
    }

    [Theory]
    [InlineData("/api/v1/resource")]
    [InlineData("/api/v2/resource")]
    [InlineData("/api/vbanana/resource")]
    [InlineData("/not-api/v9/resource")]
    public async Task InvokeAsync_Preserves404_WhenVersionIsNotUnsupported(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        var problemDetailsService = new Mock<IProblemDetailsService>();

        RequestDelegate next = httpContext =>
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        };

        var middleware = new UnsupportedApiVersionResponseMiddleware(next, SupportedVersions);

        await middleware.InvokeAsync(context, problemDetailsService.Object);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        problemDetailsService.Verify(
            service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_PreservesNon404Response_ForUnsupportedVersion()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v9/resource";

        var problemDetailsService = new Mock<IProblemDetailsService>();

        RequestDelegate next = httpContext =>
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        var middleware = new UnsupportedApiVersionResponseMiddleware(next, SupportedVersions);

        await middleware.InvokeAsync(context, problemDetailsService.Object);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        problemDetailsService.Verify(
            service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()),
            Times.Never);
    }
}
