using System.Reflection;
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
///     Unit tests for the V2 actions of <see cref="DataSourceOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class DataSourceOrganizationControllerTestsV2 : IDisposable
{
    private const long OrgId = 1L;
    private const long DataSourceId = 5L;
    private const long UserId = 10L;
    private const long ProjectIdA = 20L;
    private const long ProjectIdB = 21L;
    private readonly DataSourceOrganizationController _controller;
    private readonly Mock<IDataSourceBusiness> _mockBusiness;
    private readonly Mock<ILogger<DataSourceProjectController>> _mockLogger;

    public DataSourceOrganizationControllerTestsV2()
    {
        _mockBusiness = new Mock<IDataSourceBusiness>();
        _mockLogger = new Mock<ILogger<DataSourceProjectController>>();

        _controller = new DataSourceOrganizationController(
            _mockBusiness.Object,
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
    // Test Helpers
    // =========================================================================

    private static MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(DataSourceOrganizationController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }

    private static void AssertHasHttpAttribute(
        MethodInfo method,
        string expectedAttributeName)
    {
        Assert.Contains(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == expectedAttributeName);
    }

    private static void AssertHasAuthAttribute(
        MethodInfo method,
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

    // =========================================================================
    // GetAllDataSourcesV2 Tests
    // =========================================================================

    #region GetAllDataSourcesV2 Tests

    [Fact]
    public async Task GetAllDataSourcesV2_Returns200_WithList()
    {
        var projectIds = new[] { ProjectIdA, ProjectIdB };
        var expected = new List<DataSourceResponseDto> { new(), new() };

        _mockBusiness.Setup(b => b.GetAllDataSources(
                OrgId, It.Is<long[]>(x => x.SequenceEqual(projectIds)), true))
            .ReturnsAsync(expected);

        var result = (await _controller.GetAllDataSourcesV2(OrgId, projectIds)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllDataSourcesV2_Returns200_WithEmptyList()
    {
        _mockBusiness.Setup(b => b.GetAllDataSources(
                It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        var result = (await _controller.GetAllDataSourcesV2(OrgId, null)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<DataSourceResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllDataSourcesV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetAllDataSources(
                It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetAllDataSourcesV2(OrgId, null));
    }

    [Fact]
    public async Task GetAllDataSourcesV2_PassesFiltersToBusinessLayer()
    {
        var projectIds = new[] { ProjectIdA };
        _mockBusiness.Setup(b => b.GetAllDataSources(
                OrgId, It.Is<long[]>(x => x.SequenceEqual(projectIds)), false))
            .ReturnsAsync([]);

        await _controller.GetAllDataSourcesV2(OrgId, projectIds, false);

        _mockBusiness.Verify(b => b.GetAllDataSources(
            OrgId, It.Is<long[]>(x => x.SequenceEqual(projectIds)), false), Times.Once);
    }

    [Fact]
    public void GetAllDataSourcesV2_HasHttpGetAndReadDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.GetAllDataSourcesV2),
            "organizationId", "projectIds", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "data_source");
    }

    #endregion

    // =========================================================================
    // GetDataSourceV2 Tests
    // =========================================================================

    #region GetDataSourceV2 Tests

    [Fact]
    public async Task GetDataSourceV2_Returns200_WithDataSource()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.GetDataSource(OrgId, null, DataSourceId, true))
            .ReturnsAsync(expected);

        var result = (await _controller.GetDataSourceV2(OrgId, DataSourceId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetDataSourceV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetDataSourceV2(OrgId, DataSourceId));
    }

    [Fact]
    public async Task GetDataSourceV2_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.GetDataSource(OrgId, null, DataSourceId, true))
            .ReturnsAsync(expected);

        await _controller.GetDataSourceV2(OrgId, DataSourceId);

        _mockBusiness.Verify(b => b.GetDataSource(OrgId, null, DataSourceId, true), Times.Once);
    }

    [Fact]
    public void GetDataSourceV2_HasHttpGetAndReadDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.GetDataSourceV2),
            "organizationId", "dataSourceId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "data_source");
    }

    #endregion

    // =========================================================================
    // GetDefaultDataSourceV2 Tests
    // =========================================================================

    #region GetDefaultDataSourceV2 Tests

    [Fact]
    public async Task GetDefaultDataSourceV2_Returns200_WithDataSource()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.GetDefaultDataSource(OrgId, null))
            .ReturnsAsync(expected);

        var result = (await _controller.GetDefaultDataSourceV2(OrgId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetDefaultDataSourceV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetDefaultDataSource(It.IsAny<long>(), It.IsAny<long?>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetDefaultDataSourceV2(OrgId));
    }

    [Fact]
    public async Task GetDefaultDataSourceV2_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.GetDefaultDataSource(OrgId, null))
            .ReturnsAsync(expected);

        await _controller.GetDefaultDataSourceV2(OrgId);

        _mockBusiness.Verify(b => b.GetDefaultDataSource(OrgId, null), Times.Once);
    }

    [Fact]
    public void GetDefaultDataSourceV2_HasHttpGetAndReadDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.GetDefaultDataSourceV2),
            "organizationId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "data_source");
    }

    #endregion

    // =========================================================================
    // CreateDataSourceV2 Tests
    // =========================================================================

    #region CreateDataSourceV2 Tests

    [Fact]
    public async Task CreateDataSourceV2_Returns200_WithDataSource()
    {
        var dto = new CreateDataSourceRequestDto();
        var expected = new DataSourceResponseDto();

        _mockBusiness.Setup(b => b.CreateDataSource(OrgId, null, UserId, dto))
            .ReturnsAsync(expected);

        var result = (await _controller.CreateDataSourceV2(OrgId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateDataSourceV2_ThrowsException_WhenBusinessThrows()
    {
        var dto = new CreateDataSourceRequestDto();
        _mockBusiness.Setup(b => b.CreateDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(),
                It.IsAny<CreateDataSourceRequestDto>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.CreateDataSourceV2(OrgId, dto));
    }

    [Fact]
    public async Task CreateDataSourceV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var dto = new CreateDataSourceRequestDto();
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.CreateDataSource(OrgId, null, UserId, dto))
            .ReturnsAsync(expected);

        await _controller.CreateDataSourceV2(OrgId, dto);

        _mockBusiness.Verify(b => b.CreateDataSource(OrgId, null, UserId, dto), Times.Once);
    }

    [Fact]
    public void CreateDataSourceV2_HasHttpPostAndWriteDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.CreateDataSourceV2),
            "organizationId", "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "data_source");
    }

    #endregion

    // =========================================================================
    // UpdateDataSourceV2 Tests
    // =========================================================================

    #region UpdateDataSourceV2 Tests

    [Fact]
    public async Task UpdateDataSourceV2_Returns200_WithUpdatedDataSource()
    {
        var dto = new UpdateDataSourceRequestDto();
        var expected = new DataSourceResponseDto();

        _mockBusiness.Setup(b => b.UpdateDataSource(OrgId, null, UserId, DataSourceId, dto))
            .ReturnsAsync(expected);

        var result = (await _controller.UpdateDataSourceV2(OrgId, DataSourceId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateDataSourceV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.UpdateDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<long>(),
                It.IsAny<UpdateDataSourceRequestDto>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.UpdateDataSourceV2(
            OrgId, DataSourceId, new UpdateDataSourceRequestDto()));
    }

    [Fact]
    public async Task UpdateDataSourceV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var dto = new UpdateDataSourceRequestDto();
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.UpdateDataSource(OrgId, null, UserId, DataSourceId, dto))
            .ReturnsAsync(expected);

        await _controller.UpdateDataSourceV2(OrgId, DataSourceId, dto);

        _mockBusiness.Verify(b => b.UpdateDataSource(OrgId, null, UserId, DataSourceId, dto), Times.Once);
    }

    [Fact]
    public void UpdateDataSourceV2_HasHttpPutAndUpdateDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.UpdateDataSourceV2),
            "organizationId", "dataSourceId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "data_source");
    }

    #endregion

    // =========================================================================
    // DeleteDataSourceV2 Tests
    // =========================================================================

    #region DeleteDataSourceV2 Tests

    [Fact]
    public async Task DeleteDataSourceV2_Returns200_WithBooleanResult()
    {
        _mockBusiness.Setup(b => b.DeleteDataSource(OrgId, null, DataSourceId))
            .ReturnsAsync(true);

        var result = await _controller.DeleteDataSourceV2(OrgId, DataSourceId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteDataSourceV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.DeleteDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.DeleteDataSourceV2(OrgId, DataSourceId));
    }

    [Fact]
    public async Task DeleteDataSourceV2_PassesNullProjectIdToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.DeleteDataSource(OrgId, null, DataSourceId))
            .ReturnsAsync(true);

        await _controller.DeleteDataSourceV2(OrgId, DataSourceId);

        _mockBusiness.Verify(b => b.DeleteDataSource(OrgId, null, DataSourceId), Times.Once);
    }

    [Fact]
    public void DeleteDataSourceV2_HasHttpDeleteAndWriteDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.DeleteDataSourceV2),
            "organizationId", "dataSourceId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "data_source");
    }

    #endregion

    // =========================================================================
    // ArchiveDataSourceV2 Tests
    // =========================================================================

    #region ArchiveDataSourceV2 Tests

    [Fact]
    public async Task ArchiveDataSourceV2_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.ArchiveDataSource(OrgId, null, UserId, DataSourceId))
            .ReturnsAsync(true);

        var result = await _controller.ArchiveDataSourceV2(
            OrgId, DataSourceId, true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.ArchiveDataSource(OrgId, null, UserId, DataSourceId), Times.Once);
        _mockBusiness.Verify(b => b.UnarchiveDataSource(
            It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveDataSourceV2_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.UnarchiveDataSource(OrgId, null, UserId, DataSourceId))
            .ReturnsAsync(true);

        var result = await _controller.ArchiveDataSourceV2(
            OrgId, DataSourceId, false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.UnarchiveDataSource(OrgId, null, UserId, DataSourceId), Times.Once);
        _mockBusiness.Verify(b => b.ArchiveDataSource(
            It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveDataSourceV2_PropagatesException_ForMiddlewareToHandle()
    {
        _mockBusiness.Setup(b => b.ArchiveDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _controller.ArchiveDataSourceV2(OrgId, DataSourceId, true));
    }

    [Fact]
    public void ArchiveDataSourceV2_HasHttpPatchAndUpdateDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.ArchiveDataSourceV2),
            "organizationId", "dataSourceId", "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "data_source");
    }

    #endregion

    // =========================================================================
    // SetDefaultDataSourceV2 Tests
    // =========================================================================

    #region SetDefaultDataSourceV2 Tests

    [Fact]
    public async Task SetDefaultDataSourceV2_Returns200_WithDataSource()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.SetDefaultDataSource(OrgId, null, UserId, DataSourceId))
            .ReturnsAsync(expected);

        var result = (await _controller.SetDefaultDataSourceV2(OrgId, DataSourceId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task SetDefaultDataSourceV2_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.SetDefaultDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.SetDefaultDataSourceV2(OrgId, DataSourceId));
    }

    [Fact]
    public async Task SetDefaultDataSourceV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.SetDefaultDataSource(OrgId, null, UserId, DataSourceId))
            .ReturnsAsync(expected);

        await _controller.SetDefaultDataSourceV2(OrgId, DataSourceId);

        _mockBusiness.Verify(b => b.SetDefaultDataSource(OrgId, null, UserId, DataSourceId), Times.Once);
    }

    [Fact]
    public void SetDefaultDataSourceV2_HasHttpPatchAndWriteDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.SetDefaultDataSourceV2),
            "organizationId", "dataSourceId");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "write", "data_source");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void DataSourceOrganizationController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(DataSourceOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void DataSourceOrganizationController_HasForbidServiceAccountsAttribute()
    {
        Assert.Contains(typeof(DataSourceOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "ForbidServiceAccountsAttribute");
    }

    #endregion
}