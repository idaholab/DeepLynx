using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace deeplynx.tests.Versioning;

/// <summary>
///     Integration tests for the /api/v{version} routing and versioning contract.
///
///     Unlike the controller tests in deeplynx.tests/Controllers, which instantiate a
///     controller directly with Moq and never touch routing, these tests send real HTTP
///     requests through NexusWebApplicationFactory and check the real response - the only
///     way to prove routing, versioning, and response headers actually work.
///
///     A real Organization/Project is seeded per test (same pattern as PermissionBusinessTests),
///     because AuthMiddleware checks that the organization/project in the route actually exist
///     before the request ever reaches the controller - a nonexistent org/project 404s there,
///     before any versioning or business-layer behavior gets a chance to run.
/// </summary>
[Collection("Test Suite Collection")]
public class ApiVersioningTests : IntegrationTestBase
{
    private readonly NexusWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private long _organizationId;
    private long _projectId;

    private string ProjectTagsRoute => $"projects/{_projectId}/tags";

    public ApiVersioningTests(TestSuiteFixture fixture) : base(fixture)
    {
        _factory = new NexusWebApplicationFactory(fixture);
        _client = _factory.CreateClient();
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var organization = new Organization { Name = "Versioning Test Org" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        _organizationId = organization.Id;

        var project = new Project { Name = "Versioning Test Project", OrganizationId = _organizationId };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        _projectId = project.Id;
    }

    public override async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        await base.DisposeAsync();
    }

    // =========================================================================
    // App boots at all
    // =========================================================================

