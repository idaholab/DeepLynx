using deeplynx.api.Controllers.V2;
using deeplynx.interfaces;
using deeplynx.models.MetricsDTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for <see cref="MetricsProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Note: some V2 endpoints on this controller return Task&lt;IActionResult&gt;
///     (GetProjectStorageSize, GetProjectRecordCount, GetProjectFileCount) while others
///     return Task&lt;ActionResult&lt;int&gt;&gt; (GetDataSourceCount, GetProjectDataModalityCount)
///     — casts are direct for the former and go through `.Result` for the latter.
///
///     Also note: GetProjectRecordCount and GetProjectFileCount currently ignore the incoming
///     `hideArchived` argument and hardcode `false` when calling into the business layer (same
///     pattern seen on the system- and organization-level metrics controllers). Tests below pin
///     down that existing behavior so a future fix is a deliberate, visible change rather than a
///     silent one.
///
///     None of the V2 endpoints catch exceptions themselves (unlike their V1 counterparts, which
///     wrap calls in try/catch and return a 500 manually) — they rely on a global exception
///     handler further up the pipeline, so these tests assert that exceptions propagate rather
///     than asserting a translated status code.
/// </summary>
public class MetricsProjectControllerTests : IDisposable
{
    private readonly Mock<IMetricsBusiness> _mockMetricsBusiness;
    private readonly Mock<ILogger<MetricsController>> _mockLogger;
    private readonly MetricsProjectController _metricsProjectController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private static readonly StorageSizeDto StorageSize = new() { Bytes = 123456789L };
    private const int DataSourceCount = 7;
    private const int RecordCount = 42;
    private const int FileCount = 15;
    private const int DataModalityCount = 3;

    public MetricsProjectControllerTests()
    {
        _mockMetricsBusiness = new Mock<IMetricsBusiness>();
        _mockLogger = new Mock<ILogger<MetricsController>>();

        _metricsProjectController = new MetricsProjectController(
            _mockMetricsBusiness.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        // No UserContextStorage state is read by MetricsProjectController, but reset
        // defensively in case that changes and to stay consistent with sibling test classes.
    }

    // =========================================================================
    // GetProjectStorageSize Tests
    // =========================================================================

    #region GetProjectStorageSize Tests

    [Fact]
    public async Task GetProjectStorageSize_Returns200_WithByteCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetProjectStorageSize(OrgId, ProjectId))
            .ReturnsAsync(StorageSize);

