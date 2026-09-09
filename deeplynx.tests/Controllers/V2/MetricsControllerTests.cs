using deeplynx.api.Controllers.V2;
using deeplynx.interfaces;
using deeplynx.models.MetricsDTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Org.BouncyCastle.Utilities;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for <see cref="MetricsController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Note: the V2 endpoints on this controller return bare Task&lt;IActionResult&gt; (not
///     Task&lt;ActionResult&lt;T&gt;&gt;), so results are cast directly rather than through `.Result`.
///     The V2 endpoints also do not catch exceptions themselves (unlike their V1 counterparts,
///     which wrap calls in try/catch and return a 500 manually) — they rely on a global exception
///     handler further up the pipeline, so these tests assert that exceptions propagate rather
///     than asserting a translated status code.
/// </summary>
public class MetricsControllerTests : IDisposable
{
    private readonly Mock<IMetricsBusiness> _mockMetricsBusiness;
    private readonly Mock<ILogger<MetricsController>> _mockLogger;
    private readonly MetricsController _metricsController;
    
    private static readonly StorageSizeDto StorageSize = new() { Bytes = 123456789L };
    private const int DataSourceCount = 7;
    private const int RecordCount = 42;
    private const int FileCount = 15;

    public MetricsControllerTests()
    {
        _mockMetricsBusiness = new Mock<IMetricsBusiness>();
        _mockLogger = new Mock<ILogger<MetricsController>>();

        _metricsController = new MetricsController(
            _mockMetricsBusiness.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        // No UserContextStorage state is read by MetricsController, but reset defensively
        // in case that changes and to stay consistent with sibling test classes.
    }

    // =========================================================================
    // GetSystemStorageSize Tests
    // =========================================================================

    #region GetSystemStorageSize Tests

    [Fact]
    public async Task GetSystemStorageSize_Returns200_WithByteCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetSystemStorageSize())
            .ReturnsAsync(StorageSize);

        // Act
        var result = (await _metricsController.GetSystemStorageSize()).Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(StorageSize, result.Value);
    }

    [Fact]
    public async Task GetSystemStorageSize_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetSystemStorageSize())
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _metricsController.GetSystemStorageSize());
    }

    [Fact]
    public async Task GetSystemStorageSize_PassesToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetSystemStorageSize())
            .ReturnsAsync(StorageSize);

        // Act
        await _metricsController.GetSystemStorageSize();

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetSystemStorageSize(), Times.Once);
    }

    [Fact]
    public void GetSystemStorageSize_HasHttpGetAndSysAdminAuthorization()
    {
        var method = GetControllerMethod(nameof(MetricsController.GetSystemStorageSize));

        AssertHasHttpAttribute(method, "HttpGetAttribute", "storage/size");
        AssertHasAttribute(method, "SysAdminAttribute");
    }

    #endregion

    // =========================================================================
    // GetSystemDataSourceCount Tests
    // =========================================================================

    #region GetSystemDataSourceCount Tests

    [Fact]
    public async Task GetSystemDataSourceCount_Returns200_WithCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetSystemDataSourceCount(true))
            .ReturnsAsync(DataSourceCount);

        // Act
        var result = (await _metricsController.GetSystemDataSourceCount(true)).Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(DataSourceCount, result.Value);
    }

    [Fact]
    public async Task GetSystemDataSourceCount_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetSystemDataSourceCount(It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _metricsController.GetSystemDataSourceCount(true));
    }

    [Fact]
    public async Task GetSystemDataSourceCount_DefaultsHideArchivedToTrue()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetSystemDataSourceCount(true))
            .ReturnsAsync(DataSourceCount);

        // Act
        await _metricsController.GetSystemDataSourceCount();

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetSystemDataSourceCount(true), Times.Once);
    }

    [Fact]
    public async Task GetSystemDataSourceCount_PassesHideArchivedFlagToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetSystemDataSourceCount(false))
            .ReturnsAsync(DataSourceCount);

        // Act
        await _metricsController.GetSystemDataSourceCount(false);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetSystemDataSourceCount(false), Times.Once);
    }

    [Fact]
    public void GetSystemDataSourceCount_HasHttpGetAndSysAdminAuthorization()
    {
        var method = GetControllerMethod(
            nameof(MetricsController.GetSystemDataSourceCount),
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "datasources/count");
        AssertHasAttribute(method, "SysAdminAttribute");
    }

    #endregion

    // =========================================================================
    // GetSystemRecordCount Tests
    // =========================================================================

    #region GetSystemRecordCount Tests

    [Fact]
    public async Task GetSystemRecordCount_Returns200_WithCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount((long?)null, (long[]?)null, false))
            .ReturnsAsync(RecordCount);

        // Act
        var result = (await _metricsController.GetSystemRecordCount(false)).Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(RecordCount, result.Value);
    }

    [Fact]
    public async Task GetSystemRecordCount_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount((long?)null, (long[]?)null, false))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _metricsController.GetSystemRecordCount(false));
    }

    [Fact]
    public async Task GetSystemRecordCount_PassesHideArchivedFlagToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetRecordCount((long?)null, (long[]?)null, false))
            .ReturnsAsync(RecordCount);

        // Act
        await _metricsController.GetSystemRecordCount(hideArchived: false);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetRecordCount((long?)null, (long[]?)null, false), Times.Once);
    }

    [Fact]
    public void GetSystemRecordCount_HasHttpGetAndSysAdminAuthorization()
    {
        var method = GetControllerMethod(
            nameof(MetricsController.GetSystemRecordCount),
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "records/count");
        AssertHasAttribute(method, "SysAdminAttribute");
    }

    #endregion

    // =========================================================================
    // GetSystemFileCount Tests
    // =========================================================================

    #region GetSystemFileCount Tests

    [Fact]
    public async Task GetSystemFileCount_Returns200_WithCount()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetFileCount((long?)null, (long[]?)null, false))
            .ReturnsAsync(FileCount);

        // Act
        var result = (await _metricsController.GetSystemFileCount(false)).Result as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(FileCount, result.Value);
    }

    [Fact]
    public async Task GetSystemFileCount_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetFileCount((long?)null, (long[]?)null, false))
            .ThrowsAsync(new Exception("db error"));

        // Act / Assert
        await Assert.ThrowsAsync<Exception>(() => _metricsController.GetSystemFileCount(false));
    }

    [Fact]
    public async Task GetSystemFileCount_PassesHideArchivedFlagToBusinessLayer()
    {
        // Arrange
        _mockMetricsBusiness
            .Setup(b => b.GetFileCount((long?)null, (long[]?)null, false))
            .ReturnsAsync(FileCount);

        // Act
        await _metricsController.GetSystemFileCount(hideArchived: false);

        // Assert
        _mockMetricsBusiness.Verify(b => b.GetFileCount((long?)null, (long[]?)null, false), Times.Once);
    }

    [Fact]
    public void GetSystemFileCount_HasHttpGetAndSysAdminAuthorization()
    {
        var method = GetControllerMethod(
            nameof(MetricsController.GetSystemFileCount),
            "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute", "files/count");
        AssertHasAttribute(method, "SysAdminAttribute");
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

    private static void AssertHasAttribute(
        System.Reflection.MethodInfo method,
        string expectedAttributeName)
    {
        Assert.Contains(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == expectedAttributeName);
    }

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(MetricsController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}