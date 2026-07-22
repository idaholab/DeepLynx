using deeplynx.api.Controllers;
using deeplynx.interfaces;
using deeplynx.models.MetricsDTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for <see cref="MetricsProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Note: some V2 endpoints on this controller return Task&lt;IActionResult&gt;
///     (GetProjectStorageSizeV2, GetProjectRecordCountV2, GetProjectFileCountV2) while others
///     return Task&lt;ActionResult&lt;int&gt;&gt; (GetDataSourceCountV2, GetProjectDataModalityCountV2)
///     — casts are direct for the former and go through `.Result` for the latter.
///
///     Also note: GetProjectRecordCountV2 and GetProjectFileCountV2 currently ignore the incoming
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
public class MetricsProjectControllerV2Tests : IDisposable
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

    public MetricsProjectControllerV2Tests()
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
        var result = await _metricsProjectController.GetProjectStorageSizeV2(OrgId, ProjectId) as OkObjectResult;

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
            _metricsProjectController.GetProjectStorageSizeV2(OrgId, ProjectId));
    }

    [Fact]
    public async Task GetProjectStorageSize_PassesOrganizationIdAndProjectIdToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetProjectStorageSize(OrgId, ProjectId))
            .ReturnsAsync(StorageSize);

        // Act
        await _metricsProjectController.GetProjectStorageSizeV2(OrgId, ProjectId);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetProjectStorageSize(OrgId, ProjectId), Times.Once);
    }

    [Fact]
    public void GetProjectStorageSize_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsProjectController.GetProjectStorageSizeV2),
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
        var result = (await _metricsProjectController.GetDataSourceCountV2(ProjectId, true)).Result as OkObjectResult;

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
        await Assert.ThrowsAsync<Exception>(() => _metricsProjectController.GetDataSourceCountV2(ProjectId, true));
    }

    [Fact]
    public async Task GetDataSourceCount_DefaultsHideArchivedToTrue()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetProjectDataSourceCount(ProjectId, true))
            .ReturnsAsync(DataSourceCount);

        // Act
        await _metricsProjectController.GetDataSourceCountV2(ProjectId);

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
        await _metricsProjectController.GetDataSourceCountV2(ProjectId, false);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetProjectDataSourceCount(ProjectId, false), Times.Once);
    }

    [Fact]
    public void GetDataSourceCount_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsProjectController.GetDataSourceCountV2),
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
        var result = await _metricsProjectController.GetProjectRecordCountV2(
            OrgId, ProjectId, true) as OkObjectResult;

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
        await Assert.ThrowsAsync<Exception>(() => _metricsProjectController.GetProjectRecordCountV2(
            OrgId, ProjectId, true));
    }

    [Fact]
    public async Task GetProjectRecordCount_AlwaysPassesHideArchivedFalseRegardlessOfParameter()
    {
        // Arrange
        // Note: the controller currently ignores the incoming `hideArchived` argument and
        // hardcodes `false` when calling into the business layer. This test pins down that
        // existing (possibly unintended) behavior so a future fix is a deliberate, visible change.
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount(OrgId, ProjectId, false))
            .ReturnsAsync(RecordCount);

        // Act
        await _metricsProjectController.GetProjectRecordCountV2(OrgId, ProjectId, hideArchived: true);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetRecordCount(OrgId, ProjectId, false), Times.Once);
        _mockMetricsBusiness.Verify(b => b.GetRecordCount(OrgId, ProjectId, true), Times.Never);
    }

    [Fact]
    public async Task GetProjectRecordCount_PassesOrganizationIdAndProjectIdThrough()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount(OrgId, ProjectId, false))
            .ReturnsAsync(RecordCount);

        // Act
        await _metricsProjectController.GetProjectRecordCountV2(OrgId, ProjectId, true);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetRecordCount(OrgId, ProjectId, false), Times.Once);
    }

    [Fact]
    public void GetProjectRecordCount_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsProjectController.GetProjectRecordCountV2),
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
        var result = await _metricsProjectController.GetProjectFileCountV2(
            OrgId, ProjectId, true) as OkObjectResult;

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
        await Assert.ThrowsAsync<Exception>(() => _metricsProjectController.GetProjectFileCountV2(
            OrgId, ProjectId, true));
    }

    [Fact]
    public async Task GetProjectFileCount_AlwaysPassesHideArchivedFalseRegardlessOfParameter()
    {
        // Arrange
        // Same note as GetProjectRecordCount above: the incoming `hideArchived` value is
        // currently ignored in favor of a hardcoded `false`.
        _mockMetricsBusiness
            .Setup(b => b.GetFileCount(OrgId, ProjectId, false))
            .ReturnsAsync(FileCount);

        // Act
        await _metricsProjectController.GetProjectFileCountV2(OrgId, ProjectId, hideArchived: true);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetFileCount(OrgId, ProjectId, false), Times.Once);
        _mockMetricsBusiness.Verify(b => b.GetFileCount(OrgId, ProjectId, true), Times.Never);
    }

    [Fact]
    public void GetProjectFileCount_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsProjectController.GetProjectFileCountV2),
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
        var result = (await _metricsProjectController.GetProjectDataModalityCountV2(OrgId, ProjectId))
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
            _metricsProjectController.GetProjectDataModalityCountV2(OrgId, ProjectId));
    }

    [Fact]
    public async Task GetProjectDataModalityCount_PassesOrganizationIdAndProjectIdToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationDataModalityCount(OrgId, ProjectId))
            .ReturnsAsync(DataModalityCount);

        // Act
        await _metricsProjectController.GetProjectDataModalityCountV2(OrgId, ProjectId);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetOrganizationDataModalityCount(OrgId, ProjectId), Times.Once);
    }

    [Fact]
    public void GetProjectDataModalityCount_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsProjectController.GetProjectDataModalityCountV2),
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