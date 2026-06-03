using deeplynx.api.Controllers;
using deeplynx.helpers.Context;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]

/// <summary>
///     Unit tests for <see cref="PermissionProjectController"/>.
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
    private const long LabelId = 8L;
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
        // Arrange
        IEnumerable<PermissionResponseDto> expected =
            new List<PermissionResponseDto>();

        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(null, ProjectId, OrgId, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _permissionProjectController.GetAllPermissions(OrgId, ProjectId, null, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockPermissionBusiness.Verify(
            b => b.GetAllPermissions(null, ProjectId, OrgId, true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllPermissions_Returns200_WithEmptyList()
    {
        // Arrange

        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _permissionProjectController.GetAllPermissions(It.IsAny<long>(), It.IsAny<long>(), null, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetAllPermissions_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(
                It.Is<long?>(labelId => labelId == null),
                ProjectId,
                OrgId,
                true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _permissionProjectController.GetAllPermissions(
            OrgId,
            ProjectId,
            null,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        _mockPermissionBusiness.Verify(
            b => b.GetAllPermissions(
                It.Is<long?>(labelId => labelId == null),
                ProjectId,
                OrgId,
                true),
            Times.Once);
    }

    [Fact]
    public async Task GetAllPermissions_PassesToBusinessLayer()
    {
        // Arrange

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new List<PermissionResponseDto>();

        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(
                null,
                ProjectId,
                OrgId,
                true))
            .ReturnsAsync(expected);

        // Act
        await _permissionProjectController.GetAllPermissions(OrgId, ProjectId, null, true);

        // Assert
        _mockPermissionBusiness.Verify(
            b => b.GetAllPermissions(
                null,
                ProjectId,
                OrgId,
                true),
            Times.Once);
    }

    [Fact]
    public void GetAllPermissions_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(PermissionProjectController.GetAllPermissions),
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetPermission Tests
    // =========================================================================

    #region GetPermission Tests

    [Fact]
    public async Task GetPermission_Returns200_WithOrganization()
    {
        // Arrange
        PermissionResponseDto expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.GetPermission(
                OrgId,
                ProjectId,
                PermissionId,
                true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _permissionProjectController.GetPermission(
            OrgId,
            ProjectId,
            PermissionId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockPermissionBusiness.Verify(
            b => b.GetPermission(
                OrgId,
                ProjectId,
                PermissionId,
                true),
            Times.Once);
    }

    [Fact]
    public async Task GetPermission_Returns200_WithNullOrganization()
    {
        // Arrange

        _mockPermissionBusiness
            .Setup(b => b.GetPermission(
                OrgId,
                ProjectId,
                PermissionId,
                true))
            .ReturnsAsync((PermissionResponseDto)null!);

        // Act
        var actionResult = await _permissionProjectController.GetPermission(
            OrgId,
            ProjectId,
            PermissionId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetPermission_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockPermissionBusiness
            .Setup(b => b.GetPermission(
                OrgId,
                ProjectId,
                PermissionId,
                true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _permissionProjectController.GetPermission(
            OrgId,
            ProjectId,
            PermissionId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        _mockPermissionBusiness.Verify(
            b => b.GetPermission(
                OrgId,
                ProjectId,
                PermissionId,
                true),
            Times.Once);
    }

    [Fact]
    public async Task GetPermission_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.GetPermission(
                OrgId,
                ProjectId,
                PermissionId,
                true))
            .ReturnsAsync(expected);

        // Act
        await _permissionProjectController.GetPermission(
            OrgId,
            ProjectId,
            PermissionId,
            true);

        // Assert
        _mockPermissionBusiness.Verify(
            b => b.GetPermission(
                OrgId,
                ProjectId,
                PermissionId,
                true),
            Times.Once);
    }

    [Fact]
    public void GetPermission_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.GetPermission),
            "organizationId",
            "permissionId",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
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

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(PermissionOrganizationController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }

    private static string GetMessageFromResultValue(object? value)
    {
        Assert.NotNull(value);

        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var message = messageProperty.GetValue(value) as string;
        Assert.NotNull(message);

        return message;
    }
}