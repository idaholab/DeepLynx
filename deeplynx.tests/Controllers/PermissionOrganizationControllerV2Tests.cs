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
///     Unit tests for the V2 actions of <see cref="PermissionOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class PermissionOrganizationControllerTestsV2 : IDisposable
{
    private readonly Mock<IPermissionBusiness> _mockPermissionBusiness;
    private readonly Mock<ILogger<PermissionOrganizationController>> _mockLogger;
    private readonly PermissionOrganizationController _permissionOrganizationController;

    private const long OrgId = 1L;
    private const long UserId = 10L;
    private const long LabelId = 8L;
    private const long PermissionId = 9L;

    public PermissionOrganizationControllerTestsV2()
    {
        _mockPermissionBusiness = new Mock<IPermissionBusiness>();
        _mockLogger = new Mock<ILogger<PermissionOrganizationController>>();

        _permissionOrganizationController = new PermissionOrganizationController(
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
    // GetAllPermissionsV2 Tests
    // =========================================================================

    #region GetAllPermissionsV2 Tests

    [Fact]
    public async Task GetAllPermissionsV2_Returns200_WithPermissions()
    {
        IEnumerable<PermissionResponseDto> expected = new List<PermissionResponseDto>();

        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(LabelId, null, OrgId, true))
            .ReturnsAsync(expected);

        var result = (await _permissionOrganizationController.GetAllPermissionsV2(
            OrgId, LabelId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllPermissionsV2_Returns200_WithEmptyList()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(LabelId, null, OrgId, true))
            .ReturnsAsync([]);

        var result = (await _permissionOrganizationController.GetAllPermissionsV2(
            OrgId, LabelId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<PermissionResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllPermissionsV2_ThrowsException_WhenBusinessThrows()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(LabelId, null, OrgId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionOrganizationController.GetAllPermissionsV2(
            OrgId, LabelId, true));
    }

    [Fact]
    public async Task GetAllPermissionsV2_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new List<PermissionResponseDto>();

        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(LabelId, null, OrgId, true))
            .ReturnsAsync(expected);

        await _permissionOrganizationController.GetAllPermissionsV2(OrgId, LabelId, true);

        _mockPermissionBusiness.Verify(
            b => b.GetAllPermissions(LabelId, null, OrgId, true),
            Times.Once);
    }

    [Fact]
    public void GetAllPermissionsV2_HasHttpGetAndReadPermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.GetAllPermissionsV2),
            "organizationId", "labelId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "permission");
    }

    #endregion

    // =========================================================================
    // GetPermissionV2 Tests
    // =========================================================================

    #region GetPermissionV2 Tests

    [Fact]
    public async Task GetPermissionV2_Returns200_WithPermission()
    {
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.GetPermission(OrgId, null, PermissionId, true))
            .ReturnsAsync(expected);

        var result = (await _permissionOrganizationController.GetPermissionV2(
            OrgId, PermissionId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetPermissionV2_Returns200_WithNullPermission()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetPermission(OrgId, null, PermissionId, true))
            .ReturnsAsync((PermissionResponseDto)null!);

        var result = (await _permissionOrganizationController.GetPermissionV2(
            OrgId, PermissionId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPermissionV2_ThrowsException_WhenBusinessThrows()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetPermission(OrgId, null, PermissionId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionOrganizationController.GetPermissionV2(
            OrgId, PermissionId, true));
    }

    [Fact]
    public async Task GetPermissionV2_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.GetPermission(OrgId, null, PermissionId, true))
            .ReturnsAsync(expected);

        await _permissionOrganizationController.GetPermissionV2(OrgId, PermissionId, true);

        _mockPermissionBusiness.Verify(
            b => b.GetPermission(OrgId, null, PermissionId, true),
            Times.Once);
    }

    [Fact]
    public void GetPermissionV2_HasHttpGetAndReadPermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.GetPermissionV2),
            "organizationId", "permissionId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "permission");
    }

    #endregion

    // =========================================================================
    // CreatePermissionV2 Tests
    // =========================================================================

    #region CreatePermissionV2 Tests

    [Fact]
    public async Task CreatePermissionV2_Returns200_WithPermission()
    {
        var input = new CreatePermissionRequestDto();
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.CreatePermission(UserId, input, null, OrgId))
            .ReturnsAsync(expected);

        var result = (await _permissionOrganizationController.CreatePermissionV2(
            OrgId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreatePermissionV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new CreatePermissionRequestDto();
        _mockPermissionBusiness
            .Setup(b => b.CreatePermission(UserId, input, null, OrgId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionOrganizationController.CreatePermissionV2(
            OrgId, input));
    }

    [Fact]
    public async Task CreatePermissionV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new CreatePermissionRequestDto();
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.CreatePermission(UserId, input, null, OrgId))
            .ReturnsAsync(expected);

        await _permissionOrganizationController.CreatePermissionV2(OrgId, input);

        _mockPermissionBusiness.Verify(
            b => b.CreatePermission(UserId, input, null, OrgId),
            Times.Once);
    }

    [Fact]
    public void CreatePermissionV2_HasHttpPostAndWritePermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.CreatePermissionV2),
            "organizationId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
        AssertHasAuthAttribute(method, "write", "permission");
    }

    #endregion

    // =========================================================================
    // UpdatePermissionV2 Tests
    // =========================================================================

    #region UpdatePermissionV2 Tests

    [Fact]
    public async Task UpdatePermissionV2_Returns200_WithPermission()
    {
        var input = new UpdatePermissionRequestDto();
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.UpdatePermission(OrgId, null, UserId, PermissionId, input))
            .ReturnsAsync(expected);

        var result = (await _permissionOrganizationController.UpdatePermissionV2(
            OrgId, PermissionId, input)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdatePermissionV2_ThrowsException_WhenBusinessThrows()
    {
        var input = new UpdatePermissionRequestDto();
        _mockPermissionBusiness
            .Setup(b => b.UpdatePermission(OrgId, null, UserId, PermissionId, input))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionOrganizationController.UpdatePermissionV2(
            OrgId, PermissionId, input));
    }

    [Fact]
    public async Task UpdatePermissionV2_PassesCurrentUserIdAndNullProjectIdToBusinessLayer()
    {
        var input = new UpdatePermissionRequestDto();
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.UpdatePermission(OrgId, null, UserId, PermissionId, input))
            .ReturnsAsync(expected);

        await _permissionOrganizationController.UpdatePermissionV2(OrgId, PermissionId, input);

        _mockPermissionBusiness.Verify(
            b => b.UpdatePermission(OrgId, null, UserId, PermissionId, input),
            Times.Once);
    }

    [Fact]
    public void UpdatePermissionV2_HasHttpPutAndUpdatePermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.UpdatePermissionV2),
            "organizationId", "permissionId", "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
        AssertHasAuthAttribute(method, "update", "permission");
    }

    #endregion

    // =========================================================================
    // DeletePermissionV2 Tests
    // =========================================================================

    #region DeletePermissionV2 Tests

    [Fact]
    public async Task DeletePermissionV2_Returns200_WithBooleanResult()
    {
        _mockPermissionBusiness
            .Setup(b => b.DeletePermission(OrgId, null, UserId, PermissionId))
            .ReturnsAsync(true);

        var actionResult = await _permissionOrganizationController.DeletePermissionV2(
            OrgId, PermissionId);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
    }

    [Fact]
    public async Task DeletePermissionV2_ThrowsException_WhenBusinessThrows()
    {
        _mockPermissionBusiness
            .Setup(b => b.DeletePermission(OrgId, null, UserId, PermissionId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionOrganizationController.DeletePermissionV2(
            OrgId, PermissionId));
    }

    [Fact]
    public async Task DeletePermissionV2_PassesNullProjectIdToBusinessLayer()
    {
        _mockPermissionBusiness
            .Setup(b => b.DeletePermission(OrgId, null, UserId, PermissionId))
            .ReturnsAsync(true);

        await _permissionOrganizationController.DeletePermissionV2(OrgId, PermissionId);

        _mockPermissionBusiness.Verify(
            b => b.DeletePermission(OrgId, null, UserId, PermissionId),
            Times.Once);
    }

    [Fact]
    public void DeletePermissionV2_HasHttpDeleteAndWritePermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.DeletePermissionV2),
            "organizationId", "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
        AssertHasAuthAttribute(method, "write", "permission");
    }

    #endregion

    // =========================================================================
    // ArchivePermissionV2 Tests
    // =========================================================================

    #region ArchivePermissionV2 Tests

    [Fact]
    public async Task ArchivePermissionV2_WhenArchiveTrue_CallsArchiveBusinessAndReturns200()
    {
        _mockPermissionBusiness
            .Setup(b => b.ArchivePermission(OrgId, null, UserId, PermissionId))
            .ReturnsAsync(true);

        var actionResult = await _permissionOrganizationController.ArchivePermissionV2(
            OrgId, PermissionId, archive: true);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockPermissionBusiness.Verify(
            b => b.ArchivePermission(OrgId, null, UserId, PermissionId),
            Times.Once);
        _mockPermissionBusiness.Verify(
            b => b.UnarchivePermission(OrgId, null, UserId, PermissionId),
            Times.Never);
    }

    [Fact]
    public async Task ArchivePermissionV2_WhenArchiveFalse_CallsUnarchiveBusinessAndReturns200()
    {
        _mockPermissionBusiness
            .Setup(b => b.UnarchivePermission(OrgId, null, UserId, PermissionId))
            .ReturnsAsync(true);

        var actionResult = await _permissionOrganizationController.ArchivePermissionV2(
            OrgId, PermissionId, archive: false);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(true, result.Value);
        _mockPermissionBusiness.Verify(
            b => b.UnarchivePermission(OrgId, null, UserId, PermissionId),
            Times.Once);
        _mockPermissionBusiness.Verify(
            b => b.ArchivePermission(OrgId, null, UserId, PermissionId),
            Times.Never);
    }

    [Fact]
    public async Task ArchivePermissionV2_PropagatesException_ForMiddlewareToHandle_WhenArchiving()
    {
        _mockPermissionBusiness
            .Setup(b => b.ArchivePermission(OrgId, null, UserId, PermissionId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _permissionOrganizationController.ArchivePermissionV2(OrgId, PermissionId, archive: true));
    }

    [Fact]
    public async Task ArchivePermissionV2_PropagatesException_ForMiddlewareToHandle_WhenUnarchiving()
    {
        _mockPermissionBusiness
            .Setup(b => b.UnarchivePermission(OrgId, null, UserId, PermissionId))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() =>
            _permissionOrganizationController.ArchivePermissionV2(OrgId, PermissionId, archive: false));
    }

    [Fact]
    public async Task ArchivePermissionV2_PassesUserIdAndNullProjectIdToBusinessLayer_WhenArchiving()
    {
        _mockPermissionBusiness
            .Setup(b => b.ArchivePermission(OrgId, null, UserId, PermissionId))
            .ReturnsAsync(true);

        await _permissionOrganizationController.ArchivePermissionV2(OrgId, PermissionId, archive: true);

        _mockPermissionBusiness.Verify(
            b => b.ArchivePermission(OrgId, null, UserId, PermissionId),
            Times.Once);
    }

    [Fact]
    public async Task ArchivePermissionV2_PassesUserIdAndNullProjectIdToBusinessLayer_WhenUnarchiving()
    {
        _mockPermissionBusiness
            .Setup(b => b.UnarchivePermission(OrgId, null, UserId, PermissionId))
            .ReturnsAsync(true);

        await _permissionOrganizationController.ArchivePermissionV2(OrgId, PermissionId, archive: false);

        _mockPermissionBusiness.Verify(
            b => b.UnarchivePermission(OrgId, null, UserId, PermissionId),
            Times.Once);
    }

    [Fact]
    public void ArchivePermissionV2_HasHttpPatchAndUpdatePermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.ArchivePermissionV2),
            "organizationId", "permissionId", "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
        AssertHasAuthAttribute(method, "update", "permission");
    }

    #endregion

    // =========================================================================
    // Auth / Middleware Metadata Tests
    // =========================================================================

    #region Auth / Middleware Metadata Tests

    [Fact]
    public void PermissionOrganizationController_HasForbidServiceAccountsAttribute()
    {
        Assert.Contains(typeof(PermissionOrganizationController).GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == "ForbidServiceAccountsAttribute");
    }

    // Note: unlike every other Organization controller in this suite, this controller has no
    // [Authorize] attribute at the class level (only [ForbidServiceAccounts]). That's a pre-existing
    // gap flagged separately, not something to paper over here with a test that would pass against
    // the current code but assert something the class doesn't actually declare.

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
        return Assert.Single(typeof(PermissionOrganizationController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}