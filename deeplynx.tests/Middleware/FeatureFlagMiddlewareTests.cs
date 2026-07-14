// COMMENTED OUT UNTIL REAL FEATURE FLAGS ARE IMPLEMENTED
// using deeplynx.helpers;
// using Microsoft.AspNetCore.Http;
// using Moq;

// namespace deeplynx.tests.Middleware;

// /// <summary>
// ///     Unit tests for <see cref="FeatureFlagMiddleware"/> and the
// ///     <see cref="InsightEnabledAttribute"/> gate.
// ///
// ///     The middleware is exercised directly with a <see cref="DefaultHttpContext"/> —
// ///     no WebApplicationFactory or HTTP pipeline. Because the gate reads the
// ///     process-wide HIDE_INSIGHT environment variable, each test sets it explicitly
// ///     and the original value is restored on Dispose so state never leaks.
// /// </summary>
// public class FeatureFlagMiddlewareTests : IDisposable
// {
//     private readonly string? _originalHideInsight;

//     public FeatureFlagMiddlewareTests()
//     {
//         _originalHideInsight = Environment.GetEnvironmentVariable("HIDE_INSIGHT");
//     }

//     public void Dispose()
//     {
//         Environment.SetEnvironmentVariable("HIDE_INSIGHT", _originalHideInsight);
//     }

//     private static void SetHideInsight(string? value)
//     {
//         Environment.SetEnvironmentVariable("HIDE_INSIGHT", value);
//     }

//     private static HttpContext BuildContext(bool withEndpoint, bool withInsightAttribute)
//     {
//         var context = new DefaultHttpContext();
//         context.Response.Body = new MemoryStream();

//         if (withEndpoint)
//         {
//             var metadata = withInsightAttribute
//                 ? new EndpointMetadataCollection(new InsightEnabledAttribute())
//                 : new EndpointMetadataCollection();
//             context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));
//         }

//         return context;
//     }

//     private static (FeatureFlagMiddleware middleware, Func<bool> wasNextCalled) BuildMiddleware()
//     {
//         var nextCalled = false;
//         RequestDelegate next = _ =>
//         {
//             nextCalled = true;
//             return Task.CompletedTask;
//         };

//         var problemDetails = new Mock<IProblemDetailsService>();
//         problemDetails
//             .Setup(p => p.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
//             .Returns(ValueTask.FromResult(true));

//         var middleware = new FeatureFlagMiddleware(next, problemDetails.Object);
//         return (middleware, () => nextCalled);
//     }

//     [Fact]
//     public async Task InvokeAsync_CallsNext_WhenNoEndpoint()
//     {
//         SetHideInsight("true");
//         var context = BuildContext(withEndpoint: false, withInsightAttribute: false);
//         var (middleware, wasNextCalled) = BuildMiddleware();

//         await middleware.InvokeAsync(context);

//         Assert.True(wasNextCalled());
//         Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
//     }

//     [Fact]
//     public async Task InvokeAsync_CallsNext_WhenEndpointHasNoInsightAttribute()
//     {
//         SetHideInsight("true");
//         var context = BuildContext(withEndpoint: true, withInsightAttribute: false);
//         var (middleware, wasNextCalled) = BuildMiddleware();

//         await middleware.InvokeAsync(context);

//         Assert.True(wasNextCalled());
//         Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
//     }

//     [Fact]
//     public async Task InvokeAsync_Returns403_WhenInsightEnabledAndHideInsightUnset()
//     {
//         // Defaults to hidden when the env var is not supplied.
//         SetHideInsight(null);
//         var context = BuildContext(withEndpoint: true, withInsightAttribute: true);
//         var (middleware, wasNextCalled) = BuildMiddleware();

//         await middleware.InvokeAsync(context);

//         Assert.False(wasNextCalled());
//         Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
//     }

//     [Fact]
//     public async Task InvokeAsync_Returns403_WhenInsightEnabledAndHideInsightTrue()
//     {
//         SetHideInsight("true");
//         var context = BuildContext(withEndpoint: true, withInsightAttribute: true);
//         var (middleware, wasNextCalled) = BuildMiddleware();

//         await middleware.InvokeAsync(context);

//         Assert.False(wasNextCalled());
//         Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
//     }

//     [Fact]
//     public async Task InvokeAsync_CallsNext_WhenInsightEnabledAndHideInsightFalse()
//     {
//         SetHideInsight("false");
//         var context = BuildContext(withEndpoint: true, withInsightAttribute: true);
//         var (middleware, wasNextCalled) = BuildMiddleware();

//         await middleware.InvokeAsync(context);

//         Assert.True(wasNextCalled());
//         Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
//     }

//     [Theory]
//     [InlineData("False")]
//     [InlineData("FALSE")]
//     [InlineData(" false ")]
//     public async Task InvokeAsync_CallsNext_WhenHideInsightFalseRegardlessOfCaseOrWhitespace(string value)
//     {
//         // bool.TryParse is case-insensitive and trims whitespace; the frontend's
//         // isInsightHidden() is normalized to match, so these must un-hide too.
//         SetHideInsight(value);
//         var context = BuildContext(withEndpoint: true, withInsightAttribute: true);
//         var (middleware, wasNextCalled) = BuildMiddleware();

//         await middleware.InvokeAsync(context);

//         Assert.True(wasNextCalled());
//         Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
//     }

//     [Theory]
//     [InlineData("0")]
//     [InlineData("no")]
//     [InlineData("nonsense")]
//     public async Task InvokeAsync_Returns403_WhenHideInsightIsInvalid(string value)
//     {
//         // Anything that isn't a valid bool defaults to hidden (fail closed).
//         SetHideInsight(value);
//         var context = BuildContext(withEndpoint: true, withInsightAttribute: true);
//         var (middleware, wasNextCalled) = BuildMiddleware();

//         await middleware.InvokeAsync(context);

//         Assert.False(wasNextCalled());
//         Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
//     }
// }