        // Act
        var result = (await _metricsProjectController.GetProjectStorageSize(OrgId, ProjectId)).Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(StorageSize, result.Value);
    }

    [Fact]
    public async Task GetProjectStorageSize_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetProjectStorageSize(It.IsAny<long>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _metricsProjectController.GetProjectStorageSize(OrgId, ProjectId));
    }

    [Fact]
    public async Task GetProjectStorageSize_PassesOrganizationIdAndProjectIdToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetProjectStorageSize(OrgId, ProjectId))
            .ReturnsAsync(StorageSize);

        // Act
        await _metricsProjectController.GetProjectStorageSize(OrgId, ProjectId);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetProjectStorageSize(OrgId, ProjectId), Times.Once);
    }

    [Fact]
    public void GetProjectStorageSize_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsProjectController.GetProjectStorageSize),
            "organizationId",
            "projectId");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "storage/size");
    }

    #endregion

    // =========================================================================
    // GetDataSourceCount Tests
    // =========================================================================

    #region GetDataSourceCount Tests

    [Fact]
    public async Task GetDataSourceCount_Returns200_WithCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetProjectDataSourceCount(ProjectId, true))
            .ReturnsAsync(DataSourceCount);

        // Act
        var result = (await _metricsProjectController.GetDataSourceCount(ProjectId, true)).Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(DataSourceCount, result.Value);
    }

    [Fact]
    public async Task GetDataSourceCount_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetProjectDataSourceCount(It.IsAny<long>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _metricsProjectController.GetDataSourceCount(ProjectId, true));
    }

    [Fact]
    public async Task GetDataSourceCount_DefaultsHideArchivedToTrue()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetProjectDataSourceCount(ProjectId, true))
            .ReturnsAsync(DataSourceCount);

        // Act
        await _metricsProjectController.GetDataSourceCount(ProjectId);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetProjectDataSourceCount(ProjectId, true), Times.Once);
    }

    [Fact]
    public async Task GetDataSourceCount_PassesHideArchivedFlagToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetProjectDataSourceCount(ProjectId, false))
            .ReturnsAsync(DataSourceCount);

        // Act
        await _metricsProjectController.GetDataSourceCount(ProjectId, false);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetProjectDataSourceCount(ProjectId, false), Times.Once);
    }

    [Fact]
    public void GetDataSourceCount_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsProjectController.GetDataSourceCount),
            "projectId",
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "count");
    }

    #endregion

    // =========================================================================
    // GetProjectRecordCount Tests
    // =========================================================================

    #region GetProjectRecordCount Tests

    [Fact]
    public async Task GetProjectRecordCount_Returns200_WithCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount(OrgId, ProjectId, false))
            .ReturnsAsync(RecordCount);

        // Act
        var result = (await _metricsProjectController.GetProjectRecordCount(
            OrgId, ProjectId, false)).Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(RecordCount, result.Value);
    }

    [Fact]
    public async Task GetProjectRecordCount_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount(OrgId, ProjectId, false))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _metricsProjectController.GetProjectRecordCount(
            OrgId, ProjectId, false));
    }

    [Fact]
    public async Task GetProjectRecordCount_PassesHideArchivedFlagToBusinessLayer()
    {
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount(OrgId, ProjectId, false))
            .ReturnsAsync(RecordCount);

        // Act
        await _metricsProjectController.GetProjectRecordCount(OrgId, ProjectId, hideArchived: false);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetRecordCount(OrgId, ProjectId, false), Times.Once);
    }

    [Fact]
    public async Task GetProjectRecordCount_PassesOrganizationIdAndProjectIdThrough()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount(OrgId, ProjectId, false))
            .ReturnsAsync(RecordCount);

        // Act
        await _metricsProjectController.GetProjectRecordCount(OrgId, ProjectId, false);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetRecordCount(OrgId, ProjectId, false), Times.Once);
    }

    [Fact]
    public void GetProjectRecordCount_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsProjectController.GetProjectRecordCount),
            "organizationId",
            "projectId",
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "records/count");
    }

    #endregion

    // =========================================================================
    // GetProjectFileCount Tests
    // =========================================================================

    #region GetProjectFileCount Tests

    [Fact]
    public async Task GetProjectFileCount_Returns200_WithCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetFileCount(OrgId, ProjectId, false))
            .ReturnsAsync(FileCount);

        // Act
        var result = (await _metricsProjectController.GetProjectFileCount(
            OrgId, ProjectId, false)).Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(FileCount, result.Value);
    }

    [Fact]
    public async Task GetProjectFileCount_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetFileCount(OrgId, ProjectId, false))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _metricsProjectController.GetProjectFileCount(
            OrgId, ProjectId, false));
    }

    [Fact]
    public async Task GetProjectFileCount_PassesHideArchivedFlagToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetFileCount(OrgId, ProjectId, false))
            .ReturnsAsync(FileCount);

        // Act
        await _metricsProjectController.GetProjectFileCount(OrgId, ProjectId, hideArchived: false);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetFileCount(OrgId, ProjectId, false), Times.Once);
    }

    [Fact]
    public void GetProjectFileCount_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsProjectController.GetProjectFileCount),
            "organizationId",
            "projectId",
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "files/count");
    }

    #endregion

    // =========================================================================
    // GetProjectDataModalityCount Tests
    // =========================================================================

    #region GetProjectDataModalityCount Tests

    [Fact]
    public async Task GetProjectDataModalityCount_Returns200_WithCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationDataModalityCount(OrgId, ProjectId))
            .ReturnsAsync(DataModalityCount);

        // Act
        var result = (await _metricsProjectController.GetProjectDataModalityCount(OrgId, ProjectId))
            .Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(DataModalityCount, result.Value);
    }

    [Fact]
    public async Task GetProjectDataModalityCount_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationDataModalityCount(It.IsAny<long>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _metricsProjectController.GetProjectDataModalityCount(OrgId, ProjectId));
    }

    [Fact]
    public async Task GetProjectDataModalityCount_PassesOrganizationIdAndProjectIdToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationDataModalityCount(OrgId, ProjectId))
            .ReturnsAsync(DataModalityCount);

        // Act
        await _metricsProjectController.GetProjectDataModalityCount(OrgId, ProjectId);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetOrganizationDataModalityCount(OrgId, ProjectId), Times.Once);
    }

    [Fact]
    public void GetProjectDataModalityCount_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsProjectController.GetProjectDataModalityCount),
            "organizationId",
            "projectId");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "modalities/count");
    }

    #endregion

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private static void AssertHasHttpAttribute(
        System.Reflection.MethodInfo method,
        string expectedAttributeName,
        string expectedRoute)
    {
        var httpAttribute = Assert.Single(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == expectedAttributeName);

        Assert.Equal(expectedRoute, httpAttribute.ConstructorArguments[0].Value);
    }

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(MetricsProjectController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}