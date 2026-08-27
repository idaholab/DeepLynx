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
///     Unit tests for the V2 actions of <see cref="PermissionProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class PermissionProjectControllerTests : IDisposable
{
    private readonly Mock<IPermissionBusiness> _mockPermissionBusiness;
    private readonly Mock<ILogger<PermissionProjectController>> _mockLogger;
    private readonly PermissionProjectController _permissionProjectController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long PermissionId = 9L;

    public PermissionProjectControllerTests()
    {
        _mockPermissionBusiness = new Mock<IPermissionBusiness>();
        _mockLogger = new Mock<ILogger<PermissionProjectController>>();

        _permissionProjectController = new PermissionProjectController(
            _mockPermissionBusiness.Object,
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
    // GetAllPermissions Tests
    // =========================================================================

    #region GetAllPermissions Tests

    [Fact]
    public async Task GetAllPermissions_Returns200_WithPermissions()
    {
        IEnumerable<PermissionResponseDto> expected = new List<PermissionResponseDto>();

        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(null, ProjectId, OrgId, true))
            .ReturnsAsync(expected);

        var result = (await _permissionProjectController.GetAllPermissions(
            OrgId, ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllPermissions_Returns200_WithEmptyList()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(null, ProjectId, OrgId, true))
            .ReturnsAsync([]);

        var result = (await _permissionProjectController.GetAllPermissions(
            OrgId, ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<PermissionResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllPermissions_ThrowsException_WhenBusinessThrows()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(null, ProjectId, OrgId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionProjectController.GetAllPermissions(
            OrgId, ProjectId, true));
    }

    [Fact]
    public async Task GetAllPermissions_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new List<PermissionResponseDto>();

        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(null, ProjectId, OrgId, true))
            .ReturnsAsync(expected);

        await _permissionProjectController.GetAllPermissions(OrgId, ProjectId, true);

        _mockPermissionBusiness.Verify(
            b => b.GetAllPermissions(null, ProjectId, OrgId, true),
            Times.Once);
    }

    [Fact]
    public void GetAllPermissions_HasHttpGetAndReadPermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionProjectController.GetAllPermissions),
            "organizationId", "projectId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "permission");
    }

    #endregion

    // =========================================================================
    // GetPermission Tests
    // =========================================================================

    #region GetPermission Tests

    [Fact]
    public async Task GetPermission_Returns200_WithPermission()
    {
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.GetPermission(OrgId, ProjectId, PermissionId, true))
            .ReturnsAsync(expected);

        var result = (await _permissionProjectController.GetPermission(
            OrgId, ProjectId, PermissionId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetPermission_Returns200_WithNullPermission()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetPermission(OrgId, ProjectId, PermissionId, true))
            .ReturnsAsync((PermissionResponseDto)null!);

        var result = (await _permissionProjectController.GetPermission(
            OrgId, ProjectId, PermissionId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPermission_ThrowsException_WhenBusinessThrows()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetPermission(OrgId, ProjectId, PermissionId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionProjectController.GetPermission(
            OrgId, ProjectId, PermissionId, true));
    }

    [Fact]
    public async Task GetPermission_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.GetPermission(OrgId, ProjectId, PermissionId, true))
            .ReturnsAsync(expected);

        await _permissionProjectController.GetPermission(OrgId, ProjectId, PermissionId, true);

        _mockPermissionBusiness.Verify(
            b => b.GetPermission(OrgId, ProjectId, PermissionId, true),
            Times.Once);
    }

    [Fact]
    public void GetPermission_HasHttpGetAndReadPermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionProjectController.GetPermission),
            "organizationId", "projectId", "permissionId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "permission");
    }

    #endregion

    // =========================================================================
    // CreatePermission Tests
    // =========================================================================

    #region CreatePermission Tests

    [Fact]
    public async Task CreatePermission_Returns200_WithPermission()
    {
        var input = new CreatePermissionRequestDto();
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.CreatePermission(UserId, input, ProjectId, OrgId))
            .ReturnsAsync(expected);

        var result = (await _permissionProjectController.CreatePermission(
            OrgId, ProjectId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreatePermission_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreatePermissionRequestDto();
        _mockPermissionBusiness
            .Setup(b => b.CreatePermission(UserId, input, ProjectId, OrgId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionProjectController.CreatePermission(
            OrgId, ProjectId, input));
    }

    [Fact]
    public async Task CreatePermission_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var input = new CreatePermissionRequestDto();
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.CreatePermission(UserId, input, ProjectId, OrgId))
            .ReturnsAsync(expected);

        await _permissionProjectController.CreatePermission(OrgId, ProjectId, input);

        _mockPermissionBusiness.Verify(
            b => b.CreatePermission(UserId, input, ProjectId, OrgId),
            Times.Once);
    }

    [Fact]
    public void CreatePermission_HasHttpPostAndWritePermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionProjectController.CreatePermission),
            "organizationId", "projectId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "permission");
    }

    #endregion

    // =========================================================================
    // UpdatePermission Tests
    // =========================================================================

    #region UpdatePermission Tests

    [Fact]
    public async Task UpdatePermission_Returns200_WithPermission()
    {
        var input = new UpdatePermissionRequestDto();
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.UpdatePermission(OrgId, ProjectId, UserId, PermissionId, input))
            .ReturnsAsync(expected);

        var result = (await _permissionProjectController.UpdatePermission(
            OrgId, ProjectId, PermissionId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdatePermission_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdatePermissionRequestDto();
        _mockPermissionBusiness
            .Setup(b => b.UpdatePermission(OrgId, ProjectId, UserId, PermissionId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionProjectController.UpdatePermission(
            OrgId, ProjectId, PermissionId, input));
    }

    [Fact]
    public async Task UpdatePermission_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var input = new UpdatePermissionRequestDto();
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.UpdatePermission(OrgId, ProjectId, UserId, PermissionId, input))
            .ReturnsAsync(expected);

        await _permissionProjectController.UpdatePermission(OrgId, ProjectId, PermissionId, input);

        _mockPermissionBusiness.Verify(
            b => b.UpdatePermission(OrgId, ProjectId, UserId, PermissionId, input),
            Times.Once);
    }

    [Fact]
    public void UpdatePermission_HasHttpPutAndUpdatePermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionProjectController.UpdatePermission),
            "organizationId", "projectId", "permissionId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
        AssertHasAuthAttribute(method, "update", "permission");
    }

    #endregion

    // =========================================================================
    // DeletePermission Tests
    // =========================================================================

    #region DeletePermission Tests

    [Fact]
    public async Task DeletePermission_Returns200_WithBooleanResult()
    {
        _mockPermissionBusiness
            .Setup(b => b.DeletePermission(OrgId, ProjectId, UserId, PermissionId))
            .ReturnsAsync(true);

        var actionResult = await _permissionProjectController.DeletePermission(
            OrgId, ProjectId, PermissionId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeletePermission_ThrowsException_WhenBusinessThrows()
    {
        _mockPermissionBusiness
            .Setup(b => b.DeletePermission(OrgId, ProjectId, UserId, PermissionId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionProjectController.DeletePermission(
            OrgId, ProjectId, PermissionId));
    }

    [Fact]
    public async Task DeletePermission_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockPermissionBusiness
            .Setup(b => b.DeletePermission(OrgId, ProjectId, UserId, PermissionId))
            .ReturnsAsync(true);

        await _permissionProjectController.DeletePermission(OrgId, ProjectId, PermissionId);

        _mockPermissionBusiness.Verify(
            b => b.DeletePermission(OrgId, ProjectId, UserId, PermissionId),
            Times.Once);
    }

    [Fact]
    public void DeletePermission_HasHttpDeleteAndWritePermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionProjectController.DeletePermission),
            "organizationId", "projectId", "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "write", "permission");
    }

    #endregion

    // =========================================================================
    // ArchivePermission Tests
    // =========================================================================

    #region ArchivePermission Tests

    [Fact]
    public async Task ArchivePermission_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockPermissionBusiness
            .Setup(b => b.ArchivePermission(OrgId, ProjectId, UserId, PermissionId))
            .ReturnsAsync(true);

        var actionResult = await _permissionProjectController.ArchivePermission(
            OrgId, ProjectId, PermissionId, archive: true);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockPermissionBusiness.Verify(
            b => b.ArchivePermission(OrgId, ProjectId, UserId, PermissionId),
            Times.Once);
        _mockPermissionBusiness.Verify(
            b => b.UnarchivePermission(OrgId, ProjectId, UserId, PermissionId),
            Times.Never);
    }
    
    [Fact]
    public async Task ArchivePermission_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockPermissionBusiness
            .Setup(b => b.UnarchivePermission(OrgId, ProjectId, UserId, PermissionId))
            .ReturnsAsync(true);

        var actionResult = await _permissionProjectController.ArchivePermission(
            OrgId, ProjectId, PermissionId, archive: false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockPermissionBusiness.Verify(
            b => b.UnarchivePermission(OrgId, ProjectId, UserId, PermissionId),
            Times.Once);
        _mockPermissionBusiness.Verify(
            b => b.ArchivePermission(OrgId, ProjectId, UserId, PermissionId),
            Times.Never);
    }

    [Fact]
    public async Task ArchivePermission_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockPermissionBusiness
            .Setup(b => b.ArchivePermission(OrgId, ProjectId, UserId, PermissionId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _permissionProjectController.ArchivePermission(OrgId, ProjectId, PermissionId, archive: true));
    }

    [Fact]
    public async Task ArchivePermission_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockPermissionBusiness
            .Setup(b => b.UnarchivePermission(OrgId, ProjectId, UserId, PermissionId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _permissionProjectController.ArchivePermission(OrgId, ProjectId, PermissionId, archive: false));
    }

    [Fact]
    public async Task ArchivePermission_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer_WhenArchiving()
    {
        _mockPermissionBusiness
            .Setup(b => b.ArchivePermission(OrgId, ProjectId, UserId, PermissionId))
            .ReturnsAsync(true);

        await _permissionProjectController.ArchivePermission(OrgId, ProjectId, PermissionId, archive: true);

        _mockPermissionBusiness.Verify(
            b => b.ArchivePermission(OrgId, ProjectId, UserId, PermissionId),
            Times.Once);
    }

    [Fact]
    public async Task ArchivePermission_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer_WhenUnarchiving()
    {
        _mockPermissionBusiness
            .Setup(b => b.UnarchivePermission(OrgId, ProjectId, UserId, PermissionId))
            .ReturnsAsync(true);

        await _permissionProjectController.ArchivePermission(OrgId, ProjectId, PermissionId, archive: false);

        _mockPermissionBusiness.Verify(
            b => b.UnarchivePermission(OrgId, ProjectId, UserId, PermissionId),
            Times.Once);
    }

    [Fact]
    public void ArchivePermission_HasHttpPatchAndUpdatePermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionProjectController.ArchivePermission),
            "organizationId", "projectId", "permissionId", "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
        AssertHasAuthAttribute(method, "update", "permission");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void PermissionProjectController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(PermissionProjectController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    #endregion

    // =========================================================================
    // Test Helpers
    // =========================================================================

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

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(PermissionProjectController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}