using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for <see cref="OlapController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class OlapControllerTests : IDisposable
{
    private readonly Mock<IOlapBusiness> _mockOlapBusiness;
    private readonly Mock<ILogger<OlapController>> _mockLogger;
    private readonly OlapController _olapController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long RecordIdConst = 7L;
    private const string ViewName = "test-view";
    private const long partNumber = 1L;
    public OlapControllerTests()
    {
        _mockOlapBusiness = new Mock<IOlapBusiness>();
        _mockLogger = new Mock<ILogger<OlapController>>();

        _olapController = new OlapController(
            _mockOlapBusiness.Object,
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

    // =========================================================================
    // ExecuteOlapQuery Tests
    // =========================================================================

    #region ExecuteOlapQuery Tests

    [Fact]
    public async Task ExecuteOlapQuery_Returns200_WithPLotData()
    {
        var request = new OlapQueryRequestDto();
        var expected = new PlotDataDto();

        _mockOlapBusiness.Setup(b => b.QueryTabularFile(
            UserId, OrgId, ProjectId, RecordIdConst, request, ViewName))
            .ReturnsAsync(expected);

        var result = (await _olapController.ExecuteOlapQuery(
            OrgId,
            ProjectId,
            RecordIdConst,
            ViewName,
            request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task ExecuteOlapQuery_Returns200_WithNrException()
    {
        var request = new OlapQueryRequestDto();
        const string expected = "No results found";

        _mockOlapBusiness.Setup(b => b.QueryTabularFile(
            UserId, OrgId, ProjectId, RecordIdConst, request, ViewName))
            .ThrowsAsync(new NoResultsException(expected));

        var result = (await _olapController.ExecuteOlapQuery(
            OrgId,
            ProjectId,
            RecordIdConst,
            ViewName,
            request)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task ExecuteOlapQuery_Returns400_InvalidOlapQuery()
    {
        var request = new OlapQueryRequestDto();
        const string expected = "Invalid OLAP query request";

        _mockOlapBusiness.Setup(b => b.QueryTabularFile(
            UserId, OrgId, ProjectId, RecordIdConst, request, ViewName))
            .ThrowsAsync(new ArgumentException(expected));

        var result = (await _olapController.ExecuteOlapQuery(
            OrgId,
            ProjectId,
            RecordIdConst,
            ViewName,
            request)).Result as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task ExecuteOlapQuery_Returns400_OnInvalidOperationException()
    {
        var request = new OlapQueryRequestDto();
        const string expectedMessage = "Invalid OLAP operation";

        _mockOlapBusiness.Setup(b => b.QueryTabularFile(
                         UserId, OrgId, ProjectId, RecordIdConst, request, ViewName))
                     .ThrowsAsync(new InvalidOperationException(expectedMessage));

        var result = (await _olapController.ExecuteOlapQuery(
            OrgId,
            ProjectId,
            RecordIdConst,
            ViewName,
            request)).Result as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(expectedMessage, result.Value);
    }

    [Fact]
    public async Task ExecuteOlapQuery_Returns500_OnUnexpectedException()
    {
        var request = new OlapQueryRequestDto();

        _mockOlapBusiness.Setup(b => b.QueryTabularFile(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<long>(), It.IsAny<OlapQueryRequestDto>(), It.IsAny<string>()))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _olapController.ExecuteOlapQuery(
            OrgId,
            ProjectId,
            RecordIdConst,
            ViewName,
            request)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.IsType<string>(result.Value);
        Assert.Contains("An error occurred while querying tabular data", result.Value.ToString());
    }

    [Fact]
    public async Task ExecuteOlapQuery_PassesIdsRequestAndViewNameToBusinessLayer()
    {
        var request = new OlapQueryRequestDto();
        var expected = new PlotDataDto();

        _mockOlapBusiness.Setup(b => b.QueryTabularFile(
                         UserId, OrgId, ProjectId, RecordIdConst, request, ViewName))
                     .ReturnsAsync(expected);

        await _olapController.ExecuteOlapQuery(
            OrgId,
            ProjectId,
            RecordIdConst,
            ViewName,
            request);

        _mockOlapBusiness.Verify(b => b.QueryTabularFile(
            UserId, OrgId, ProjectId, RecordIdConst, request, ViewName), Times.Once);
    }
    #endregion

    // =========================================================================
    // AppendTabularFile Tests
    // =========================================================================

    #region AppendTabularFile Tests

    [Fact]
    public async Task AppendTabularFile_Returns200_WithMessage()
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("append.csv");
        var file = mockFile.Object;

        _mockOlapBusiness.Setup(b => b.AppendTabularBlob(
                         OrgId, ProjectId, RecordIdConst, partNumber, file))
                     .Returns(Task.CompletedTask);

        var result = (await _olapController.AppendTabularFile(
            OrgId,
            ProjectId,
            RecordIdConst,
            partNumber,
            file)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("Data appended", result.Value);
    }

    [Fact]
    public async Task AppendTabularFile_Returns500_OnUnexpectedExcepption()
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("append.csv");
        var file = mockFile.Object;

        _mockOlapBusiness.Setup(b => b.AppendTabularBlob(
                         OrgId, ProjectId, RecordIdConst, partNumber, file))
                     .Throws(new Exception("append error"));

        var result = (await _olapController.AppendTabularFile(
            OrgId,
            ProjectId,
            RecordIdConst,
            partNumber,
            file)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.IsType<string>(result.Value);
        Assert.Contains("An error occurred while appending to a tabular file for append.csv", result.Value.ToString());
        Assert.Contains("append error", result.Value.ToString());
    }

    [Fact]
    public async Task AppendTabularFile_PassesIdsPartNumberAndFileToBusinessLayer()
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("append.csv");
        var file = mockFile.Object;

        _mockOlapBusiness.Setup(b => b.AppendTabularBlob(
                         OrgId, ProjectId, RecordIdConst, partNumber, file))
                     .Returns(Task.CompletedTask);

        await _olapController.AppendTabularFile(
            OrgId,
            ProjectId,
            RecordIdConst,
            partNumber,
            file);

        _mockOlapBusiness.Verify(b => b.AppendTabularBlob(
            OrgId, ProjectId, RecordIdConst, partNumber, file), Times.Once);
    }
    #endregion

    // =========================================================================
    // GetPlotData Tests
    // =========================================================================

    #region GetPlotData Tests

    [Fact]
    public async Task GetPlotData_Returns200_WithPLotData()
    {
        var request = new OlapQueryRequestDto();
        var expected = new PlotDataDto();

        _mockOlapBusiness.Setup(b => b.GetPlotData(
            UserId, OrgId, ProjectId, RecordIdConst, request))
            .ReturnsAsync(expected);

        var result = (await _olapController.GetPlotData(
            OrgId,
            ProjectId,
            RecordIdConst,
            request)) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var plotDataProperty = result.Value.GetType().GetProperty("PlotData");
        Assert.NotNull(plotDataProperty);

        var actual = plotDataProperty.GetValue(result.Value);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task GetPlotData_Returns400_WithMessage()
    {
        var request = new OlapQueryRequestDto();
        const string expected = "Invalid request for plot data";

        _mockOlapBusiness.Setup(b => b.GetPlotData(
            UserId, OrgId, ProjectId, RecordIdConst, request))
            .ThrowsAsync(new ArgumentException(expected));

        var result = (await _olapController.GetPlotData(
            OrgId,
            ProjectId,
            RecordIdConst,
            request)) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetPlotData_Returns500_WithMessage()
    {
        var request = new OlapQueryRequestDto();
        const string expected = "database connection failed";

        _mockOlapBusiness.Setup(b => b.GetPlotData(
            UserId, OrgId, ProjectId, RecordIdConst, request))
            .ThrowsAsync(new Exception(expected));

        var result = (await _olapController.GetPlotData(
            OrgId,
            ProjectId,
            RecordIdConst,
            request)) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetPlotData_PassesIdAndRequestsToBusinessLayer()
    {
        var request = new OlapQueryRequestDto();
        var expected = new PlotDataDto();

        _mockOlapBusiness.Setup(b => b.GetPlotData(
            UserId, OrgId, ProjectId, RecordIdConst, request))
            .ReturnsAsync(expected);

        await _olapController.GetPlotData(
            OrgId,
            ProjectId,
            RecordIdConst,
            request);

        _mockOlapBusiness.Verify(b => b.GetPlotData(
            UserId, OrgId, ProjectId, RecordIdConst, request), Times.Once);
    }
    #endregion

    // =========================================================================
    // GetHighestPartNumber Tests
    // =========================================================================

    #region GetHighestPartNumber Tests

    [Fact]
    public async Task GetHighestPartNumber_Returns200_WithHighestPartNumber()
    {
        const long expected = 2L;

        _mockOlapBusiness.Setup(b => b.GetHighestPartNumber(
            OrgId, ProjectId, RecordIdConst))
            .ReturnsAsync(expected);

        var result = (await _olapController.GetHighestPartNumber(
            OrgId,
            ProjectId,
            RecordIdConst)) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);

        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetHighestPartNumber_Returns400_InvalidRequestMessage()
    {
        const string expected = "Invalid request to get highest part number";

        _mockOlapBusiness.Setup(b => b.GetHighestPartNumber(
            OrgId, ProjectId, RecordIdConst))
            .ThrowsAsync(new ArgumentException(expected));

        var result = (await _olapController.GetHighestPartNumber(
            OrgId,
            ProjectId,
            RecordIdConst)) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetHighestPartNumber_Returns500_WithMessage()
    {
        string expected = "database connection failed";

        _mockOlapBusiness.Setup(b => b.GetHighestPartNumber(
            OrgId, ProjectId, RecordIdConst))
            .ThrowsAsync(new Exception(expected));

        var result = (await _olapController.GetHighestPartNumber(
            OrgId,
            ProjectId,
            RecordIdConst)) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetHighestPartNumber_PassesIdToBusinessLayer()
    {
        const long expected = 2L;

        _mockOlapBusiness.Setup(b => b.GetHighestPartNumber(
            OrgId, ProjectId, RecordIdConst))
            .ReturnsAsync(expected);

        await _olapController.GetHighestPartNumber(
            OrgId,
            ProjectId,
            RecordIdConst);

        _mockOlapBusiness.Verify(b => b.GetHighestPartNumber(
            OrgId, ProjectId, RecordIdConst), Times.Once);
    }
    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void OlapController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(OlapController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void ExecuteOlapQuery_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(OlapController.ExecuteOlapQuery),
            "organizationId",
            "projectId",
            "recordId",
            "viewName",
            "request");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "read", "record");
        AssertHasAuthAttribute(method, "read", "file");
        AssertHasSensitivityAttribute(method, "download file");
    }

    [Fact]
    public void AppendTabularFile_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(OlapController.AppendTabularFile),
            "organizationId",
            "projectId",
            "recordId",
            "partNumber",
            "file");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "read", "record");
        AssertHasAuthAttribute(method, "update", "file");
        AssertHasSensitivityAttribute(method, "update file");
    }

    [Fact]
    public void GetPlotData_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(OlapController.GetPlotData),
            "organizationId",
            "projectId",
            "recordId",
            "request");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record");
        AssertHasAuthAttribute(method, "read", "file");
        AssertHasSensitivityAttribute(method, "download file");
    }

    [Fact]
    public void GetHighestPartNumber_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(OlapController.GetHighestPartNumber),
            "organizationId",
            "projectId",
            "recordId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record");
        AssertHasAuthAttribute(method, "read", "file");
        AssertHasSensitivityAttribute(method, "download file");
    }

    #endregion

    // =========================================================================
    // Helpers for Auth / Middleware Metadata Tests
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(OlapController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => method.GetParameters()
                .Select(parameter => parameter.Name ?? string.Empty)
                .SequenceEqual(parameterNames)));
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

    private static void AssertHasSensitivityAttribute(
        System.Reflection.MethodInfo method,
        string expectedSensitivity)
    {
        var sensitivityAttributes = method.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "SensitivityAttribute")
            .ToList();

        Assert.Contains(sensitivityAttributes, attribute =>
            attribute.ConstructorArguments.Any(argument =>
                argument.Value?.ToString() == expectedSensitivity));
    }
}