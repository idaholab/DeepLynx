using deeplynx.api.Controllers;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Org.BouncyCastle.Crypto.Engines;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class RoleOrganizationControllerTests : IDisposable
{
    private readonly Mock<IRoleBusiness> _mockRoleBusiness;
    private readonly Mock<ILogger<RoleProjectController>> _mockLogger;
    private readonly RoleOrganizationController _roleOrganizationController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long RoleId = 11L;
    private const long ProjectId = 2L;
    private static readonly long[] ProjectList = { 13L, 14L };
    private const long RelationshipId = 22L;


    public RoleOrganizationControllerTests()
    {
        _mockRoleBusiness = new Mock<IRoleBusiness>();
        _mockLogger = new Mock<ILogger<RoleProjectController>>();

        _roleOrganizationController = new RoleOrganizationController(
            _mockRoleBusiness.Object,
            _mockLogger.Object
        );

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = false;
    }

    public void Dispose()
    {
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
        // Arrange
        List<RoleResponseDto> expected =
            new List<RoleResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, null, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _roleOrganizationController.GetAllRoles(OrgId, true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllRelationships_Returns200_WithEmptyList()
    {
        // Arrange

        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _roleOrganizationController.GetAllRoles(It.IsAny<long>(), It.IsAny<bool>());

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetAllRoles_Returns500_OnUnexpectedException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, null, true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _roleOrganizationController.GetAllRoles(
            OrgId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetAllRelationships_PassesToBusinessLayer()
    {
        // Arrange

        var expected = new List<RoleResponseDto>();

        _mockRoleBusiness
            .Setup(b => b.GetAllRoles(OrgId, null, true))
            .ReturnsAsync(expected);

        // Act
        await _roleOrganizationController.GetAllRoles(OrgId, true);

        // Assert
        _mockRoleBusiness.Verify(
            b => b.GetAllRoles(OrgId, null, true),
            Times.Once);
    }

    [Fact]
    public void GetAllRoles_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.GetAllRoles),
            "organizationId",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetRole Tests
    // =========================================================================

    #region GetRole Tests

    [Fact]
    public async Task GetRole_Returns200_WithRole()
    {
        // Arrange
        RoleResponseDto expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, null, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _roleOrganizationController.GetRole(
            OrgId,
            RoleId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetRole_Returns200_WithEmptyRole()
    {
        // Arrange

        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, ProjectId, true))
            .ReturnsAsync((RoleResponseDto)null!);

        // Act
        var actionResult = await _roleOrganizationController.GetRole(
            OrgId,
            RoleId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetRole_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, null, true))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _roleOrganizationController.GetRole(
            OrgId,
            RoleId,
            true);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetRole_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        var expected = new RoleResponseDto();

        _mockRoleBusiness
            .Setup(b => b.GetRole(RoleId, OrgId, null, true))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _roleOrganizationController.GetRole(
            OrgId,
            RoleId,
            true);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockRoleBusiness.Verify(
            b => b.GetRole(
                RoleId,
                OrgId,
                null,
                true),
            Times.Once);
    }

    [Fact]
    public void GetRole_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.GetRole),
            "organizationId",
            "roleId",
            "hideArchived");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // CreateRole Tests
    // =========================================================================

    #region CreateRole Tests

    [Fact]
    public async Task CreateRole_Returns200_WithRole()
    {
        // Arrange
        RoleResponseDto expected =
            new RoleResponseDto();
        CreateRoleRequestDto input = new CreateRoleRequestDto();

        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, null))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _roleOrganizationController.CreateRole(
            OrgId,
            input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task CreateRole_Returns500_OnUnexpectedException()
    {
        CreateRoleRequestDto input = new CreateRoleRequestDto();
        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, null))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _roleOrganizationController.CreateRole(
            OrgId,
            input);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateRole_PassesToBusinessLayer()
    {
        // Arrange
        CreateRoleRequestDto input = new CreateRoleRequestDto();
        var expected = new RoleResponseDto();

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRoleBusiness
            .Setup(b => b.CreateRole(UserId, input, OrgId, null))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _roleOrganizationController.CreateRole(
            OrgId,
            input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockRoleBusiness.Verify(
            b => b.CreateRole(UserId, input, OrgId, null),
            Times.Once);
    }

    [Fact]
    public void CreateRole_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.CreateRole),
            "organizationId",
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // UpdateRole Tests
    // =========================================================================

    #region UpdateRole Tests

    [Fact]
    public async Task UpdateRole_Returns200_WithRelationship()
    {
        // Arrange
        RoleResponseDto expected =
            new RoleResponseDto();
        UpdateRoleRequestDto input = new UpdateRoleRequestDto();

        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, null, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _roleOrganizationController.UpdateRole(
            OrgId,
            RoleId,
            input);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }


    [Fact]
    public async Task UpdateRole_Returns500_OnUnexpectedException()
    {
        UpdateRoleRequestDto input = new UpdateRoleRequestDto();
        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, null, input))
            .ThrowsAsync(new Exception("db error"));

        var actionResult = await _roleOrganizationController.UpdateRole(
            OrgId,
            RoleId,
            input);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_PassesToBusinessLayer()
    {
        // Arrange
        UpdateRoleRequestDto input = new UpdateRoleRequestDto();
        var expected = new RoleResponseDto();

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;

        _mockRoleBusiness
            .Setup(b => b.UpdateRole(UserId, RoleId, OrgId, null, input))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _roleOrganizationController.UpdateRole(
            OrgId,
            RoleId,
            input);

        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        // Assert
        _mockRoleBusiness.Verify(
            b => b.UpdateRole(UserId, RoleId, OrgId, null, input),
            Times.Once);
    }

    [Fact]
    public void UpdateRole_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(RoleOrganizationController.UpdateRole),
            "organizationId",
            "roleId",
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
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
        return Assert.Single(typeof(RoleOrganizationController).GetMethods()
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