using deeplynx.api.Controllers.V2;
using deeplynx.interfaces;
using deeplynx.models.MetricsDTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]


public class MetricsOrganizationControllerTests : IDisposable
{
    private readonly Mock<IMetricsBusiness> _mockMetricsBusiness;
    private readonly Mock<ILogger<MetricsController>> _mockLogger;
    private readonly MetricsOrganizationController _metricsOrganizationController;

    private const long OrgId = 1L;
    private static readonly StorageSizeDto StorageSize = new() { Bytes = 123456789L };
    private const int DataSourceCount = 7;
    private const int RecordCount = 42;
    private const int FileCount = 15;
    private const int DataModalityCount = 3;
    private static readonly long[] ProjectIds = { 100L, 200L };

    public MetricsOrganizationControllerTests()
    {
        _mockMetricsBusiness = new Mock<IMetricsBusiness>();
        _mockLogger = new Mock<ILogger<MetricsController>>();

        _metricsOrganizationController = new MetricsOrganizationController(
            _mockMetricsBusiness.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        // No UserContextStorage state is read by MetricsOrganizationController, but reset
        // defensively in case that changes and to stay consistent with sibling test classes.
    }

    // =========================================================================
    // GetOrganizationStorageSize Tests
    // =========================================================================

    #region GetOrganizationStorageSize Tests

    [Fact]
    public async Task GetOrganizationStorageSize_Returns200_WithByteCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationStorageSize(OrgId))
            .ReturnsAsync(StorageSize);

