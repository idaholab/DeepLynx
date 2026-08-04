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
///     Unit tests for the V2 actions of <see cref="DataSourceProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class DataSourceProjectControllerTests : IDisposable
{
    private readonly Mock<IDataSourceBusiness> _mockBusiness;
    private readonly Mock<ILogger<DataSourceProjectController>> _mockLogger;
    private readonly DataSourceProjectController _controller;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long DataSourceId = 5L;
    private const long UserId = 10L;

    public DataSourceProjectControllerTests()
    {
        _mockBusiness = new Mock<IDataSourceBusiness>();
        _mockLogger = new Mock<ILogger<DataSourceProjectController>>();

        _controller = new DataSourceProjectController(
            _mockBusiness.Object,
            _mockLogger.Object);

        UserContextStorage.UserId = UserId;
        UserContextStorage.OrganizationId = OrgId;
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
    // GetAllDataSources Tests
    // =========================================================================

    #region GetAllDataSources Tests

    [Fact]
    public async Task GetAllDataSources_Returns200_WithList()
    {
        var expected = new List<DataSourceResponseDto> { new(), new() };

        _mockBusiness.Setup(b => b.GetAllDataSources(
                         UserId, OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetAllDataSources(ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllDataSources_Returns200_WithEmptyList()
    {
        _mockBusiness.Setup(b => b.GetAllDataSources(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>()))
                     .ReturnsAsync([]);

        var result = (await _controller.GetAllDataSources(ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<DataSourceResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllDataSources_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetAllDataSources(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long[]>(), It.IsAny<bool>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetAllDataSources(ProjectId, true));
    }

    [Fact]
    public async Task GetAllDataSources_RebuildsProjectIdArray_AndPassesOrganizationIdFromContext()
    {
        _mockBusiness.Setup(b => b.GetAllDataSources(
                         UserId, OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), false))
                     .ReturnsAsync([]);

        await _controller.GetAllDataSources(ProjectId, hideArchived: false);

        _mockBusiness.Verify(b => b.GetAllDataSources(
            UserId, OrgId, It.Is<long[]>(x => x.SequenceEqual(new[] { ProjectId })), false), Times.Once);
    }

    [Fact]
    public void GetAllDataSources_HasHttpGetAndReadDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceProjectController.GetAllDataSources),
            "projectId", "hideArchived");

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
        _mockBusiness.Setup(b => b.GetDataSource(OrgId, ProjectId, DataSourceId, true))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetDataSource(ProjectId, DataSourceId, true)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _controller.GetDataSource(ProjectId, DataSourceId, true));
    }

    [Fact]
    public async Task GetDataSource_PassesProjectIdFromRouteAndOrganizationIdFromContext()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.GetDataSource(OrgId, ProjectId, DataSourceId, true))
                     .ReturnsAsync(expected);

        await _controller.GetDataSource(ProjectId, DataSourceId, true);

        _mockBusiness.Verify(b => b.GetDataSource(OrgId, ProjectId, DataSourceId, true), Times.Once);
    }

    [Fact]
    public void GetDataSource_HasHttpGetAndReadDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceProjectController.GetDataSource),
            "projectId", "dataSourceId", "hideArchived");

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
        _mockBusiness.Setup(b => b.GetDefaultDataSource(OrgId, ProjectId))
                     .ReturnsAsync(expected);

        var result = (await _controller.GetDefaultDataSource(ProjectId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetDefaultDataSource_ThrowsException_WhenBusinessThrows()
    {
        _mockBusiness.Setup(b => b.GetDefaultDataSource(It.IsAny<long>(), It.IsAny<long?>()))
                     .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetDefaultDataSource(ProjectId));
    }

    [Fact]
    public async Task GetDefaultDataSource_PassesProjectIdAndOrganizationIdFromContext()
    {
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.GetDefaultDataSource(OrgId, ProjectId))
                     .ReturnsAsync(expected);

        await _controller.GetDefaultDataSource(ProjectId);

        _mockBusiness.Verify(b => b.GetDefaultDataSource(OrgId, ProjectId), Times.Once);
    }

    [Fact]
    public void GetDefaultDataSource_HasHttpGetAndReadDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceProjectController.GetDefaultDataSource),
            "projectId");

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

        _mockBusiness.Setup(b => b.CreateDataSource(OrgId, ProjectId, UserId, dto))
                     .ReturnsAsync(expected);

        var result = (await _controller.CreateDataSource(ProjectId, dto)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _controller.CreateDataSource(ProjectId, dto));
    }

    [Fact]
    public async Task CreateDataSource_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var dto = new CreateDataSourceRequestDto();
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.CreateDataSource(OrgId, ProjectId, UserId, dto))
                     .ReturnsAsync(expected);

        await _controller.CreateDataSource(ProjectId, dto);

        _mockBusiness.Verify(b => b.CreateDataSource(OrgId, ProjectId, UserId, dto), Times.Once);
    }

    [Fact]
    public void CreateDataSource_HasHttpPostAndProjectAdminAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceProjectController.CreateDataSource),
            "projectId", "dto");

        AssertHasHttpAttribute(method, "HttpPostAttribute");
        AssertHasProjectAdminAttribute(method);
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

        _mockBusiness.Setup(b => b.UpdateDataSource(OrgId, ProjectId, UserId, DataSourceId, dto))
                     .ReturnsAsync(expected);

        var result = (await _controller.UpdateDataSource(ProjectId, DataSourceId, dto)).Result as OkObjectResult;

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
            ProjectId, DataSourceId, new UpdateDataSourceRequestDto()));
    }

    [Fact]
    public async Task UpdateDataSource_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        var dto = new UpdateDataSourceRequestDto();
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.UpdateDataSource(OrgId, ProjectId, UserId, DataSourceId, dto))
                     .ReturnsAsync(expected);

        await _controller.UpdateDataSource(ProjectId, DataSourceId, dto);

        _mockBusiness.Verify(b => b.UpdateDataSource(OrgId, ProjectId, UserId, DataSourceId, dto), Times.Once);
    }

    [Fact]
    public void UpdateDataSource_HasHttpPutAndProjectAdminAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceProjectController.UpdateDataSource),
            "projectId", "dataSourceId", "dto");

        AssertHasHttpAttribute(method, "HttpPutAttribute");
        AssertHasProjectAdminAttribute(method);
    }

    #endregion

    // =========================================================================
    // DeleteDataSource Tests
    // =========================================================================

    #region DeleteDataSource Tests

    [Fact]
    public async Task DeleteDataSource_Returns200_WithBooleanResult()
    {
        _mockBusiness.Setup(b => b.DeleteDataSource(OrgId, ProjectId, DataSourceId))
                     .ReturnsAsync(true);

        var result = await _controller.DeleteDataSource(ProjectId, DataSourceId) as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _controller.DeleteDataSource(ProjectId, DataSourceId));
    }

    [Fact]
    public async Task DeleteDataSource_PassesOrganizationIdFromContextToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.DeleteDataSource(OrgId, ProjectId, DataSourceId))
                     .ReturnsAsync(true);

        await _controller.DeleteDataSource(ProjectId, DataSourceId);

        _mockBusiness.Verify(b => b.DeleteDataSource(OrgId, ProjectId, DataSourceId), Times.Once);
    }

    [Fact]
    public void DeleteDataSource_HasHttpDeleteAndProjectAdminAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceProjectController.DeleteDataSource),
            "projectId", "dataSourceId");

        AssertHasHttpAttribute(method, "HttpDeleteAttribute");
        AssertHasProjectAdminAttribute(method);
    }

    #endregion

    // =========================================================================
    // ArchiveDataSource Tests
    // =========================================================================

    #region ArchiveDataSource Tests

    [Fact]
    public async Task ArchiveDataSource_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.ArchiveDataSource(OrgId, ProjectId, UserId, DataSourceId))
                     .ReturnsAsync(true);

        var result = await _controller.ArchiveDataSource(
            ProjectId, DataSourceId, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.ArchiveDataSource(OrgId, ProjectId, UserId, DataSourceId), Times.Once);
        _mockBusiness.Verify(b => b.UnarchiveDataSource(
            It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveDataSource_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockBusiness.Setup(b => b.UnarchiveDataSource(OrgId, ProjectId, UserId, DataSourceId))
                     .ReturnsAsync(true);

        var result = await _controller.ArchiveDataSource(
            ProjectId, DataSourceId, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockBusiness.Verify(b => b.UnarchiveDataSource(OrgId, ProjectId, UserId, DataSourceId), Times.Once);
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
            _controller.ArchiveDataSource(ProjectId, DataSourceId, archive: true));
    }

    [Fact]
    public void ArchiveDataSource_HasHttpPatchAndProjectAdminAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceProjectController.ArchiveDataSource),
            "projectId", "dataSourceId", "archive");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasProjectAdminAttribute(method);
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
        _mockBusiness.Setup(b => b.SetDefaultDataSource(OrgId, ProjectId, UserId, DataSourceId))
                     .ReturnsAsync(expected);

        var result = (await _controller.SetDefaultDataSource(ProjectId, DataSourceId, true)).Result as OkObjectResult;

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

        await Assert.ThrowsAsync<Exception>(() => _controller.SetDefaultDataSource(ProjectId, DataSourceId, true));
    }

    [Fact]
    public async Task SetDefaultDataSource_PassesOrganizationIdFromContextAndCurrentUserIdToBusinessLayer()
    {
        // Note: the controller accepts an `isDefault` query parameter but never forwards it to the
        // business layer in either version (pre-existing gap in v1, not introduced by versioning),
        // so this deliberately does not assert on `isDefault` being passed through.
        var expected = new DataSourceResponseDto();
        _mockBusiness.Setup(b => b.SetDefaultDataSource(OrgId, ProjectId, UserId, DataSourceId))
                     .ReturnsAsync(expected);

        await _controller.SetDefaultDataSource(ProjectId, DataSourceId, isDefault: false);

        _mockBusiness.Verify(b => b.SetDefaultDataSource(OrgId, ProjectId, UserId, DataSourceId), Times.Once);
    }

    [Fact]
    public void SetDefaultDataSource_HasHttpPatchAndUpdateDataSourceAuthorization()
    {
        var method = GetControllerMethod(
            nameof(DataSourceProjectController.SetDefaultDataSource),
            "projectId", "dataSourceId", "isDefault");

        AssertHasHttpAttribute(method, "HttpPatchAttribute");
        AssertHasAuthAttribute(method, "update", "data_source");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void DataSourceProjectController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(DataSourceProjectController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    #endregion

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(DataSourceProjectController).GetMethods()
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

    private static void AssertHasProjectAdminAttribute(System.Reflection.MethodInfo method)
    {
        Assert.Contains(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "ProjectAdminAttribute");
    }
}