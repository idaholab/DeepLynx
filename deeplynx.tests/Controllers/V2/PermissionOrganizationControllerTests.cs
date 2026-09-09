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
///     Unit tests for the V2 actions of <see cref="PermissionOrganizationController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>
public class PermissionOrganizationControllerTests : IDisposable
{
    private readonly Mock<IPermissionBusiness> _mockPermissionBusiness;
    private readonly Mock<ILogger<PermissionOrganizationController>> _mockLogger;
    private readonly PermissionOrganizationController _permissionOrganizationController;

    private const long OrgId = 1L;
    private const long UserId = 10L;
    private const long PermissionId = 9L;

    public PermissionOrganizationControllerTests()
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
    // GetAllPermissions Tests
    // =========================================================================

    #region GetAllPermissions Tests

    [Fact]
    public async Task GetAllPermissions_Returns200_WithPermissions()
    {
        IEnumerable<PermissionResponseDto> expected = new List<PermissionResponseDto>();

        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(null, null, OrgId, true))
            .ReturnsAsync(expected);

        var result = (await _permissionOrganizationController.GetAllPermissions(
            OrgId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllPermissions_Returns200_WithEmptyList()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(null, null, OrgId, true))
            .ReturnsAsync([]);

        var result = (await _permissionOrganizationController.GetAllPermissions(
            OrgId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<PermissionResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetAllPermissions_ThrowsException_WhenBusinessThrows()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(null, null, OrgId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionOrganizationController.GetAllPermissions(
            OrgId, true));
    }

    [Fact]
    public async Task GetAllPermissions_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new List<PermissionResponseDto>();

        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(null, null, OrgId, true))
            .ReturnsAsync(expected);

        await _permissionOrganizationController.GetAllPermissions(OrgId, true);

        _mockPermissionBusiness.Verify(
            b => b.GetAllPermissions(null, null, OrgId, true),
            Times.Once);
    }

    [Fact]
    public void GetAllPermissions_HasHttpGetAndReadPermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.GetAllPermissions),
            "organizationId", "hideArchived");

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
            .Setup(b => b.GetPermission(OrgId, null, PermissionId, true))
            .ReturnsAsync(expected);

        var result = (await _permissionOrganizationController.GetPermission(
            OrgId, PermissionId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetPermission_Returns200_WithNullPermission()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetPermission(OrgId, null, PermissionId, true))
            .ReturnsAsync((PermissionResponseDto)null!);

        var result = (await _permissionOrganizationController.GetPermission(
            OrgId, PermissionId, true)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPermission_ThrowsException_WhenBusinessThrows()
    {
        _mockPermissionBusiness
            .Setup(b => b.GetPermission(OrgId, null, PermissionId, true))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _permissionOrganizationController.GetPermission(
            OrgId, PermissionId, true));
    }

    [Fact]
    public async Task GetPermission_PassesNullProjectIdToBusinessLayer()
    {
        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.GetPermission(OrgId, null, PermissionId, true))
            .ReturnsAsync(expected);

        await _permissionOrganizationController.GetPermission(OrgId, PermissionId, true);

        _mockPermissionBusiness.Verify(
            b => b.GetPermission(OrgId, null, PermissionId, true),
            Times.Once);
    }

    [Fact]
    public void GetPermission_HasHttpGetAndReadPermissionAuthorization()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.GetPermission),
            "organizationId", "permissionId", "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
        AssertHasAuthAttribute(method, "read", "permission");
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