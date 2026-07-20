using System.Text.Json;
using Asp.Versioning;
using deeplynx.api.ExceptionHandlers;
using deeplynx.helpers.ExceptionHandlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace deeplynx.tests.ExceptionHandlers;

public class VersionedInvalidModelStateResponseFactoryTests
{
    [Fact]
    public void Create_UsesFrameworkValidationProblemDetails_ForV1()
    {
        var context = CreateActionContext(new ApiVersion(1));

        var result = Assert.IsType<BadRequestObjectResult>(
            VersionedInvalidModelStateResponseFactory.Create(context));
        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);

        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem.Type);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("$.name", problem.Errors.Keys);
        Assert.Equal(["application/problem+json"], result.ContentTypes);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Create_UsesUnifiedBadRequestProblemDetails_ForVersionsAfterV1(int majorVersion)
    {
        var context = CreateActionContext(new ApiVersion(majorVersion));

        var result = Assert.IsType<BadRequestObjectResult>(
            VersionedInvalidModelStateResponseFactory.Create(context));
        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);

        Assert.Equal(BadRequestProblemDetailsFactory.ProblemType, problem.Type);
        Assert.Equal(BadRequestProblemDetailsFactory.ProblemTitle, problem.Title);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("name", problem.Errors.Keys);
        Assert.DoesNotContain("$.name", problem.Errors.Keys);
        Assert.Equal(["application/problem+json"], result.ContentTypes);
    }

    [Fact]
    public void Create_UsesV1Fallback_WhenVersionIsUnresolved()
    {
        var context = CreateActionContext(requestedVersion: null);

        var result = Assert.IsType<BadRequestObjectResult>(
            VersionedInvalidModelStateResponseFactory.Create(context));
        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);

        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem.Type);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.Contains("$.name", problem.Errors.Keys);
    }

    [Fact]
    public void Create_V1ResponseMatchesFrozenGoldenPayload()
    {
        var context = CreateActionContext(new ApiVersion(1));
        var result = Assert.IsType<BadRequestObjectResult>(
            VersionedInvalidModelStateResponseFactory.Create(context));
        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);

        var actual = JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var goldenPath = Path.Combine(
            AppContext.BaseDirectory,
            "ExceptionHandlers",
            "Snapshots",
            "v1-validation-problem-details.json");
        var expected = File.ReadAllText(goldenPath).TrimEnd();

        Assert.Equal(expected, actual);
    }

    private static ActionContext CreateActionContext(ApiVersion? requestedVersion)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddControllers()
            .Services
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            TraceIdentifier = "frozen-v1-trace-id"
        };

        var versioningFeature = new ApiVersioningFeature(httpContext)
        {
            RequestedApiVersion = requestedVersion
        };
        httpContext.Features.Set<IApiVersioningFeature>(versioningFeature);

        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.name", "The name field is required.");

        return new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            modelState);
    }
}
