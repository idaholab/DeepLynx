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
///     Unit tests for <see cref="PermissionOrganizationController"/>.
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
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long LabelId = 8L;
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
        // Arrange
        IEnumerable<PermissionResponseDto> expected =
            new List<PermissionResponseDto>();

        _mockPermissionBusiness
            .Setup(b => b.GetAllPermissions(LabelId, null, OrgId, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _permissionOrganizationController.GetAllPermissions(OrgId, LabelId, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockPermissionBusiness.Verify(
            b => b.GetAllPermissions(LabelId, null, OrgId, true),
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
        var actionResult = await _permissionOrganizationController.GetAllPermissions(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>());

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
                LabelId,
                It.Is<long?>(projectId => projectId == null),
                OrgId,
                true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _permissionOrganizationController.GetAllPermissions(
            OrgId,
            LabelId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        _mockPermissionBusiness.Verify(
            b => b.GetAllPermissions(
                LabelId,
                It.Is<long?>(projectId => projectId == null),
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
            .Setup(b => b.GetAllPermissions(LabelId, null, OrgId, true))
            .ReturnsAsync(expected);

        // Act
        await _permissionOrganizationController.GetAllPermissions(OrgId, LabelId, true);

        // Assert
        _mockPermissionBusiness.Verify(
            b => b.GetAllPermissions(LabelId, null, OrgId, true),
            Times.Once);
    }

    [Fact]
    public void GetAllPermissions_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.GetAllPermissions),
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetPermission Tests
    // =========================================================================

    #region GetPermission Tests

    [Fact]
    public async Task GetPermission_Returns200_WithPermission()
    {
        // Arrange
        PermissionResponseDto expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.GetPermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                PermissionId,
                true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _permissionOrganizationController.GetPermission(
            OrgId,
            PermissionId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockPermissionBusiness.Verify(
            b => b.GetPermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                PermissionId,
                true),
            Times.Once);
    }

    [Fact]
    public async Task GetPermission_Returns200_WithNullPermission()
    {
        // Arrange

        _mockPermissionBusiness
            .Setup(b => b.GetPermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                PermissionId,
                true))
            .ReturnsAsync((PermissionResponseDto)null!);

        // Act
        var actionResult = await _permissionOrganizationController.GetPermission(
            OrgId,
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
                It.Is<long?>(projectId => projectId == null),
                PermissionId,
                true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _permissionOrganizationController.GetPermission(
            OrgId,
            PermissionId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        _mockPermissionBusiness.Verify(
            b => b.GetPermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
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
                It.Is<long?>(projectId => projectId == null),
                PermissionId,
                true))
            .ReturnsAsync(expected);

        // Act
        await _permissionOrganizationController.GetPermission(
            OrgId,
            PermissionId,
            true);

        // Assert
        _mockPermissionBusiness.Verify(
            b => b.GetPermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
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
    // CreatePermission Tests
    // =========================================================================

    #region CreatePermission Tests

    [Fact]
    public async Task CreatePermission_Returns200_WithPermission()
    {
        // Arrange
        PermissionResponseDto expected =
            new PermissionResponseDto();
        CreatePermissionRequestDto input = new CreatePermissionRequestDto();

        _mockPermissionBusiness
            .Setup(b => b.CreatePermission(
                UserId,
                input,
                It.Is<long?>(projectId => projectId == null),
                OrgId))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _permissionOrganizationController.CreatePermission(OrgId, input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockPermissionBusiness.Verify(
            b => b.CreatePermission(
                UserId,
                input,
                It.Is<long?>(projectId => projectId == null),
                OrgId),
            Times.Once);
    }


    [Fact]
    public async Task CreatePermission_Returns500_OnUnexpectedException()
    {
        _mockPermissionBusiness
            .Setup(b => b.CreatePermission(
                It.IsAny<long>(),
                It.IsAny<CreatePermissionRequestDto>(),
                It.Is<long?>(projectId => projectId == null),
                It.IsAny<long>()))
                .ThrowsAsync(new Exception("db error"));

        var result = (await _permissionOrganizationController.CreatePermission(It.IsAny<long>(), It.IsAny<CreatePermissionRequestDto>())).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreatePermission_PassesToBusinessLayer()
    {
        // Arrange
        CreatePermissionRequestDto input = new CreatePermissionRequestDto();

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.CreatePermission(
                UserId,
                input,
                It.Is<long?>(projectId => projectId == null),
                OrgId))
            .ReturnsAsync(expected);

        // Act
        await _permissionOrganizationController.CreatePermission(OrgId, input);

        // Assert
        _mockPermissionBusiness.Verify(
            b => b.CreatePermission(
                UserId,
                input,
                It.Is<long?>(projectId => projectId == null),
                OrgId),
            Times.Once);
    }

    [Fact]
    public void CreatePermission_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.CreatePermission),
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // UpdatePermission Tests
    // =========================================================================

    #region UpdatePermission Tests

    [Fact]
    public async Task UpdatePermission_Returns200_WithPermission()
    {
        // Arrange
        PermissionResponseDto expected =
            new PermissionResponseDto();
        UpdatePermissionRequestDto input = new UpdatePermissionRequestDto();

        _mockPermissionBusiness
            .Setup(b => b.UpdatePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId,
                input)).ReturnsAsync(expected);

        // Act
        var actionResult = await _permissionOrganizationController.UpdatePermission(OrgId, PermissionId, input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockPermissionBusiness.Verify(
            b => b.UpdatePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId,
                input),
            Times.Once);
    }


    [Fact]
    public async Task UpdatePermission_Returns500_OnUnexpectedException()
    {
        _mockPermissionBusiness
           .Setup(b => b.UpdatePermission(
               It.IsAny<long>(),
               It.Is<long?>(projectId => projectId == null),
               It.IsAny<long>(),
               It.IsAny<long>(),
               It.IsAny<UpdatePermissionRequestDto>())).ThrowsAsync(new Exception("db error"));

        var result = (await _permissionOrganizationController.UpdatePermission(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<UpdatePermissionRequestDto>())).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdatePermission_PassesToBusinessLayer()
    {
        // Arrange
        UpdatePermissionRequestDto input = new UpdatePermissionRequestDto();

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new PermissionResponseDto();

        _mockPermissionBusiness
            .Setup(b => b.UpdatePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId,
                input)).ReturnsAsync(expected);

        // Act
        await _permissionOrganizationController.UpdatePermission(OrgId, PermissionId, input);

        // Assert
        _mockPermissionBusiness.Verify(
            b => b.UpdatePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId,
                input),
            Times.Once);
    }

    [Fact]
    public void UpdatePermission_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.UpdatePermission),
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
    }

    #endregion

    // =========================================================================
    // DeletePermission Tests
    // =========================================================================

    #region DeletePermission Tests

    [Fact]
    public async Task DeletePermission_Returns200_WithMessage()
    {
        // Arrange
        var expectedMessage = $"Deleted permission {PermissionId}";

        _mockPermissionBusiness
            .Setup(b => b.DeletePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId)).ReturnsAsync(true);

        // Act
        var actionResult = await _permissionOrganizationController.DeletePermission(OrgId, PermissionId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);

        var messageProperty = result.Value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var actualMessage = messageProperty.GetValue(result.Value) as string;
        Assert.Equal(expectedMessage, actualMessage);

        _mockPermissionBusiness.Verify(
            b => b.DeletePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId),
            Times.Once);
    }

    [Fact]
    public async Task DeletePermission_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockPermissionBusiness
            .Setup(b => b.DeletePermission(
                It.IsAny<long>(),
                It.Is<long?>(projectId => projectId == null),
                It.IsAny<long>(),
                It.IsAny<long>())).ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _permissionOrganizationController.DeletePermission(OrgId, PermissionId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while deleting permission {PermissionId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task DeletePermission_PassesToBusinessLayer()
    {
        // Arrange
        _mockPermissionBusiness
            .Setup(b => b.DeletePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId)).ReturnsAsync(true);

        // Act
        await _permissionOrganizationController.DeletePermission(OrgId, PermissionId);

        // Assert
        _mockPermissionBusiness.Verify(
            b => b.DeletePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId),
            Times.Once);
    }

    [Fact]
    public void DeletePermission_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.DeletePermission),
            "permissionId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // ArchivePermission Tests
    // =========================================================================

    #region ArchivePermission Tests

    [Fact]
    public async Task ArchivePermission_Returns200_WithArchivedMessage()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockPermissionBusiness
            .Setup(b => b.ArchivePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _permissionOrganizationController.ArchivePermission(
            OrgId,
            PermissionId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Archived permission {PermissionId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task ArchivePermission_Returns200_WithUnarchivedMessage()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockPermissionBusiness
            .Setup(b => b.UnarchivePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _permissionOrganizationController.ArchivePermission(
            OrgId,
            PermissionId,
            false);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Unarchived permission {PermissionId}",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task ArchivePermission_Returns500_OnArchiveException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockPermissionBusiness
            .Setup(b => b.ArchivePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _permissionOrganizationController.ArchivePermission(
            OrgId,
            PermissionId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while archiving permission {PermissionId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task ArchivePermission_Returns500_OnUnarchiveException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockPermissionBusiness
            .Setup(b => b.UnarchivePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _permissionOrganizationController.ArchivePermission(
            OrgId,
            PermissionId,
            false);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while unarchiving permission {PermissionId}", message);
        Assert.Contains("db error", message);
    }

    [Fact]
    public async Task ArchivePermission_PassesUserIdOrganizationIdAndPermissionIdToBusinessLayer_WhenArchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockPermissionBusiness
            .Setup(b => b.ArchivePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId))
            .ReturnsAsync(true);

        // Act
        await _permissionOrganizationController.ArchivePermission(
            OrgId,
            PermissionId,
            true);

        // Assert
        _mockPermissionBusiness.Verify(
            b => b.ArchivePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId),
            Times.Once);

        _mockPermissionBusiness.Verify(
            b => b.UnarchivePermission(
                It.IsAny<long>(),
                It.IsAny<long?>(),
                It.IsAny<long>(),
                It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchivePermission_PassesUserIdOrganizationIdAndPermissionIdToBusinessLayer_WhenUnarchiving()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockPermissionBusiness
            .Setup(b => b.UnarchivePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId))
            .ReturnsAsync(true);

        // Act
        await _permissionOrganizationController.ArchivePermission(
            OrgId,
            PermissionId,
            false);

        // Assert
        _mockPermissionBusiness.Verify(
            b => b.UnarchivePermission(
                OrgId,
                It.Is<long?>(projectId => projectId == null),
                UserId,
                PermissionId),
            Times.Once);

        _mockPermissionBusiness.Verify(
            b => b.ArchivePermission(
                It.IsAny<long>(),
                It.IsAny<long?>(),
                It.IsAny<long>(),
                It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public void ArchivePermission_HasHttpPatch()
    {
        var method = GetControllerMethod(
            nameof(PermissionOrganizationController.ArchivePermission),
            "organizationId",
            "permissionId",
            "archive");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
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
