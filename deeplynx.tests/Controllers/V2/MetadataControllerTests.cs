using deeplynx.api.Controllers.V2;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for the V2 actions of <see cref="MetadataController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class MetadataControllerTests : IDisposable
{
    private readonly Mock<IMetadataBusiness> _mockMetadataBusiness;
    private readonly Mock<ILogger<MetadataController>> _mockLogger;
    private readonly MetadataController _metadataController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long DataSourceId = 5L;
    private const long UserId = 10L;

    public MetadataControllerTests()
    {
        _mockMetadataBusiness = new Mock<IMetadataBusiness>();
        _mockLogger = new Mock<ILogger<MetadataController>>();

        _metadataController = new MetadataController(
            _mockMetadataBusiness.Object,
            _mockLogger.Object);

        UserContextStorage.UserId = UserId;
    }

    public void Dispose()
    {
        // Reset to safe sentinels so a mutated value never bleeds into another class's tests
        UserContextStorage.UserId = default;
        UserContextStorage.OrganizationId = default;
        UserContextStorage.IsSysAdmin = default;
        UserContextStorage.IsOrgAdmin = default;
        UserContextStorage.IsProjectAdmin = default;
    }

    private static IFormFile CreateMockFormFile(string fileName = "metadata.json", string content = "{}")
    {
        var mockFile = new Mock<IFormFile>();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(bytes.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        return mockFile.Object;
    }

    // =========================================================================
    // CreateMetadata Tests
    // =========================================================================

    #region CreateMetadata Tests

    [Fact]
    public async Task CreateMetadata_Returns200_WithMetadata()
    {
        var request = new CreateMetadataRequestDto();
        var expected = new MetadataResponseDto();

        _mockMetadataBusiness.Setup(b => b.CreateMetadata(UserId, ProjectId, OrgId, DataSourceId, request))
                     .ReturnsAsync(expected);

        var result = (await _metadataController.CreateMetadata(
            OrgId, ProjectId, DataSourceId, request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateMetadata_ThrowsException_WhenBusinessThrows()
    {
        var request = new CreateMetadataRequestDto();

        _mockMetadataBusiness.Setup(b => b.CreateMetadata(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<CreateMetadataRequestDto>()))
                     .ThrowsAsync(new Exception("parse error"));

        await Assert.ThrowsAsync<Exception>(() => _metadataController.CreateMetadata(
            OrgId, ProjectId, DataSourceId, request));
    }

    [Fact]
    public async Task CreateMetadata_PassesCurrentUserIdIdsAndRequestToBusinessLayer()
    {
        var request = new CreateMetadataRequestDto();
        var expected = new MetadataResponseDto();

        _mockMetadataBusiness.Setup(b => b.CreateMetadata(UserId, ProjectId, OrgId, DataSourceId, request))
                     .ReturnsAsync(expected);

        await _metadataController.CreateMetadata(OrgId, ProjectId, DataSourceId, request);

        _mockMetadataBusiness.Verify(b => b.CreateMetadata(UserId, ProjectId, OrgId, DataSourceId, request), Times.Once);
    }

    #endregion

    // =========================================================================
    // CreateMetadataFromFile Tests
    // =========================================================================

    #region CreateMetadataFromFile Tests

    [Fact]
    public async Task CreateMetadataFromFile_Returns200_WithMetadata()
    {
        var file = CreateMockFormFile();
        var expected = new MetadataResponseDto();

        _mockMetadataBusiness.Setup(b => b.CreateMetadataFromFile(UserId, ProjectId, OrgId, DataSourceId, file))
                     .ReturnsAsync(expected);

        var result = (await _metadataController.CreateMetadataFromFile(
            OrgId, ProjectId, DataSourceId, file)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateMetadataFromFile_ThrowsException_WhenBusinessThrows()
    {
        var file = CreateMockFormFile();

        _mockMetadataBusiness.Setup(b => b.CreateMetadataFromFile(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<IFormFile>()))
                     .ThrowsAsync(new Exception("parse error"));

        await Assert.ThrowsAsync<Exception>(() => _metadataController.CreateMetadataFromFile(
            OrgId, ProjectId, DataSourceId, file));
    }

    [Fact]
    public async Task CreateMetadataFromFile_PassesCurrentUserIdIdsAndFileToBusinessLayer()
    {
        var file = CreateMockFormFile();
        var expected = new MetadataResponseDto();

        _mockMetadataBusiness.Setup(b => b.CreateMetadataFromFile(UserId, ProjectId, OrgId, DataSourceId, file))
                     .ReturnsAsync(expected);

        await _metadataController.CreateMetadataFromFile(OrgId, ProjectId, DataSourceId, file);

        _mockMetadataBusiness.Verify(b => b.CreateMetadataFromFile(UserId, ProjectId, OrgId, DataSourceId, file), Times.Once);
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void MetadataController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(MetadataController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void CreateMetadata_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(MetadataController.CreateMetadata),
            "organizationId", "projectId", "dataSourceId", "metadataRequestDto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "class");
        AssertHasAuthAttribute(method, "write", "relationship");
        AssertHasAuthAttribute(method, "write", "tag");
        AssertHasAuthAttribute(method, "write", "record");
        AssertHasAuthAttribute(method, "write", "edge");
    }

    [Fact]
    public void CreateMetadataFromFile_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(MetadataController.CreateMetadataFromFile),
            "organizationId", "projectId", "dataSourceId", "file");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "class");
        AssertHasAuthAttribute(method, "write", "relationship");
        AssertHasAuthAttribute(method, "write", "tag");
        AssertHasAuthAttribute(method, "write", "record");
        AssertHasAuthAttribute(method, "write", "edge");
    }

    #endregion

    // =========================================================================
    // Helpers for Auth / Middleware Metadata Tests
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(MetadataController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }

    private static void AssertHasHttpAttribute(
        System.Reflection.MethodInfo method,
        string expectedAttributeName)
    {
        Assert.Contains(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == expectedAttributeName);
    }

    private static void AssertHasAuthAttribute(
        System.Reflection.MethodInfo method,
        string expectedAction,
        string expectedResource)
    {
        var authAttributes = method.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "AuthAttribute")
            .ToList();

        Assert.Contains(authAttributes, attribute =>
            attribute.ConstructorArguments.Count >= 2 &&
            attribute.ConstructorArguments[0].Value?.ToString() == expectedAction &&
            attribute.ConstructorArguments[1].Value?.ToString() == expectedResource);
    }
}