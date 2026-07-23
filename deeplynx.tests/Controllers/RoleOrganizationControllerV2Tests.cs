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
///     Unit tests for the V2 actions of <see cref="RoleOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class RoleOrganizationControllerTestsV2 : IDisposable
{
    private readonly Mock<IRoleBusiness> _mockRoleBusiness;
    private readonly Mock<ILogger<RoleProjectController>> _mockLogger;
    private readonly RoleOrganizationController _roleOrganizationController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long RoleId = 11L;
    private const long PermissionId = 15L;
    private static readonly long[] PermissionList = { 13L, 14L };

    public RoleOrganizationControllerTestsV2()
    {
        _mockRoleBusiness = new Mock<IRoleBusiness>();
        _mockLogger = new Mock<ILogger<RoleProjectController>>();

        _roleOrganizationController = new RoleOrganizationController(
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
            .Setup(b => b.GetAllRoles(OrgId, null, true))
            .ReturnsAsync(expected);

        var result = (await _roleOrganizationController.GetAllRolesV2(OrgId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllRolesV2_Returns200_WithEmptyList()
    {
        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, null, true))
            .ReturnsAsync([]);

        var result = (await _roleOrganizationController.GetAllRolesV2(OrgId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<RoleResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllRolesV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, null, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.GetAllRolesV2(OrgId, true));
    }

    [Fact]
    public async Task GetAllRolesV2_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new List<RoleResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, null, true))
            .ReturnsAsync(expected);

        await _roleOrganizationController.GetAllRolesV2(OrgId, true);

        _mockRoleBusiness.Verify(
            b => b.GetAllRoles(OrgId, null, true),
            Times.Once);
    }

    [Fact]
    public void GetAllRolesV2_HasHttpGetAndReadRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.GetAllRolesV2),
            "organizationId", "hideArchived");

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
            .Setup(b => b.GetRole(RoleId, OrgId, null, true))
            .ReturnsAsync(expected);

        var result = (await _roleOrganizationController.GetRoleV2(OrgId, RoleId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRoleV2_Returns200_WithNullRole()
    {
        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, null, true))
            .ReturnsAsync((RoleResponseDto)null!);

        var result = (await _roleOrganizationController.GetRoleV2(OrgId, RoleId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, null, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.GetRoleV2(OrgId, RoleId, true));
    }

    [Fact]
    public async Task GetRoleV2_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, null, true))
            .ReturnsAsync(expected);

        await _roleOrganizationController.GetRoleV2(OrgId, RoleId, true);

        _mockRoleBusiness.Verify(
            b => b.GetRole(RoleId, OrgId, null, true),
            Times.Once);
    }

    [Fact]
    public void GetRoleV2_HasHttpGetAndReadRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.GetRoleV2),
            "organizationId", "roleId", "hideArchived");

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
            .Setup(b => b.CreateRole(UserId, input, OrgId, null))
            .ReturnsAsync(expected);

        var result = (await _roleOrganizationController.CreateRoleV2(OrgId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateRoleV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateRoleRequestDto();
        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.CreateRoleV2(OrgId, input));
    }

    [Fact]
    public async Task CreateRoleV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new CreateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, null))
            .ReturnsAsync(expected);

        await _roleOrganizationController.CreateRoleV2(OrgId, input);

        _mockRoleBusiness.Verify(
            b => b.CreateRole(UserId, input, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void CreateRoleV2_HasHttpPostAndWriteRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.CreateRoleV2),
            "organizationId", "dto");

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
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, null, input))
            .ReturnsAsync(expected);

        var result = (await _roleOrganizationController.UpdateRoleV2(OrgId, RoleId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateRoleV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateRoleRequestDto();
        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, null, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.UpdateRoleV2(OrgId, RoleId, input));
    }

    [Fact]
    public async Task UpdateRoleV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new UpdateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, null, input))
            .ReturnsAsync(expected);

        await _roleOrganizationController.UpdateRoleV2(OrgId, RoleId, input);

        _mockRoleBusiness.Verify(
            b => b.UpdateRole(UserId, RoleId, OrgId, null, input),
            Times.Once);
    }

    [Fact]
    public void UpdateRoleV2_HasHttpPutAndUpdateRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.UpdateRoleV2),
            "organizationId", "roleId", "dto");

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
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.DeleteRoleV2(OrgId, RoleId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.DeleteRoleV2(OrgId, RoleId));
    }

    [Fact]
    public async Task DeleteRoleV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.DeleteRoleV2(OrgId, RoleId);

        _mockRoleBusiness.Verify(
            b => b.DeleteRole(UserId, RoleId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void DeleteRoleV2_HasHttpDeleteAndWriteRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.DeleteRoleV2),
            "organizationId", "roleId");

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
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.ArchiveRoleV2(
            OrgId, RoleId, archive: true);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockRoleBusiness.Verify(
            b => b.ArchiveRole(UserId, RoleId, OrgId, null),
            Times.Once);
        _mockRoleBusiness.Verify(
            b => b.UnarchiveRole(UserId, RoleId, OrgId, null),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveRoleV2_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.ArchiveRoleV2(
            OrgId, RoleId, archive: false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockRoleBusiness.Verify(
            b => b.UnarchiveRole(UserId, RoleId, OrgId, null),
            Times.Once);
        _mockRoleBusiness.Verify(
            b => b.ArchiveRole(UserId, RoleId, OrgId, null),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveRoleV2_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _roleOrganizationController.ArchiveRoleV2(OrgId, RoleId, archive: true));
    }

    [Fact]
    public async Task ArchiveRoleV2_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _roleOrganizationController.ArchiveRoleV2(OrgId, RoleId, archive: false));
    }

    [Fact]
    public async Task ArchiveRoleV2_PassesUserIdAndNullProjectIdToBusinessLayer_WhenArchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.ArchiveRoleV2(OrgId, RoleId, archive: true);

        _mockRoleBusiness.Verify(
            b => b.ArchiveRole(UserId, RoleId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveRoleV2_PassesUserIdAndNullProjectIdToBusinessLayer_WhenUnarchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.ArchiveRoleV2(OrgId, RoleId, archive: false);

        _mockRoleBusiness.Verify(
            b => b.UnarchiveRole(UserId, RoleId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void ArchiveRoleV2_HasHttpPatchAndUpdateRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.ArchiveRoleV2),
            "organizationId", "roleId", "archive");

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
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, null))
            .ReturnsAsync(expected);

        var result = (await _roleOrganizationController.GetPermissionsByRoleV2(
            OrgId, RoleId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetPermissionsByRoleV2_Returns200_WithNullPermissionsList()
    {
        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, null))
            .ReturnsAsync((List<PermissionResponseDto>)null!);

        var result = (await _roleOrganizationController.GetPermissionsByRoleV2(
            OrgId, RoleId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPermissionsByRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.GetPermissionsByRoleV2(
            OrgId, RoleId));
    }

    [Fact]
    public async Task GetPermissionsByRoleV2_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new List<PermissionResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, null))
            .ReturnsAsync(expected);

        await _roleOrganizationController.GetPermissionsByRoleV2(OrgId, RoleId);

        _mockRoleBusiness.Verify(
            b => b.GetPermissionsByRole(RoleId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void GetPermissionsByRoleV2_HasHttpGetAndReadRoleAndReadPermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.GetPermissionsByRoleV2),
            "organizationId", "roleId");

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
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.AddPermissionToRoleV2(
            OrgId, RoleId, PermissionId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task AddPermissionToRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.AddPermissionToRoleV2(
            OrgId, RoleId, PermissionId));
    }

    [Fact]
    public async Task AddPermissionToRoleV2_PassesNullProjectIdToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.AddPermissionToRoleV2(OrgId, RoleId, PermissionId);

        _mockRoleBusiness.Verify(
            b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void AddPermissionToRoleV2_HasHttpPostAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.AddPermissionToRoleV2),
            "organizationId", "roleId", "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "update", "role");
        AssertHasAuthAttribute(method, "read", "permission");
        AssertHasAuthAttribute(method, "update", "user");
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
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.RemovePermissionFromRoleV2(
            OrgId, RoleId, PermissionId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task RemovePermissionFromRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.RemovePermissionFromRoleV2(
            OrgId, RoleId, PermissionId));
    }

    [Fact]
    public async Task RemovePermissionFromRoleV2_PassesNullProjectIdToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.RemovePermissionFromRoleV2(OrgId, RoleId, PermissionId);

        _mockRoleBusiness.Verify(
            b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void RemovePermissionFromRoleV2_HasHttpDeleteAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.RemovePermissionFromRoleV2),
            "organizationId", "roleId", "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "update", "role");
        AssertHasAuthAttribute(method, "read", "permission");
        AssertHasAuthAttribute(method, "update", "user");
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
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.SetPermissionsForRoleV2(
            OrgId, RoleId, PermissionList);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task SetPermissionsForRoleV2_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.SetPermissionsForRoleV2(
            OrgId, RoleId, PermissionList));
    }

    [Fact]
    public async Task SetPermissionsForRoleV2_PassesNullProjectIdToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.SetPermissionsForRoleV2(OrgId, RoleId, PermissionList);

        _mockRoleBusiness.Verify(
            b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void SetPermissionsForRoleV2_HasHttpPutAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.SetPermissionsForRoleV2),
            "organizationId", "roleId", "permissionIds");

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
    public void RoleOrganizationController_HasAuthorizeAttribute()
    {
        Assert.Contains(typeof(RoleOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "AuthorizeAttribute");
    }

    [Fact]
    public void RoleOrganizationController_HasForbidServiceAccountsAttribute()
    {
        Assert.Contains(typeof(RoleOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "ForbidServiceAccountsAttribute");
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
        return Assert.Single(typeof(RoleOrganizationController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}