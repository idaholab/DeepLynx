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
///     Unit tests for the V2 actions of <see cref="RoleOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class RoleOrganizationControllerTests : IDisposable
{
    private readonly Mock<IRoleBusiness> _mockRoleBusiness;
    private readonly Mock<ILogger<RoleProjectController>> _mockLogger;
    private readonly RoleOrganizationController _roleOrganizationController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long RoleId = 11L;
    private const long PermissionId = 15L;
    private static readonly long[] PermissionList = { 13L, 14L };

    public RoleOrganizationControllerTests()
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
    // GetAllRoles Tests
    // =========================================================================

    #region GetAllRoles Tests

    [Fact]
    public async Task GetAllRoles_Returns200_WithRoles()
    {
        var expected = new List<RoleResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, null, true))
            .ReturnsAsync(expected);

        var result = (await _roleOrganizationController.GetAllRoles(OrgId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllRoles_Returns200_WithEmptyList()
    {
        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, null, true))
            .ReturnsAsync([]);

        var result = (await _roleOrganizationController.GetAllRoles(OrgId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<RoleResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllRoles_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, null, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.GetAllRoles(OrgId, true));
    }

    [Fact]
    public async Task GetAllRoles_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new List<RoleResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, null, true))
            .ReturnsAsync(expected);

        await _roleOrganizationController.GetAllRoles(OrgId, true);

        _mockRoleBusiness.Verify(
            b => b.GetAllRoles(OrgId, null, true),
            Times.Once);
    }

    [Fact]
    public void GetAllRoles_HasHttpGetAndReadRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.GetAllRoles),
            "organizationId", "hideArchived");

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
            .Setup(b => b.GetRole(RoleId, OrgId, null, true))
            .ReturnsAsync(expected);

        var result = (await _roleOrganizationController.GetRole(OrgId, RoleId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRole_Returns200_WithNullRole()
    {
        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, null, true))
            .ReturnsAsync((RoleResponseDto)null!);

        var result = (await _roleOrganizationController.GetRole(OrgId, RoleId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, null, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.GetRole(OrgId, RoleId, true));
    }

    [Fact]
    public async Task GetRole_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, null, true))
            .ReturnsAsync(expected);

        await _roleOrganizationController.GetRole(OrgId, RoleId, true);

        _mockRoleBusiness.Verify(
            b => b.GetRole(RoleId, OrgId, null, true),
            Times.Once);
    }

    [Fact]
    public void GetRole_HasHttpGetAndReadRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.GetRole),
            "organizationId", "roleId", "hideArchived");

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
            .Setup(b => b.CreateRole(UserId, input, OrgId, null))
            .ReturnsAsync(expected);

        var result = (await _roleOrganizationController.CreateRole(OrgId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateRole_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreateRoleRequestDto();
        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.CreateRole(OrgId, input));
    }

    [Fact]
    public async Task CreateRole_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new CreateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, null))
            .ReturnsAsync(expected);

        await _roleOrganizationController.CreateRole(OrgId, input);

        _mockRoleBusiness.Verify(
            b => b.CreateRole(UserId, input, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void CreateRole_HasHttpPostAndWriteRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.CreateRole),
            "organizationId", "dto");

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
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, null, input))
            .ReturnsAsync(expected);

        var result = (await _roleOrganizationController.UpdateRole(OrgId, RoleId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateRole_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdateRoleRequestDto();
        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, null, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.UpdateRole(OrgId, RoleId, input));
    }

    [Fact]
    public async Task UpdateRole_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new UpdateRoleRequestDto();
        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, null, input))
            .ReturnsAsync(expected);

        await _roleOrganizationController.UpdateRole(OrgId, RoleId, input);

        _mockRoleBusiness.Verify(
            b => b.UpdateRole(UserId, RoleId, OrgId, null, input),
            Times.Once);
    }

    [Fact]
    public void UpdateRole_HasHttpPutAndUpdateRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.UpdateRole),
            "organizationId", "roleId", "dto");

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
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.DeleteRole(OrgId, RoleId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeleteRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.DeleteRole(OrgId, RoleId));
    }

    [Fact]
    public async Task DeleteRole_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.DeleteRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.DeleteRole(OrgId, RoleId);

        _mockRoleBusiness.Verify(
            b => b.DeleteRole(UserId, RoleId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void DeleteRole_HasHttpDeleteAndWriteRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.DeleteRole),
            "organizationId", "roleId");

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
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.ArchiveRole(
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
    public async Task ArchiveRole_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.ArchiveRole(
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
    public async Task ArchiveRole_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _roleOrganizationController.ArchiveRole(OrgId, RoleId, archive: true));
    }

    [Fact]
    public async Task ArchiveRole_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _roleOrganizationController.ArchiveRole(OrgId, RoleId, archive: false));
    }

    [Fact]
    public async Task ArchiveRole_PassesUserIdAndNullProjectIdToBusinessLayer_WhenArchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.ArchiveRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.ArchiveRole(OrgId, RoleId, archive: true);

        _mockRoleBusiness.Verify(
            b => b.ArchiveRole(UserId, RoleId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveRole_PassesUserIdAndNullProjectIdToBusinessLayer_WhenUnarchiving()
    {
        _mockRoleBusiness
            .Setup(b => b.UnarchiveRole(UserId, RoleId, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.ArchiveRole(OrgId, RoleId, archive: false);

        _mockRoleBusiness.Verify(
            b => b.UnarchiveRole(UserId, RoleId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void ArchiveRole_HasHttpPatchAndUpdateRoleAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.ArchiveRole),
            "organizationId", "roleId", "archive");

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
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, null))
            .ReturnsAsync(expected);

        var result = (await _roleOrganizationController.GetPermissionsByRole(
            OrgId, RoleId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetPermissionsByRole_Returns200_WithNullPermissionsList()
    {
        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, null))
            .ReturnsAsync((List<PermissionResponseDto>)null!);

        var result = (await _roleOrganizationController.GetPermissionsByRole(
            OrgId, RoleId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPermissionsByRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.GetPermissionsByRole(
            OrgId, RoleId));
    }

    [Fact]
    public async Task GetPermissionsByRole_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new List<PermissionResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetPermissionsByRole(RoleId, OrgId, null))
            .ReturnsAsync(expected);

        await _roleOrganizationController.GetPermissionsByRole(OrgId, RoleId);

        _mockRoleBusiness.Verify(
            b => b.GetPermissionsByRole(RoleId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void GetPermissionsByRole_HasHttpGetAndReadRoleAndReadPermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.GetPermissionsByRole),
            "organizationId", "roleId");

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
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.AddPermissionToRole(
            OrgId, RoleId, PermissionId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task AddPermissionToRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.AddPermissionToRole(
            OrgId, RoleId, PermissionId));
    }

    [Fact]
    public async Task AddPermissionToRole_PassesNullProjectIdToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.AddPermissionToRole(OrgId, RoleId, PermissionId);

        _mockRoleBusiness.Verify(
            b => b.AddPermissionToRole(RoleId, PermissionId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void AddPermissionToRole_HasHttpPostAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.AddPermissionToRole),
            "organizationId", "roleId", "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "update", "role");
        AssertHasAuthAttribute(method, "read", "permission");
        AssertHasAuthAttribute(method, "update", "user");
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
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.RemovePermissionFromRole(
            OrgId, RoleId, PermissionId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task RemovePermissionFromRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.RemovePermissionFromRole(
            OrgId, RoleId, PermissionId));
    }

    [Fact]
    public async Task RemovePermissionFromRole_PassesNullProjectIdToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.RemovePermissionFromRole(OrgId, RoleId, PermissionId);

        _mockRoleBusiness.Verify(
            b => b.RemovePermissionFromRole(RoleId, PermissionId, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void RemovePermissionFromRole_HasHttpDeleteAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.RemovePermissionFromRole),
            "organizationId", "roleId", "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "update", "role");
        AssertHasAuthAttribute(method, "read", "permission");
        AssertHasAuthAttribute(method, "update", "user");
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
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, null))
            .ReturnsAsync(true);

        var actionResult = await _roleOrganizationController.SetPermissionsForRole(
            OrgId, RoleId, PermissionList);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task SetPermissionsForRole_ThrowsException_WhenBusinessThrows()
    {
        _mockRoleBusiness
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _roleOrganizationController.SetPermissionsForRole(
            OrgId, RoleId, PermissionList));
    }

    [Fact]
    public async Task SetPermissionsForRole_PassesNullProjectIdToBusinessLayer()
    {
        _mockRoleBusiness
            .Setup(b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, null))
            .ReturnsAsync(true);

        await _roleOrganizationController.SetPermissionsForRole(OrgId, RoleId, PermissionList);

        _mockRoleBusiness.Verify(
            b => b.SetPermissionsForRole(RoleId, PermissionList, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void SetPermissionsForRole_HasHttpPutAndRequiredAuthorization()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.SetPermissionsForRole),
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