        // Act
        var result = (await _metricsOrganizationController.GetOrganizationStorageSize(OrgId)).Result as OkObjectResult;
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(StorageSize, result.Value);
    }

    [Fact]
    public async Task GetOrganizationStorageSize_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationStorageSize(It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _metricsOrganizationController.GetOrganizationStorageSize(OrgId));
    }

    [Fact]
    public async Task GetOrganizationStorageSize_PassesOrganizationIdToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationStorageSize(OrgId))
            .ReturnsAsync(StorageSize);

        // Act
        await _metricsOrganizationController.GetOrganizationStorageSize(OrgId);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetOrganizationStorageSize(OrgId), Times.Once);
    }

    [Fact]
    public void GetOrganizationStorageSize_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsOrganizationController.GetOrganizationStorageSize),
            "organizationId");

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
            .Setup(b => b.GetOrganizationDataSourceCount(OrgId, ProjectIds, true))
            .ReturnsAsync(DataSourceCount);

        // Act
        var result = (await _metricsOrganizationController.GetDataSourceCount(
            OrgId, ProjectIds, true)).Result as OkObjectResult;

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
            .Setup(b => b.GetOrganizationDataSourceCount(
                It.IsAny<long>(), It.IsAny<long[]?>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _metricsOrganizationController.GetDataSourceCount(
            OrgId, ProjectIds, true));
    }

    [Fact]
    public async Task GetDataSourceCount_DefaultsHideArchivedToTrue()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationDataSourceCount(OrgId, ProjectIds, true))
            .ReturnsAsync(DataSourceCount);

        // Act
        await _metricsOrganizationController.GetDataSourceCount(OrgId, ProjectIds);

        // Assert
        _mockMetricsBusiness.Verify(
            b => b.GetOrganizationDataSourceCount(OrgId, ProjectIds, true), Times.Once);
    }

    [Fact]
    public async Task GetDataSourceCount_PassesAllParametersToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationDataSourceCount(OrgId, ProjectIds, false))
            .ReturnsAsync(DataSourceCount);

        // Act
        await _metricsOrganizationController.GetDataSourceCount(OrgId, ProjectIds, false);

        // Assert
        _mockMetricsBusiness.Verify(
            b => b.GetOrganizationDataSourceCount(OrgId, ProjectIds, false), Times.Once);
    }

    [Fact]
    public void GetDataSourceCount_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsOrganizationController.GetDataSourceCount),
            "organizationId",
            "projectIds",
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "count");
    }

    #endregion

    // =========================================================================
    // GetOrganizationRecordCount Tests
    // =========================================================================

    #region GetOrganizationRecordCount Tests

    [Fact]
    public async Task GetOrganizationRecordCount_Returns200_WithCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount(OrgId, ProjectIds, false))
            .ReturnsAsync(RecordCount);

        // Act
        var result = (await _metricsOrganizationController.GetOrganizationRecordCount(
            OrgId, ProjectIds, false)).Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(RecordCount, result.Value);
    }

    [Fact]
    public async Task GetOrganizationRecordCount_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount(OrgId, ProjectIds, false))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _metricsOrganizationController.GetOrganizationRecordCount(
            OrgId, ProjectIds, false));
    }

    [Fact]
    public async Task GetOrganizationRecordCount_PassesHideArchivedFlagToBusinessLayer()
    {
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount(OrgId, ProjectIds, false))
            .ReturnsAsync(RecordCount);

        // Act
        await _metricsOrganizationController.GetOrganizationRecordCount(
            OrgId, ProjectIds, hideArchived: false);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetRecordCount(OrgId, ProjectIds, false), Times.Once);
    }

    [Fact]
    public async Task GetOrganizationRecordCount_PassesOrganizationIdAndProjectIdsThrough()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount(OrgId, ProjectIds, false))
            .ReturnsAsync(RecordCount);

        // Act
        await _metricsOrganizationController.GetOrganizationRecordCount(OrgId, ProjectIds, false);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetRecordCount(OrgId, ProjectIds, false), Times.Once);
    }

    [Fact]
    public void GetOrganizationRecordCount_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsOrganizationController.GetOrganizationRecordCount),
            "organizationId",
            "projectIds",
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "records/count");
    }

    #endregion

    // =========================================================================
    // GetOrganizationFileCount Tests
    // =========================================================================

    #region GetOrganizationFileCount Tests

    [Fact]
    public async Task GetOrganizationFileCount_Returns200_WithCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetFileCount(OrgId, ProjectIds, false))
            .ReturnsAsync(FileCount);

        // Act
        var result = (await _metricsOrganizationController.GetOrganizationFileCount(
            OrgId, ProjectIds, false)).Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(FileCount, result.Value);
    }

    [Fact]
    public async Task GetOrganizationFileCount_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetFileCount(OrgId, ProjectIds, false))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _metricsOrganizationController.GetOrganizationFileCount(
            OrgId, ProjectIds, false));
    }

    [Fact]
    public async Task GetOrganizationFileCount_PassesHideArchivedFlagToBusinessLayer()
    {
        _mockMetricsBusiness
            .Setup(b => b.GetFileCount(OrgId, ProjectIds, false))
            .ReturnsAsync(FileCount);

        // Act
        await _metricsOrganizationController.GetOrganizationFileCount(
            OrgId, ProjectIds, hideArchived: false);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetFileCount(OrgId, ProjectIds, false), Times.Once);
    }

    [Fact]
    public void GetOrganizationFileCount_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(MetricsOrganizationController.GetOrganizationFileCount),
            "organizationId",
            "projectIds",
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "files/count");
    }

    #endregion

    // =========================================================================
    // GetOrganizationDataModalityCount Tests
    // =========================================================================

    #region GetOrganizationDataModalityCount Tests

    [Fact]
    public async Task GetOrganizationDataModalityCount_Returns200_WithCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationDataModalityCount(OrgId, null))
            .ReturnsAsync(DataModalityCount);

        // Act
        var result = (await _metricsOrganizationController.GetOrganizationDataModalityCount(OrgId))
            .Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(DataModalityCount, result.Value);
    }

    [Fact]
    public async Task GetOrganizationDataModalityCount_PropagatesException_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationDataModalityCount(It.IsAny<long>(), null))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _metricsOrganizationController.GetOrganizationDataModalityCount(OrgId));
    }

    [Fact]
    public async Task GetOrganizationDataModalityCount_PassesOrganizationIdAndNullToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetOrganizationDataModalityCount(OrgId, null))
            .ReturnsAsync(DataModalityCount);

        // Act
        await _metricsOrganizationController.GetOrganizationDataModalityCount(OrgId);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetOrganizationDataModalityCount(OrgId, null), Times.Once);
    }

    [Fact]
    public void GetOrganizationDataModalityCount_HasHttpGetAndReadDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(MetricsOrganizationController.GetOrganizationDataModalityCount),
            "organizationId");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "modalities/count");
        AssertHasAuthAttribute(method, "read", "data_source");
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

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(MetricsOrganizationController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}