    [Fact]
    public async Task HealthCheck_Returns200_ProvingTheRealAppBootsUnderWebApplicationFactory()
    {
        var response = await _client.GetAsync(ApiPath("v1", "health"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // =========================================================================
    // Versioning headers (DL-1896: ReportApiVersions)
    // =========================================================================

    #region Versioning Headers

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task VersionedResponse_ReportsV2AsSupportedAndV1AsDeprecated(string version)
    {
        var response = await _client.GetAsync(ApiPath(version, ProjectTagsRoute));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var supportedVersions = Assert.Single(response.Headers.GetValues("api-supported-versions"));
        var deprecatedVersions = Assert.Single(response.Headers.GetValues("api-deprecated-versions"));

        Assert.Multiple(
            () => Assert.Equal("2", supportedVersions),
            () => Assert.Equal("1", deprecatedVersions));
    }

    [Fact]
    public async Task V1Response_HasDeprecationAndSunsetHeaders()
    {
        var response = await _client.GetAsync(ApiPath("v1", ProjectTagsRoute));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("@1785196800", Assert.Single(response.Headers.GetValues("Deprecation")));
        Assert.Equal(
            "Wed, 30 Sep 2026 23:59:59 GMT",
            Assert.Single(response.Headers.GetValues("Sunset")));
    }

    [Fact]
    public async Task V2Response_DoesNotHaveDeprecationOrSunsetHeaders()
    {
        var response = await _client.GetAsync(ApiPath("v2", ProjectTagsRoute));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Deprecation"));
        Assert.False(response.Headers.Contains("Sunset"));
    }

    #endregion

    // =========================================================================
    // OpenAPI deprecation metadata consumed by Scalar
    // =========================================================================

    #region OpenAPI Deprecation Metadata

    [Theory]
    [InlineData("v1", true)]
    [InlineData("v2", false)]
    public async Task OpenApiDocument_MarksOnlyV1OperationsAsDeprecated(
        string documentName,
        bool expectedDeprecated)
    {
        var response = await _client.GetAsync($"/api/openapi/{documentName}.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var content = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(content);
        var operations = GetOpenApiOperations(document.RootElement);

        Assert.NotEmpty(operations);

        var incorrectlyMarkedOperations = operations
            .Where(operation => operation.Deprecated != expectedDeprecated)
            .Select(operation => $"{operation.Method.ToUpperInvariant()} {operation.Path}")
            .ToArray();

        Assert.True(
            incorrectlyMarkedOperations.Length == 0,
            $"The {documentName} OpenAPI document contains operations with incorrect deprecation metadata: " +
            string.Join(", ", incorrectlyMarkedOperations));
    }

    #endregion

    // =========================================================================
    // Scalar default document
    // =========================================================================

    [Fact]
    public async Task Scalar_DefaultsToV2()
    {
        var response = await _client.GetAsync("/api/scalar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            """{"title":"v2","url":"api/openapi/v2.json","default":true}""",
            html);
        Assert.DoesNotContain(
            """{"title":"v1","url":"api/openapi/v1.json","default":true}""",
            html);
    }

    // =========================================================================
    // Unsupported version segment
    // =========================================================================

    [Fact]
    public async Task UnsupportedVersion_Returns400()
    {
        var response = await _client.GetAsync(ApiPath("v9", ProjectTagsRoute));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // =========================================================================
    // v2 error shapes: RFC 7807 ProblemDetails via the global exception handlers
    // (DL-1332). ITagBusiness is mocked so each test controls exactly which
    // exception is thrown - the same technique the direct-instantiation controller
    // tests already use, just now exercised through real HTTP instead of a direct
    // method call. This suite proves the HTTP/versioning envelope around an
    // exception; it is not responsible for proving the real business layer throws
    // that exception - that's what the existing business/controller tests cover.
    // =========================================================================

    #region V2 Error Shapes

    [Fact]
    public async Task V2_Returns404ProblemDetails_WhenKeyNotFoundExceptionThrown()
    {
        var mockBusiness = new Mock<ITagBusiness>();
        mockBusiness
            .Setup(b => b.GetTag(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<bool>()))
            .ThrowsAsync(new KeyNotFoundException("Tag not found"));

        using var mockedFactory = WithMockedTagBusiness(mockBusiness);
        using var client = mockedFactory.CreateClient();

        var response = await client.GetAsync(ApiPath("v2", $"{ProjectTagsRoute}/9"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status404NotFound, problem!.Status);
    }

    [Fact]
    public async Task V2_Returns400ProblemDetails_WhenValidationExceptionThrown()
    {
        var mockBusiness = new Mock<ITagBusiness>();
        mockBusiness
            .Setup(b => b.GetAllTags(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long[]?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new ValidationException("tag filter is invalid"));

        using var mockedFactory = WithMockedTagBusiness(mockBusiness);
        using var client = mockedFactory.CreateClient();

        var response = await client.GetAsync(ApiPath("v2", ProjectTagsRoute));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal("tag filter is invalid", problem.Detail);
    }

    [Fact]
    public async Task V2_Returns500ProblemDetails_AndDoesNotLeakRawExceptionMessage_WhenUnhandledExceptionThrown()
    {
        var mockBusiness = new Mock<ITagBusiness>();
        mockBusiness
            .Setup(b => b.GetAllTags(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long[]?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new Exception("some internal secret detail"));

        using var mockedFactory = WithMockedTagBusiness(mockBusiness);
        using var client = mockedFactory.CreateClient();

        var response = await client.GetAsync(ApiPath("v2", ProjectTagsRoute));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem!.Status);

        // The exact wording of the sanitized message belongs to InternalServerErrorExceptionHandler -
        // what this test actually guards is that the raw exception message never reaches the client.
        Assert.DoesNotContain("some internal secret detail", problem.Detail ?? string.Empty);
    }

    #endregion

    // =========================================================================
    // v1 regression guard (DL-1332): same failure, frozen legacy shape - proves
    // v1 did not silently adopt v2's ProblemDetails envelope.
    // =========================================================================

    #region V1 Regression Guard

    [Fact]
    public async Task V1_Returns500AsBareString_NotProblemDetails_WhenUnhandledExceptionThrown()
    {
        var mockBusiness = new Mock<ITagBusiness>();
        mockBusiness
            .Setup(b => b.GetAllTags(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long[]?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        using var mockedFactory = WithMockedTagBusiness(mockBusiness);
        using var client = mockedFactory.CreateClient();

        var response = await client.GetAsync(ApiPath("v1", ProjectTagsRoute));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("An error occurred while listing all tags", body);
    }

    #endregion

    // =========================================================================
    // Model-state validation 400 (DL-1899): same invalid payload, different
    // envelope per version. The Type/Title values below are the ones stated in
    // the ticket itself (v1 = frozen ASP.NET Core default, v2 = the unified
    // factory) - this is a stand-in "golden" check; swap it for your team's
    // existing snapshot pattern under ExceptionHandlers/Snapshots if you'd
    // rather keep the format consistent with those tests.
    // =========================================================================

    #region Model-State Validation 400

    [Fact]
    public async Task V1_ModelValidation400_MatchesFrozenFrameworkDefaultShape()
    {
        var response = await _client.PostAsync(ApiPath("v1", ProjectTagsRoute), InvalidCreateTagBody());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem!.Type);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("$.name", problem.Errors.Keys);
    }

    [Fact]
    public async Task V2_ModelValidation400_UsesUnifiedProblemDetailsEnvelope()
    {
        var response = await _client.PostAsync(ApiPath("v2", ProjectTagsRoute), InvalidCreateTagBody());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("https://tools.ietf.org/html/rfc7231#section-6.5.1", problem!.Type);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }

    private static StringContent InvalidCreateTagBody()
    {
        // Name is a string on CreateTagRequestDto. Supplying a number triggers
        // [ApiController]'s automatic model-state 400 before the controller method
        // (and therefore the business layer) ever runs.
        return new StringContent("""{"name":123}""", Encoding.UTF8, "application/json");
    }

    #endregion

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private static IReadOnlyList<OpenApiOperationStatus> GetOpenApiOperations(JsonElement root)
    {
        var operations = new List<OpenApiOperationStatus>();

        foreach (var path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (var candidate in path.Value.EnumerateObject())
            {
                if (!IsHttpMethod(candidate.Name))
                    continue;

                var deprecated = candidate.Value.TryGetProperty("deprecated", out var value)
                                 && value.ValueKind == JsonValueKind.True;

                operations.Add(new OpenApiOperationStatus(path.Name, candidate.Name, deprecated));
            }
        }

        return operations;
    }

    private static bool IsHttpMethod(string value)
    {
        return value is "get" or "put" or "post" or "delete" or "patch" or "options" or "head" or "trace";
    }

    private sealed record OpenApiOperationStatus(string Path, string Method, bool Deprecated);

    /// <summary>
    ///     Returns a copy of the factory with ITagBusiness replaced by the given mock,
    ///     so a test can force a specific exception without needing a real business-layer bug.
    ///     Dispose the returned factory (and any client built from it) when the test is done.
    /// </summary>
    private WebApplicationFactory<Program> WithMockedTagBusiness(Mock<ITagBusiness> mockBusiness)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITagBusiness>();
                services.AddScoped(_ => mockBusiness.Object);
            });
        });
    }
}
