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
///     Unit tests for the V2 actions of <see cref="RoleProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class RoleProjectControllerTests : IDisposable
{
    private readonly Mock<IRoleBusiness> _mockRoleBusiness;
    private readonly Mock<ILogger<RoleProjectController>> _mockLogger;
    private readonly RoleProjectController _roleProjectController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long RoleId = 11L;
    private const long PermissionId = 15L;
    private static readonly long[] PermissionList = { 13L, 14L };

    public RoleProjectControllerTests()
    {
        _mockRoleBusiness = new Mock<IRoleBusiness>();
        _mockLogger = new Mock<ILogger<RoleProjectController>>();

        _roleProjectController = new RoleProjectController(
            _mockRoleBusiness.Object,
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
    // GetAllRoles Tests
    // =========================================================================

    #region GetAllRoles Tests

    [Fact]
    public async Task GetAllRoles_Returns200_WithRoles()
    {
        var expected = new List<RoleResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, ProjectId, true))
            .ReturnsAsync(expected);

        var result = (await _roleProjectController.GetAllRoles(
            OrgId, ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllRoles_Returns200_WithEmptyList()
    {
        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, ProjectId, true))
            .ReturnsAsync([]);

        var result = (await _roleProjectController.GetAllRoles(
            OrgId, ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<RoleResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllRoles_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, ProjectId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.GetAllRoles(OrgId, ProjectId, true));
    }

    [Fact]
    public async Task GetAllRoles_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new List<RoleResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, ProjectId, true))
            .ReturnsAsync(expected);

        await _roleProjectController.GetAllRoles(OrgId, ProjectId, true);

        _mockRoleBusiness.Verify(
            b => b.GetAllRoles(OrgId, ProjectId, true),
            Times.Once);
    }

    [Fact]
    public void GetAllRoles_HasHttpGetAndReadRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.GetAllRoles),
            "organizationId", "projectId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "role");
    }

    #endregion

    // =========================================================================
    // GetRole Tests
    // =========================================================================

    #region GetRole Tests

    [Fact]
    public async Task GetRole_Returns200_WithRole()
    {
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, ProjectId, true))
            .ReturnsAsync(expected);

        var result = (await _roleProjectController.GetRole(
            OrgId, ProjectId, RoleId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRole_Returns200_WithNullRole()
    {
        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, ProjectId, true))
            .ReturnsAsync((RoleResponseDto)null!);

        var result = (await _roleProjectController.GetRole(
            OrgId, ProjectId, RoleId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, ProjectId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.GetRole(
            OrgId, ProjectId, RoleId, true));
    }

    [Fact]
    public async Task GetRole_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, ProjectId, true))
            .ReturnsAsync(expected);

        await _roleProjectController.GetRole(OrgId, ProjectId, RoleId, true);

        _mockRoleBusiness.Verify(
            b => b.GetRole(RoleId, OrgId, ProjectId, true),
            Times.Once);
    }

    [Fact]
    public void GetRole_HasHttpGetAndReadRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.GetRole),
            "organizationId", "projectId", "roleId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "role");
    }

    #endregion

    // =========================================================================
    // CreateRole Tests
    // =========================================================================

    #region CreateRole Tests

    [Fact]
    public async Task CreateRole_Returns200_WithRole()
    {
        var input = new CreateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, ProjectId))
            .ReturnsAsync(expected);

        var result = (await _roleProjectController.CreateRole(
            OrgId, ProjectId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateRole_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateRoleRequestDto();
        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.CreateRole(OrgId, ProjectId, input));
    }

    [Fact]
    public async Task CreateRole_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var input = new CreateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, ProjectId))
            .ReturnsAsync(expected);

        await _roleProjectController.CreateRole(OrgId, ProjectId, input);

        _mockRoleBusiness.Verify(
            b => b.CreateRole(UserId, input, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void CreateRole_HasHttpPostAndWriteRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.CreateRole),
            "organizationId", "projectId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "role");
    }

    #endregion

    // =========================================================================
    // UpdateRole Tests
    // =========================================================================

    #region UpdateRole Tests

    [Fact]
    public async Task UpdateRole_Returns200_WithRole()
    {
        var input = new UpdateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, ProjectId, input))
            .ReturnsAsync(expected);

        var result = (await _roleProjectController.UpdateRole(
            OrgId, ProjectId, RoleId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateRole_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateRoleRequestDto();
        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, ProjectId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.UpdateRole(
            OrgId, ProjectId, RoleId, input));
    }

    [Fact]
    public async Task UpdateRole_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var input = new UpdateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, ProjectId, input))
            .ReturnsAsync(expected);

        await _roleProjectController.UpdateRole(OrgId, ProjectId, RoleId, input);

        _mockRoleBusiness.Verify(
            b => b.UpdateRole(UserId, RoleId, OrgId, ProjectId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateRole_HasHttpPutAndUpdateRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.UpdateRole),
            "organizationId", "projectId", "roleId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
        AssertHasAuthAttribute(method, "update", "role");
    }

    #endregion

    // =========================================================================
    // DeleteRole Tests
    // =========================================================================

    #region DeleteRole Tests

    [Fact]
    public async Task DeleteRole_Returns200_WithBooleanResult()
    {
        _mockRoleBusiness
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        var actionResult = await _roleProjectController.DeleteRole(OrgId, ProjectId, RoleId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.DeleteRole(OrgId, ProjectId, RoleId));
    }

    [Fact]
    public async Task DeleteRole_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.DeleteRole(OrgId, ProjectId, RoleId);

        _mockRoleBusiness.Verify(
            b => b.DeleteRole(UserId, RoleId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void DeleteRole_HasHttpDeleteAndWriteRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.DeleteRole),
            "organizationId", "projectId", "roleId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "write", "role");
    }

    #endregion

    // =========================================================================
    // ArchiveRole Tests
    // =========================================================================

    #region ArchiveRole Tests

    [Fact]
    public async Task ArchiveRole_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockRoleBusiness
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        var result = await _roleProjectController.ArchiveRole(
            OrgId, ProjectId, RoleId, archive: true) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockRoleBusiness.Verify(
            b => b.ArchiveRole(UserId, RoleId, OrgId, ProjectId),
            Times.Once);
        _mockRoleBusiness.Verify(
            b => b.UnarchiveRole(UserId, RoleId, OrgId, ProjectId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveRole_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        var result = await _roleProjectController.ArchiveRole(
            OrgId, ProjectId, RoleId, archive: false) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockRoleBusiness.Verify(
            b => b.UnarchiveRole(UserId, RoleId, OrgId, ProjectId),
            Times.Once);
        _mockRoleBusiness.Verify(
            b => b.ArchiveRole(UserId, RoleId, OrgId, ProjectId),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveRole_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _roleProjectController.ArchiveRole(OrgId, ProjectId, RoleId, archive: true));
    }

    [Fact]
    public async Task ArchiveRole_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _roleProjectController.ArchiveRole(OrgId, ProjectId, RoleId, archive: false));
    }

    [Fact]
    public async Task ArchiveRole_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer_WhenArchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.ArchiveRole(OrgId, ProjectId, RoleId, archive: true);

        _mockRoleBusiness.Verify(
            b => b.ArchiveRole(UserId, RoleId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveRole_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer_WhenUnarchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.ArchiveRole(OrgId, ProjectId, RoleId, archive: false);

        _mockRoleBusiness.Verify(
            b => b.UnarchiveRole(UserId, RoleId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void ArchiveRole_HasHttpPatchAndUpdateRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.ArchiveRole),
            "organizationId", "projectId", "roleId", "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
        AssertHasAuthAttribute(method, "update", "role");
    }

    #endregion

    // =========================================================================
    // GetPermissionsByRole Tests
    // =========================================================================

    #region GetPermissionsByRole Tests

    [Fact]
    public async Task GetPermissionsByRole_Returns200_WithPermissions()
    {
        var expected = new List<PermissionResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, ProjectId))
            .ReturnsAsync(expected);

        var result = (await _roleProjectController.GetPermissionsByRole(
            OrgId, ProjectId, RoleId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetPermissionsByRole_Returns200_WithNullPermissionsList()
    {
        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, ProjectId))
            .ReturnsAsync((List<PermissionResponseDto>)null!);

        var result = (await _roleProjectController.GetPermissionsByRole(
            OrgId, ProjectId, RoleId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPermissionsByRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.GetPermissionsByRole(
            OrgId, ProjectId, RoleId));
    }

    [Fact]
    public async Task GetPermissionsByRole_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new List<PermissionResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, ProjectId))
            .ReturnsAsync(expected);

        await _roleProjectController.GetPermissionsByRole(OrgId, ProjectId, RoleId);

        _mockRoleBusiness.Verify(
            b => b.GetPermissionsByRole(RoleId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void GetPermissionsByRole_HasHttpGetAndReadRoleAndReadPermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.GetPermissionsByRole),
            "organizationId", "projectId", "roleId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "role");
        AssertHasAuthAttribute(method, "read", "permission");
    }

    #endregion

    // =========================================================================
    // AddPermissionToRole Tests
    // =========================================================================

    #region AddPermissionToRole Tests

    [Fact]
    public async Task AddPermissionToRole_Returns200_WithBooleanResult()
    {
        _mockRoleBusiness
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, ProjectId))
            .ReturnsAsync(true);

        var actionResult = await _roleProjectController.AddPermissionToRole(
            OrgId, ProjectId, RoleId, PermissionId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task AddPermissionToRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.AddPermissionToRole(
            OrgId, ProjectId, RoleId, PermissionId));
    }

    [Fact]
    public async Task AddPermissionToRole_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.AddPermissionToRole(OrgId, ProjectId, RoleId, PermissionId);

        _mockRoleBusiness.Verify(
            b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void AddPermissionToRole_HasHttpPostAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.AddPermissionToRole),
            "organizationId", "projectId", "roleId", "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "update", "role");
        AssertHasAuthAttribute(method, "read", "permission");
    }

    #endregion

    // =========================================================================
    // RemovePermissionFromRole Tests
    // =========================================================================

    #region RemovePermissionFromRole Tests

    [Fact]
    public async Task RemovePermissionFromRole_Returns200_WithBooleanResult()
    {
        _mockRoleBusiness
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, ProjectId))
            .ReturnsAsync(true);

        var actionResult = await _roleProjectController.RemovePermissionFromRole(
            OrgId, ProjectId, RoleId, PermissionId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task RemovePermissionFromRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.RemovePermissionFromRole(
            OrgId, ProjectId, RoleId, PermissionId));
    }

    [Fact]
    public async Task RemovePermissionFromRole_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.RemovePermissionFromRole(OrgId, ProjectId, RoleId, PermissionId);

        _mockRoleBusiness.Verify(
            b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void RemovePermissionFromRole_HasHttpDeleteAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.RemovePermissionFromRole),
            "organizationId", "projectId", "roleId", "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "update", "role");
        AssertHasAuthAttribute(method, "read", "permission");
    }

    #endregion

    // =========================================================================
    // SetPermissionsForRole Tests
    // =========================================================================

    #region SetPermissionsForRole Tests

    [Fact]
    public async Task SetPermissionsForRole_Returns200_WithBooleanResult()
    {
        _mockRoleBusiness
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, ProjectId))
            .ReturnsAsync(true);

        var actionResult = await _roleProjectController.SetPermissionsForRole(
            OrgId, ProjectId, RoleId, PermissionList);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task SetPermissionsForRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.SetPermissionsForRole(
            OrgId, ProjectId, RoleId, PermissionList));
    }

    [Fact]
    public async Task SetPermissionsForRole_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.SetPermissionsForRole(OrgId, ProjectId, RoleId, PermissionList);

        _mockRoleBusiness.Verify(
            b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void SetPermissionsForRole_HasHttpPutAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.SetPermissionsForRole),
            "organizationId", "projectId", "roleId", "permissionIds");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
        AssertHasAuthAttribute(method, "update", "role");
        AssertHasAuthAttribute(method, "read", "permission");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void RoleProjectController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(RoleProjectController).GetCustomAttributesData(), attribute =>
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
        return Assert.Single(typeof(RoleProjectController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}