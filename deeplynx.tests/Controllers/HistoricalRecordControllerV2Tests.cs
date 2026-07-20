using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for the V2 actions of <see cref="HistoricalRecordController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class HistoricalRecordControllerTestsV2 : IDisposable
{
    private readonly Mock<IHistoricalRecordBusiness> _mockHistoricalRecordBusiness;
    private readonly Mock<ILogger<HistoricalRecordController>> _mockLogger;
    private readonly HistoricalRecordController _historicalRecordController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long RecordIdConst = 20L;
    private const long DataSourceId = 5L;
    private static readonly DateTime PointInTimeConst = new(2024, 1, 1);

    public HistoricalRecordControllerTestsV2()
    {
        _mockHistoricalRecordBusiness = new Mock<IHistoricalRecordBusiness>();
        _mockLogger = new Mock<ILogger<HistoricalRecordController>>();

        _historicalRecordController = new HistoricalRecordController(
            _mockHistoricalRecordBusiness.Object,
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
    // GetAllHistoricalRecordsV2 Tests
    // =========================================================================

    #region GetAllHistoricalRecordsV2 Tests

    [Fact]
    public async Task GetAllHistoricalRecordsV2_Returns200_WithList()
    {
        var expected = new List<HistoricalRecordResponseDto> { new(), new() };

        _mockHistoricalRecordBusiness.Setup(b => b.GetAllHistoricalRecords(
                         UserId, ProjectId, OrgId, DataSourceId, PointInTimeConst, true, false, false, false))
                     .ReturnsAsync(expected);

        var result = (await _historicalRecordController.GetAllHistoricalRecordsV2(
            OrgId, ProjectId, DataSourceId, PointInTimeConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllHistoricalRecordsV2_Returns200_WithEmptyList()
    {
        _mockHistoricalRecordBusiness.Setup(b => b.GetAllHistoricalRecords(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<DateTime?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _historicalRecordController.GetAllHistoricalRecordsV2(
            OrgId, ProjectId, null, null, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<HistoricalRecordResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllHistoricalRecordsV2_Returns500_OnUnexpectedException()
    {
        _mockHistoricalRecordBusiness.Setup(b => b.GetAllHistoricalRecords(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(),
                         It.IsAny<DateTime?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _historicalRecordController.GetAllHistoricalRecordsV2(
            OrgId, ProjectId, null, null, true));
    }

    [Fact]
    public async Task GetAllHistoricalRecordsV2_PassesIdsFiltersAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        _mockHistoricalRecordBusiness.Setup(b => b.GetAllHistoricalRecords(
                         UserId, ProjectId, OrgId, DataSourceId, PointInTimeConst, false, true, true, true))
                     .ReturnsAsync([]);

        await _historicalRecordController.GetAllHistoricalRecordsV2(
            OrgId, ProjectId, DataSourceId, PointInTimeConst, hideArchived: false);

        _mockHistoricalRecordBusiness.Verify(b => b.GetAllHistoricalRecords(
            UserId, ProjectId, OrgId, DataSourceId, PointInTimeConst, false, true, true, true), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetHistoricalRecordV2 Tests
    // =========================================================================

    #region GetHistoricalRecordV2 Tests

    [Fact]
    public async Task GetHistoricalRecordV2_Returns200_WithRecord()
    {
        var expected = new HistoricalRecordResponseDto();

        _mockHistoricalRecordBusiness.Setup(b => b.GetHistoricalRecord(
                         UserId, RecordIdConst, OrgId, PointInTimeConst, true, false, false, false))
                     .ReturnsAsync(expected);

        var result = (await _historicalRecordController.GetHistoricalRecordV2(
            OrgId, ProjectId, RecordIdConst, PointInTimeConst, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetHistoricalRecordV2_Returns500_OnUnexpectedException()
    {
        _mockHistoricalRecordBusiness.Setup(b => b.GetHistoricalRecord(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<DateTime?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _historicalRecordController.GetHistoricalRecordV2(
            OrgId, ProjectId, RecordIdConst, null, true));
    }

    [Fact]
    public async Task GetHistoricalRecordV2_PassesIdsFiltersAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        var expected = new HistoricalRecordResponseDto();

        _mockHistoricalRecordBusiness.Setup(b => b.GetHistoricalRecord(
                         UserId, RecordIdConst, OrgId, PointInTimeConst, false, true, true, true))
                     .ReturnsAsync(expected);

        await _historicalRecordController.GetHistoricalRecordV2(
            OrgId, ProjectId, RecordIdConst, PointInTimeConst, hideArchived: false);

        _mockHistoricalRecordBusiness.Verify(b => b.GetHistoricalRecord(
            UserId, RecordIdConst, OrgId, PointInTimeConst, false, true, true, true), Times.Once);
    }

    #endregion

    // =========================================================================
    // GetRecordHistoryV2 Tests
    // =========================================================================

    #region GetRecordHistoryV2 Tests

    [Fact]
    public async Task GetRecordHistoryV2_Returns200_WithHistory()
    {
        var expected = new List<HistoricalRecordResponseDto> { new(), new() };

        _mockHistoricalRecordBusiness.Setup(b => b.GetHistoryForRecord(
                         UserId, RecordIdConst, OrgId, false, false, false))
                     .ReturnsAsync(expected);

        var result = (await _historicalRecordController.GetRecordHistoryV2(
            OrgId, ProjectId, RecordIdConst)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRecordHistoryV2_Returns200_WithEmptyList()
    {
        _mockHistoricalRecordBusiness.Setup(b => b.GetHistoryForRecord(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _historicalRecordController.GetRecordHistoryV2(
            OrgId, ProjectId, RecordIdConst)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<HistoricalRecordResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetRecordHistoryV2_Returns500_OnUnexpectedException()
    {
        _mockHistoricalRecordBusiness.Setup(b => b.GetHistoryForRecord(
                         It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                         It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _historicalRecordController.GetRecordHistoryV2(
            OrgId, ProjectId, RecordIdConst));
    }

    [Fact]
    public async Task GetRecordHistoryV2_PassesIdsAndAdminFlagsToBusinessLayer()
    {
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        _mockHistoricalRecordBusiness.Setup(b => b.GetHistoryForRecord(
                         UserId, RecordIdConst, OrgId, true, true, true))
                     .ReturnsAsync([]);

        await _historicalRecordController.GetRecordHistoryV2(OrgId, ProjectId, RecordIdConst);

        _mockHistoricalRecordBusiness.Verify(b => b.GetHistoryForRecord(
            UserId, RecordIdConst, OrgId, true, true, true), Times.Once);
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void HistoricalRecordController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(HistoricalRecordController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void GetAllHistoricalRecordsV2_HasRequiredAuthAttributes()
    {
        var method = GetControllerMethod(
            nameof(HistoricalRecordController.GetAllHistoricalRecordsV2),
            "organizationId", "projectId", "dataSourceId", "pointInTime", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record");
    }

    [Fact]
    public void GetHistoricalRecordV2_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(HistoricalRecordController.GetHistoricalRecordV2),
            "organizationId", "projectId", "recordId", "pointInTime", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record");
        AssertHasSensitivityAttribute(method, "read record");
    }

    [Fact]
    public void GetRecordHistoryV2_HasRequiredAuthAndSensitivityAttributes()
    {
        var method = GetControllerMethod(
            nameof(HistoricalRecordController.GetRecordHistoryV2),
            "organizationId", "projectId", "recordId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "record");
        AssertHasSensitivityAttribute(method, "read record");
    }

    #endregion

    // =========================================================================
    // Helpers for Auth / Middleware Metadata Tests
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(HistoricalRecordController).GetMethods()
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