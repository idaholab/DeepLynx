using System.Reflection;
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
///     Unit tests for the V2 actions of <see cref="DataSourceOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class DataSourceOrganizationControllerTests : IDisposable
{
    private const long OrgId = 1L;
    private const long DataSourceId = 5L;
    private const long UserId = 10L;
    private const long ProjectIdA = 20L;
    private const long ProjectIdB = 21L;
    private readonly DataSourceOrganizationController _controller;
    private readonly Mock<IDataSourceBusiness> _mockBusiness;
    private readonly Mock<ILogger<DataSourceProjectController>> _mockLogger;

    public DataSourceOrganizationControllerTests()
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
    // GetAllDataSources Tests
    // =========================================================================

    #region GetAllDataSources Tests

    [Fact]
    public async Task GetAllDataSources_Returns200_WithList()
    {
        var projectIds = new[] { ProjectIdA, ProjectIdB };
        var expected = new List<DataSourceResponseDto> { new(), new() };

        _mockBusiness.Setup(b => b.GetAllDataSources(
                UserId, OrgId,  It.Is<long[]>(x => x.SequenceEqual(projectIds)), true))
            .ReturnsAsync(expected);

        var result = (await _controller.GetAllDataSources(OrgId, projectIds)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllDataSources_Returns200_WithEmptyList()
    {
        _mockBusiness.Setup(b => b.GetAllDataSources(
                It.IsAny<long>(),It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        var result = (await _controller.GetAllDataSources(OrgId, null)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<DataSourceResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllDataSources_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetAllDataSources(
                It.IsAny<long>(),It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetAllDataSources(OrgId, null));
    }

    [Fact]
    public async Task GetAllDataSources_PassesFiltersToBusinessLayer()
    {
        var projectIds = new[] { ProjectIdA };
        _mockBusiness.Setup(b => b.GetAllDataSources(
                UserId, OrgId, It.Is<long[]>(x => x.SequenceEqual(projectIds)), false))
            .ReturnsAsync([]);

        await _controller.GetAllDataSources(OrgId, projectIds, false);

        _mockBusiness.Verify(b => b.GetAllDataSources(
            UserId, OrgId, It.Is<long[]>(x => x.SequenceEqual(projectIds)), false), Times.Once);
    }

    [Fact]
    public void GetAllDataSources_HasHttpGetAndReadDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.GetAllDataSources),
            "organizationId", "projectIds", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "data_source");
    }

    #endregion

    // =========================================================================
    // GetDataSource Tests
    // =========================================================================

    #region GetDataSource Tests

    [Fact]
    public async Task GetDataSource_Returns200_WithDataSource()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.GetDataSource(OrgId, null, DataSourceId, true))
            .ReturnsAsync(expected);

        var result = (await _controller.GetDataSource(OrgId, DataSourceId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetDataSource_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetDataSource(OrgId, DataSourceId));
    }

    [Fact]
    public async Task GetDataSource_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.GetDataSource(OrgId, null, DataSourceId, true))
            .ReturnsAsync(expected);

        await _controller.GetDataSource(OrgId, DataSourceId);

        _mockBusiness.Verify(b => b.GetDataSource(OrgId, null, DataSourceId, true), Times.Once);
    }

    [Fact]
    public void GetDataSource_HasHttpGetAndReadDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.GetDataSource),
            "organizationId", "dataSourceId", "hideArchived");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "data_source");
    }

    #endregion

    // =========================================================================
    // GetDefaultDataSource Tests
    // =========================================================================

    #region GetDefaultDataSource Tests

    [Fact]
    public async Task GetDefaultDataSource_Returns200_WithDataSource()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.GetDefaultDataSource(OrgId, null))
            .ReturnsAsync(expected);

        var result = (await _controller.GetDefaultDataSource(OrgId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetDefaultDataSource_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetDefaultDataSource(It.IsAny<long>(), It.IsAny<long?>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetDefaultDataSource(OrgId));
    }

    [Fact]
    public async Task GetDefaultDataSource_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.GetDefaultDataSource(OrgId, null))
            .ReturnsAsync(expected);

        await _controller.GetDefaultDataSource(OrgId);

        _mockBusiness.Verify(b => b.GetDefaultDataSource(OrgId, null), Times.Once);
    }

    [Fact]
    public void GetDefaultDataSource_HasHttpGetAndReadDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.GetDefaultDataSource),
            "organizationId");

        AssertHasHttpAttribute(method, "HttpGetAttribute");
        AssertHasAuthAttribute(method, "read", "data_source");
    }

    #endregion

    // =========================================================================
    // CreateDataSource Tests
    // =========================================================================

    #region CreateDataSource Tests

    [Fact]
    public async Task CreateDataSource_Returns200_WithDataSource()
    {
        var dto = new CreateDataSourceRequestDto();
        var expected = new DataSourceResponseDto();

        _mockBusiness.Setup(b => b.CreateDataSource(OrgId, null, UserId, dto))
            .ReturnsAsync(expected);

        var result = (await _controller.CreateDataSource(OrgId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateDataSource_ThrowsException_WhenBusinessThrows()
    {
        var dto = new CreateDataSourceRequestDto();
        _mockBusiness.Setup(b => b.CreateDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(),
                It.IsAny<CreateDataSourceRequestDto>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.CreateDataSource(OrgId, dto));
    }

    [Fact]
    public async Task CreateDataSource_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var dto = new CreateDataSourceRequestDto();
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.CreateDataSource(OrgId, null, UserId, dto))
            .ReturnsAsync(expected);

        await _controller.CreateDataSource(OrgId, dto);

        _mockBusiness.Verify(b => b.CreateDataSource(OrgId, null, UserId, dto), Times.Once);
    }

    [Fact]
    public void CreateDataSource_HasHttpPostAndWriteDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.CreateDataSource),
            "organizationId", "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasAuthAttribute(method, "write", "data_source");
    }

    #endregion

    // =========================================================================
    // UpdateDataSource Tests
    // =========================================================================

    #region UpdateDataSource Tests

    [Fact]
    public async Task UpdateDataSource_Returns200_WithUpdatedDataSource()
    {
        var dto = new UpdateDataSourceRequestDto();
        var expected = new DataSourceResponseDto();

        _mockBusiness.Setup(b => b.UpdateDataSource(OrgId, null, UserId, DataSourceId, dto))
            .ReturnsAsync(expected);

        var result = (await _controller.UpdateDataSource(OrgId, DataSourceId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateDataSource_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.UpdateDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<long>(),
                It.IsAny<UpdateDataSourceRequestDto>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.UpdateDataSource(
            OrgId, DataSourceId, new UpdateDataSourceRequestDto()));
    }

    [Fact]
    public async Task UpdateDataSource_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var dto = new UpdateDataSourceRequestDto();
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.UpdateDataSource(OrgId, null, UserId, DataSourceId, dto))
            .ReturnsAsync(expected);

        await _controller.UpdateDataSource(OrgId, DataSourceId, dto);

        _mockBusiness.Verify(b => b.UpdateDataSource(OrgId, null, UserId, DataSourceId, dto), Times.Once);
    }

    [Fact]
    public void UpdateDataSource_HasHttpPutAndUpdateDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.UpdateDataSource),
            "organizationId", "dataSourceId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasAuthAttribute(method, "update", "data_source");
    }

    #endregion

    // =========================================================================
    // DeleteDataSource Tests
    // =========================================================================

    #region DeleteDataSource Tests

    [Fact]
    public async Task DeleteDataSource_Returns200_WithBooleanResult()
    {
        _mockBusiness.Setup(b => b.DeleteDataSource(OrgId, null, DataSourceId))
            .ReturnsAsync(true);

        var result = await _controller.DeleteDataSource(OrgId, DataSourceId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteDataSource_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.DeleteDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.DeleteDataSource(OrgId, DataSourceId));
    }

    [Fact]
    public async Task DeleteDataSource_PassesNullProjectIdToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.DeleteDataSource(OrgId, null, DataSourceId))
            .ReturnsAsync(true);

        await _controller.DeleteDataSource(OrgId, DataSourceId);

        _mockBusiness.Verify(b => b.DeleteDataSource(OrgId, null, DataSourceId), Times.Once);
    }

    [Fact]
    public void DeleteDataSource_HasHttpDeleteAndWriteDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.DeleteDataSource),
            "organizationId", "dataSourceId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasAuthAttribute(method, "write", "data_source");
    }

    #endregion

    // =========================================================================
    // ArchiveDataSource Tests
    // =========================================================================

    #region ArchiveDataSource Tests

    [Fact]
    public async Task ArchiveDataSource_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.ArchiveDataSource(OrgId, null, UserId, DataSourceId))
            .ReturnsAsync(true);

        var result = await _controller.ArchiveDataSource(
            OrgId, DataSourceId, true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.ArchiveDataSource(OrgId, null, UserId, DataSourceId), Times.Once);
        _mockBusiness.Verify(b => b.UnarchiveDataSource(
            It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveDataSource_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.UnarchiveDataSource(OrgId, null, UserId, DataSourceId))
            .ReturnsAsync(true);

        var result = await _controller.ArchiveDataSource(
            OrgId, DataSourceId, false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.UnarchiveDataSource(OrgId, null, UserId, DataSourceId), Times.Once);
        _mockBusiness.Verify(b => b.ArchiveDataSource(
            It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveDataSource_PropagatesException_ForMiddlewareToHandle()
    {
        _mockBusiness.Setup(b => b.ArchiveDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _controller.ArchiveDataSource(OrgId, DataSourceId, true));
    }

    [Fact]
    public void ArchiveDataSource_HasHttpPatchAndUpdateDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.ArchiveDataSource),
            "organizationId", "dataSourceId", "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "data_source");
    }

    #endregion

    // =========================================================================
    // SetDefaultDataSource Tests
    // =========================================================================

    #region SetDefaultDataSource Tests

    [Fact]
    public async Task SetDefaultDataSource_Returns200_WithDataSource()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.SetDefaultDataSource(OrgId, null, UserId, DataSourceId))
            .ReturnsAsync(expected);

        var result = (await _controller.SetDefaultDataSource(OrgId, DataSourceId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task SetDefaultDataSource_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.SetDefaultDataSource(
                It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.SetDefaultDataSource(OrgId, DataSourceId));
    }

    [Fact]
    public async Task SetDefaultDataSource_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.SetDefaultDataSource(OrgId, null, UserId, DataSourceId))
            .ReturnsAsync(expected);

        await _controller.SetDefaultDataSource(OrgId, DataSourceId);

        _mockBusiness.Verify(b => b.SetDefaultDataSource(OrgId, null, UserId, DataSourceId), Times.Once);
    }

    [Fact]
    public void SetDefaultDataSource_HasHttpPatchAndWriteDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceOrganizationController.SetDefaultDataSource),
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