using deeplynx.api.Controllers.V2;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for the V2 actions of <see cref="HistoricalEdgeController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
///     Note: this controller does not itself read UserContextStorage, but the reset is
///     kept here for consistency with the rest of the controller test suite.
/// </summary>
public class HistoricalEdgeControllerTests : IDisposable
{
    private readonly Mock<IHistoricalEdgeBusiness> _mockHistoricalEdgeBusiness;
    private readonly Mock<ILogger<HistoricalEdgeController>> _mockLogger;
    private readonly HistoricalEdgeController _historicalEdgeController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long EdgeIdConst = 30L;
    private const long DataSourceId = 5L;
    private const long OriginId = 40L;
    private const long DestinationId = 41L;
    private static readonly DateTime PointInTimeConst = new(2024, 1, 1);

    public HistoricalEdgeControllerTests()
    {
        _mockHistoricalEdgeBusiness = new Mock<IHistoricalEdgeBusiness>();
        _mockLogger = new Mock<ILogger<HistoricalEdgeController>>();

        _historicalEdgeController = new HistoricalEdgeController(
            _mockHistoricalEdgeBusiness.Object,
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
    // GetAllHistoricalEdges Tests
    // =========================================================================

    #region GetAllHistoricalEdges Tests

    [Fact]
    public async Task GetAllHistoricalEdges_Returns200_WithList()
    {
        var expected = new List<HistoricalEdgeResponseDto> { new(), new() };

        _mockHistoricalEdgeBusiness.Setup(b => b.GetAllHistoricalEdges(
                         ProjectId, DataSourceId, PointInTimeConst, true))
                     .ReturnsAsync(expected);

        var result = (await _historicalEdgeController.GetAllHistoricalEdges(
            OrgId, ProjectId, DataSourceId, PointInTimeConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllHistoricalEdges_Returns200_WithEmptyList()
    {
        _mockHistoricalEdgeBusiness.Setup(b => b.GetAllHistoricalEdges(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<DateTime?>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _historicalEdgeController.GetAllHistoricalEdges(
            OrgId, ProjectId, null, null, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<HistoricalEdgeResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllHistoricalEdges_ThrowsException_WhenBusinessThrows()
    {
        _mockHistoricalEdgeBusiness.Setup(b => b.GetAllHistoricalEdges(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<DateTime?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _historicalEdgeController.GetAllHistoricalEdges(
            OrgId, ProjectId, null, null, true));
    }

    [Fact]
    public async Task GetAllHistoricalEdges_PassesProjectIdFiltersToBusinessLayer()
    {
        _mockHistoricalEdgeBusiness.Setup(b => b.GetAllHistoricalEdges(
                         ProjectId, DataSourceId, PointInTimeConst, false))
                     .ReturnsAsync([]);

        await _historicalEdgeController.GetAllHistoricalEdges(
            OrgId, ProjectId, DataSourceId, PointInTimeConst, hideArchived: false);

        _mockHistoricalEdgeBusiness.Verify(b => b.GetAllHistoricalEdges(
            ProjectId, DataSourceId, PointInTimeConst, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetHistoricalEdgeById Tests
    // =========================================================================

    #region GetHistoricalEdgeById Tests

    [Fact]
    public async Task GetHistoricalEdgeById_Returns200_WithEdge()
    {
        var expected = new HistoricalEdgeResponseDto();

        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoricalEdge(
                         OrgId, EdgeIdConst, null, null, PointInTimeConst, true))
                     .ReturnsAsync(expected);

        var result = (await _historicalEdgeController.GetHistoricalEdgeById(
            OrgId, ProjectId, EdgeIdConst, PointInTimeConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetHistoricalEdgeById_ThrowsException_WhenBusinessThrows()
    {
        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoricalEdge(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>(),
                         It.IsAny<DateTime?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _historicalEdgeController.GetHistoricalEdgeById(
            OrgId, ProjectId, EdgeIdConst, null, true));
    }

    [Fact]
    public async Task GetHistoricalEdgeById_PassesIdsAndFiltersToBusinessLayer()
    {
        var expected = new HistoricalEdgeResponseDto();

        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoricalEdge(
                         OrgId, EdgeIdConst, null, null, PointInTimeConst, false))
                     .ReturnsAsync(expected);

        await _historicalEdgeController.GetHistoricalEdgeById(
            OrgId, ProjectId, EdgeIdConst, PointInTimeConst, hideArchived: false);

        _mockHistoricalEdgeBusiness.Verify(b => b.GetHistoricalEdge(
            OrgId, EdgeIdConst, null, null, PointInTimeConst, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetHistoricalEdgeByRelationship Tests
    // =========================================================================

    #region GetHistoricalEdgeByRelationship Tests

    [Fact]
    public async Task GetHistoricalEdgeByRelationship_Returns200_WithEdge()
    {
        var expected = new HistoricalEdgeResponseDto();

        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoricalEdge(
                         OrgId, null, OriginId, DestinationId, PointInTimeConst, true))
                     .ReturnsAsync(expected);

        var result = (await _historicalEdgeController.GetHistoricalEdgeByRelationship(
            OrgId, ProjectId, OriginId, DestinationId, PointInTimeConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetHistoricalEdgeByRelationship_ThrowsException_WhenBusinessThrows()
    {
        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoricalEdge(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>(),
                         It.IsAny<DateTime?>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _historicalEdgeController.GetHistoricalEdgeByRelationship(
            OrgId, ProjectId, OriginId, DestinationId, null, true));
    }

    [Fact]
    public async Task GetHistoricalEdgeByRelationship_PassesOriginAndDestinationIdsToBusinessLayer()
    {
        var expected = new HistoricalEdgeResponseDto();

        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoricalEdge(
                         OrgId, null, OriginId, DestinationId, PointInTimeConst, false))
                     .ReturnsAsync(expected);

        await _historicalEdgeController.GetHistoricalEdgeByRelationship(
            OrgId, ProjectId, OriginId, DestinationId, PointInTimeConst, hideArchived: false);

        _mockHistoricalEdgeBusiness.Verify(b => b.GetHistoricalEdge(
            OrgId, null, OriginId, DestinationId, PointInTimeConst, false), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetEdgeHistoryById Tests
    // =========================================================================

    #region GetEdgeHistoryById Tests

    [Fact]
    public async Task GetEdgeHistoryById_Returns200_WithHistory()
    {
        var expected = new List<HistoricalEdgeResponseDto> { new(), new() };

        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoryForEdge(OrgId, EdgeIdConst, null, null))
                     .ReturnsAsync(expected);

        var result = (await _historicalEdgeController.GetEdgeHistoryById(
            OrgId, ProjectId, EdgeIdConst)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetEdgeHistoryById_Returns200_WithEmptyList()
    {
        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoryForEdge(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ReturnsAsync([]);

        var result = (await _historicalEdgeController.GetEdgeHistoryById(
            OrgId, ProjectId, EdgeIdConst)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<HistoricalEdgeResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetEdgeHistoryById_ThrowsException_WhenBusinessThrows()
    {
        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoryForEdge(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _historicalEdgeController.GetEdgeHistoryById(
            OrgId, ProjectId, EdgeIdConst));
    }

    [Fact]
    public async Task GetEdgeHistoryById_PassesIdsToBusinessLayer()
    {
        var expected = new List<HistoricalEdgeResponseDto> { new() };

        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoryForEdge(OrgId, EdgeIdConst, null, null))
                     .ReturnsAsync(expected);

        await _historicalEdgeController.GetEdgeHistoryById(OrgId, ProjectId, EdgeIdConst);

        _mockHistoricalEdgeBusiness.Verify(b => b.GetHistoryForEdge(OrgId, EdgeIdConst, null, null), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetEdgeHistoryByRelationship Tests
    // =========================================================================

    #region GetEdgeHistoryByRelationship Tests

    [Fact]
    public async Task GetEdgeHistoryByRelationship_Returns200_WithHistory()
    {
        var expected = new List<HistoricalEdgeResponseDto> { new(), new() };

        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoryForEdge(OrgId, null, OriginId, DestinationId))
                     .ReturnsAsync(expected);

        var result = (await _historicalEdgeController.GetEdgeHistoryByRelationship(
            OrgId, ProjectId, OriginId, DestinationId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetEdgeHistoryByRelationship_ThrowsException_WhenBusinessThrows()
    {
        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoryForEdge(
                         It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _historicalEdgeController.GetEdgeHistoryByRelationship(
            OrgId, ProjectId, OriginId, DestinationId));
    }

    [Fact]
    public async Task GetEdgeHistoryByRelationship_PassesOriginAndDestinationIdsToBusinessLayer()
    {
        var expected = new List<HistoricalEdgeResponseDto> { new() };

        _mockHistoricalEdgeBusiness.Setup(b => b.GetHistoryForEdge(OrgId, null, OriginId, DestinationId))
                     .ReturnsAsync(expected);

        await _historicalEdgeController.GetEdgeHistoryByRelationship(OrgId, ProjectId, OriginId, DestinationId);

        _mockHistoricalEdgeBusiness.Verify(b => b.GetHistoryForEdge(OrgId, null, OriginId, DestinationId), Times.Once);
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void HistoricalEdgeController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(HistoricalEdgeController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void GetAllHistoricalEdges_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(HistoricalEdgeController.GetAllHistoricalEdges),
            "organizationId", "projectId", "dataSourceId", "pointInTime", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "edge");
    }

    [Fact]
    public void GetHistoricalEdgeById_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(HistoricalEdgeController.GetHistoricalEdgeById),
            "organizationId", "projectId", "edgeId", "pointInTime", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "edge");
    }

    [Fact]
    public void GetHistoricalEdgeByRelationship_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(HistoricalEdgeController.GetHistoricalEdgeByRelationship),
            "organizationId", "projectId", "originId", "destinationId", "pointInTime", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "edge");
    }

    [Fact]
    public void GetEdgeHistoryById_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(HistoricalEdgeController.GetEdgeHistoryById),
            "organizationId", "projectId", "edgeId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "edge");
    }

    [Fact]
    public void GetEdgeHistoryByRelationship_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(HistoricalEdgeController.GetEdgeHistoryByRelationship),
            "organizationId", "projectId", "originId", "destinationId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "edge");
    }

    #endregion

    // =========================================================================
    // Helpers for Auth / Middleware Metadata Tests
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(HistoricalEdgeController).GetMethods()
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
}