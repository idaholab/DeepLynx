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
///     Unit tests for the V2 actions of <see cref="RoleProjectController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class RoleProjectControllerTestsV2 : IDisposable
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

    public RoleProjectControllerTestsV2()
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
    // GetAllRolesV2 Tests
    // =========================================================================

    #region GetAllRolesV2 Tests

    [Fact]
    public async Task GetAllRolesV2_Returns200_WithRoles()
    {
        var expected = new List<RoleResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, ProjectId, true))
            .ReturnsAsync(expected);

        var result = (await _roleProjectController.GetAllRolesV2(
            OrgId, ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllRolesV2_Returns200_WithEmptyList()
    {
        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, ProjectId, true))
            .ReturnsAsync([]);

        var result = (await _roleProjectController.GetAllRolesV2(
            OrgId, ProjectId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<RoleResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllRolesV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, ProjectId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.GetAllRolesV2(OrgId, ProjectId, true));
    }

    [Fact]
    public async Task GetAllRolesV2_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new List<RoleResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, ProjectId, true))
            .ReturnsAsync(expected);

        await _roleProjectController.GetAllRolesV2(OrgId, ProjectId, true);

        _mockRoleBusiness.Verify(
            b => b.GetAllRoles(OrgId, ProjectId, true),
            Times.Once);
    }

    [Fact]
    public void GetAllRolesV2_HasHttpGetAndReadRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.GetAllRolesV2),
            "organizationId", "projectId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "role");
    }

    #endregion

    // =========================================================================
    // GetRoleV2 Tests
    // =========================================================================

    #region GetRoleV2 Tests

    [Fact]
    public async Task GetRoleV2_Returns200_WithRole()
    {
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, ProjectId, true))
            .ReturnsAsync(expected);

        var result = (await _roleProjectController.GetRoleV2(
            OrgId, ProjectId, RoleId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRoleV2_Returns200_WithNullRole()
    {
        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, ProjectId, true))
            .ReturnsAsync((RoleResponseDto)null!);

        var result = (await _roleProjectController.GetRoleV2(
            OrgId, ProjectId, RoleId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, ProjectId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.GetRoleV2(
            OrgId, ProjectId, RoleId, true));
    }

    [Fact]
    public async Task GetRoleV2_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, ProjectId, true))
            .ReturnsAsync(expected);

        await _roleProjectController.GetRoleV2(OrgId, ProjectId, RoleId, true);

        _mockRoleBusiness.Verify(
            b => b.GetRole(RoleId, OrgId, ProjectId, true),
            Times.Once);
    }

    [Fact]
    public void GetRoleV2_HasHttpGetAndReadRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.GetRoleV2),
            "organizationId", "projectId", "roleId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "role");
    }

    #endregion

    // =========================================================================
    // CreateRoleV2 Tests
    // =========================================================================

    #region CreateRoleV2 Tests

    [Fact]
    public async Task CreateRoleV2_Returns200_WithRole()
    {
        var input = new CreateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, ProjectId))
            .ReturnsAsync(expected);

        var result = (await _roleProjectController.CreateRoleV2(
            OrgId, ProjectId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateRoleV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateRoleRequestDto();
        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.CreateRoleV2(OrgId, ProjectId, input));
    }

    [Fact]
    public async Task CreateRoleV2_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var input = new CreateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, ProjectId))
            .ReturnsAsync(expected);

        await _roleProjectController.CreateRoleV2(OrgId, ProjectId, input);

        _mockRoleBusiness.Verify(
            b => b.CreateRole(UserId, input, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void CreateRoleV2_HasHttpPostAndWriteRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.CreateRoleV2),
            "organizationId", "projectId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "role");
    }

    #endregion

    // =========================================================================
    // UpdateRoleV2 Tests
    // =========================================================================

    #region UpdateRoleV2 Tests

    [Fact]
    public async Task UpdateRoleV2_Returns200_WithRole()
    {
        var input = new UpdateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, ProjectId, input))
            .ReturnsAsync(expected);

        var result = (await _roleProjectController.UpdateRoleV2(
            OrgId, ProjectId, RoleId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateRoleV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateRoleRequestDto();
        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, ProjectId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.UpdateRoleV2(
            OrgId, ProjectId, RoleId, input));
    }

    [Fact]
    public async Task UpdateRoleV2_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        var input = new UpdateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, ProjectId, input))
            .ReturnsAsync(expected);

        await _roleProjectController.UpdateRoleV2(OrgId, ProjectId, RoleId, input);

        _mockRoleBusiness.Verify(
            b => b.UpdateRole(UserId, RoleId, OrgId, ProjectId, input),
            Times.Once);
    }

    [Fact]
    public void UpdateRoleV2_HasHttpPutAndUpdateRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.UpdateRoleV2),
            "organizationId", "projectId", "roleId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
        AssertHasAuthAttribute(method, "update", "role");
    }

    #endregion

    // =========================================================================
    // DeleteRoleV2 Tests
    // =========================================================================

    #region DeleteRoleV2 Tests

    [Fact]
    public async Task DeleteRoleV2_Returns200_WithBooleanResult()
    {
        _mockRoleBusiness
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        var result = await _roleProjectController.DeleteRoleV2(OrgId, ProjectId, RoleId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.DeleteRoleV2(OrgId, ProjectId, RoleId));
    }

    [Fact]
    public async Task DeleteRoleV2_PassesCurrentUserIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.DeleteRoleV2(OrgId, ProjectId, RoleId);

        _mockRoleBusiness.Verify(
            b => b.DeleteRole(UserId, RoleId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void DeleteRoleV2_HasHttpDeleteAndWriteRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.DeleteRoleV2),
            "organizationId", "projectId", "roleId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "write", "role");
    }

    #endregion

    // =========================================================================
    // ArchiveRoleV2 Tests
    // =========================================================================

    #region ArchiveRoleV2 Tests

    [Fact]
    public async Task ArchiveRoleV2_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockRoleBusiness
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        var result = await _roleProjectController.ArchiveRoleV2(
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
    public async Task ArchiveRoleV2_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        var result = await _roleProjectController.ArchiveRoleV2(
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
    public async Task ArchiveRoleV2_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _roleProjectController.ArchiveRoleV2(OrgId, ProjectId, RoleId, archive: true));
    }

    [Fact]
    public async Task ArchiveRoleV2_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _roleProjectController.ArchiveRoleV2(OrgId, ProjectId, RoleId, archive: false));
    }

    [Fact]
    public async Task ArchiveRoleV2_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer_WhenArchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.ArchiveRoleV2(OrgId, ProjectId, RoleId, archive: true);

        _mockRoleBusiness.Verify(
            b => b.ArchiveRole(UserId, RoleId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveRoleV2_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer_WhenUnarchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.ArchiveRoleV2(OrgId, ProjectId, RoleId, archive: false);

        _mockRoleBusiness.Verify(
            b => b.UnarchiveRole(UserId, RoleId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void ArchiveRoleV2_HasHttpPatchAndUpdateRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.ArchiveRoleV2),
            "organizationId", "projectId", "roleId", "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
        AssertHasAuthAttribute(method, "update", "role");
    }

    #endregion

    // =========================================================================
    // GetPermissionsByRoleV2 Tests
    // =========================================================================

    #region GetPermissionsByRoleV2 Tests

    [Fact]
    public async Task GetPermissionsByRoleV2_Returns200_WithPermissions()
    {
        var expected = new List<PermissionResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, ProjectId))
            .ReturnsAsync(expected);

        var result = (await _roleProjectController.GetPermissionsByRoleV2(
            OrgId, ProjectId, RoleId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetPermissionsByRoleV2_Returns200_WithNullPermissionsList()
    {
        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, ProjectId))
            .ReturnsAsync((List<PermissionResponseDto>)null!);

        var result = (await _roleProjectController.GetPermissionsByRoleV2(
            OrgId, ProjectId, RoleId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPermissionsByRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.GetPermissionsByRoleV2(
            OrgId, ProjectId, RoleId));
    }

    [Fact]
    public async Task GetPermissionsByRoleV2_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        var expected = new List<PermissionResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, ProjectId))
            .ReturnsAsync(expected);

        await _roleProjectController.GetPermissionsByRoleV2(OrgId, ProjectId, RoleId);

        _mockRoleBusiness.Verify(
            b => b.GetPermissionsByRole(RoleId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void GetPermissionsByRoleV2_HasHttpGetAndReadRoleAndReadPermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.GetPermissionsByRoleV2),
            "organizationId", "projectId", "roleId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "role");
        AssertHasAuthAttribute(method, "read", "permission");
    }

    #endregion

    // =========================================================================
    // AddPermissionToRoleV2 Tests
    // =========================================================================

    #region AddPermissionToRoleV2 Tests

    [Fact]
    public async Task AddPermissionToRoleV2_Returns200_WithBooleanResult()
    {
        _mockRoleBusiness
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, ProjectId))
            .ReturnsAsync(true);

        var result = await _roleProjectController.AddPermissionToRoleV2(
            OrgId, ProjectId, RoleId, PermissionId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task AddPermissionToRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.AddPermissionToRoleV2(
            OrgId, ProjectId, RoleId, PermissionId));
    }

    [Fact]
    public async Task AddPermissionToRoleV2_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.AddPermissionToRoleV2(OrgId, ProjectId, RoleId, PermissionId);

        _mockRoleBusiness.Verify(
            b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void AddPermissionToRoleV2_HasHttpPostAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.AddPermissionToRoleV2),
            "organizationId", "projectId", "roleId", "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "update", "role");
        AssertHasAuthAttribute(method, "read", "permission");
    }

    #endregion

    // =========================================================================
    // RemovePermissionFromRoleV2 Tests
    // =========================================================================

    #region RemovePermissionFromRoleV2 Tests

    [Fact]
    public async Task RemovePermissionFromRoleV2_Returns200_WithBooleanResult()
    {
        _mockRoleBusiness
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, ProjectId))
            .ReturnsAsync(true);

        var result = await _roleProjectController.RemovePermissionFromRoleV2(
            OrgId, ProjectId, RoleId, PermissionId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task RemovePermissionFromRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.RemovePermissionFromRoleV2(
            OrgId, ProjectId, RoleId, PermissionId));
    }

    [Fact]
    public async Task RemovePermissionFromRoleV2_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.RemovePermissionFromRoleV2(OrgId, ProjectId, RoleId, PermissionId);

        _mockRoleBusiness.Verify(
            b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void RemovePermissionFromRoleV2_HasHttpDeleteAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.RemovePermissionFromRoleV2),
            "organizationId", "projectId", "roleId", "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "update", "role");
        AssertHasAuthAttribute(method, "read", "permission");
    }

    #endregion

    // =========================================================================
    // SetPermissionsForRoleV2 Tests
    // =========================================================================

    #region SetPermissionsForRoleV2 Tests

    [Fact]
    public async Task SetPermissionsForRoleV2_Returns200_WithBooleanResult()
    {
        _mockRoleBusiness
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, ProjectId))
            .ReturnsAsync(true);

        var result = await _roleProjectController.SetPermissionsForRoleV2(
            OrgId, ProjectId, RoleId, PermissionList) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task SetPermissionsForRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, ProjectId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleProjectController.SetPermissionsForRoleV2(
            OrgId, ProjectId, RoleId, PermissionList));
    }

    [Fact]
    public async Task SetPermissionsForRoleV2_PassesOrganizationIdAndProjectIdFromRouteToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, ProjectId))
            .ReturnsAsync(true);

        await _roleProjectController.SetPermissionsForRoleV2(OrgId, ProjectId, RoleId, PermissionList);

        _mockRoleBusiness.Verify(
            b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, ProjectId),
            Times.Once);
    }

    [Fact]
    public void SetPermissionsForRoleV2_HasHttpPutAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleProjectController.SetPermissionsForRoleV2),
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