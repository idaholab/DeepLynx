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
///     Unit tests for <see cref="UserModelTokenController"/>.
///     All business dependencies are mocked with Moq.
///     The controller is instantiated directly — no WebApplicationFactory or HTTP pipeline.
///
///     Implements IDisposable to reset UserContextStorage statics after every test,
///     preventing static state leaking across classes when the runner reuses threads.
/// </summary>

public class UserModelTokenControllerTests : IDisposable
{
    private readonly Mock<IUserModelTokenBusiness> _mockBusiness;
    private readonly Mock<ILogger<UserModelTokenController>> _mockLogger;
    private readonly UserModelTokenController _userModelTokenController;

    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long UserId = 10L;
    private const long OtherUserId = 11L;
    private const long RoleId = 20L;
    private const long GroupId = 30L;
    private const long UserModelTokenId = 9L;

    public UserModelTokenControllerTests()
    {
        _mockBusiness = new Mock<IUserModelTokenBusiness>();
        _mockLogger = new Mock<ILogger<UserModelTokenController>>();

        _userModelTokenController = new UserModelTokenController(
            _mockBusiness.Object,
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
    // GetUserTokens Tests
    // =========================================================================

    #region GetUserTokens Tests

    [Fact]
    public async Task GetUserTokens_Returns200_WithList()
    {
        var expected = new List<UserModelTokenResponseDto>
        {
            new(),
            new()
        };

        _mockBusiness.Setup(b => b.GetUserTokens(UserId, null))
                     .ReturnsAsync(expected);

        var result = (await _userModelTokenController.GetUserTokens(null)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetUserTokens_Returns200_WithEmptyList()
    {
        _mockBusiness.Setup(b => b.GetUserTokens(UserId, null))
                     .ReturnsAsync([]);

        var result = (await _userModelTokenController.GetUserTokens(null)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<IEnumerable<UserModelTokenResponseDto>>(result.Value);
    }

    [Fact]
    public async Task GetUserTokens_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetUserTokens(UserId, null))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userModelTokenController.GetUserTokens(null)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetUserTokens_PassesCurrentUserIdAndHideArchivedToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.GetUserTokens(UserId, null))
                     .ReturnsAsync([]);

        await _userModelTokenController.GetUserTokens(null);

        _mockBusiness.Verify(b => b.GetUserTokens(UserId, null), Times.Once);
    }

    [Fact]
    public void GetUserTokens_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(UserModelTokenController.GetUserTokens),
            "aiModelConfigId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GetTokenById Tests
    // =========================================================================

    #region GetTokenById Tests

    [Fact]
    public async Task GetTokenById_Returns200_WithList()
    {
        var expected = new UserModelTokenResponseDto();


        _mockBusiness.Setup(b => b.GetTokenById(UserId, UserModelTokenId))
                     .ReturnsAsync(expected);

        var result = (await _userModelTokenController.GetTokenById(UserModelTokenId)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetTokenById_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.GetTokenById(UserId, UserModelTokenId))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userModelTokenController.GetTokenById(UserModelTokenId)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetTokenById_PassesToBusinessLayer()
    {
        var expected = new UserModelTokenResponseDto();
        _mockBusiness.Setup(b => b.GetTokenById(UserId, UserModelTokenId))
                     .ReturnsAsync(expected);

        await _userModelTokenController.GetTokenById(UserModelTokenId);

        _mockBusiness.Verify(b => b.GetTokenById(UserId, UserModelTokenId), Times.Once);
    }

    [Fact]
    public void GetTokenById_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(UserModelTokenController.GetTokenById),
            "userModelTokenId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // CreateUserModelToken Tests
    // =========================================================================

    #region CreateUserModelToken Tests

    [Fact]
    public async Task CreateUserModelToken_Returns200_WithProject()
    {
        var dto = new CreateUserModelTokenRequestDto();
        var expected = new UserModelTokenResponseDto();

        _mockBusiness.Setup(b => b.CreateUserModelToken(UserId, dto))
                     .ReturnsAsync(expected);

        var result = (await _userModelTokenController.CreateUserModelToken(dto))
        .Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateUserModelToken_Returns500_OnUnexpectedException()
    {
        var dto = new CreateUserModelTokenRequestDto();
        _mockBusiness.Setup(b => b.CreateUserModelToken(UserId, dto))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userModelTokenController.CreateUserModelToken(dto))
        .Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task CreateUserModelToken_PassesToBusinessLayer()
    {
        var dto = new CreateUserModelTokenRequestDto();

        _mockBusiness.Setup(b => b.CreateUserModelToken(UserId, dto))
                     .ReturnsAsync(new UserModelTokenResponseDto());

        await _userModelTokenController.CreateUserModelToken(dto);

        _mockBusiness.Verify(b => b.CreateUserModelToken(UserId, dto), Times.Once);
    }

    [Fact]
    public void CreateUserModelToken_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(UserModelTokenController.CreateUserModelToken),
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // UpdateUserModelToken Tests
    // =========================================================================

    #region UpdateUserModelToken Tests

    [Fact]
    public async Task UpdateUserModelToken_Returns200_WithUpdatedProject()
    {
        var dto = new UpdateUserModelTokenRequestDto();
        var expected = new UserModelTokenResponseDto();

        _mockBusiness.Setup(b => b.UpdateUserModelToken(UserId, UserModelTokenId, dto))
                     .ReturnsAsync(expected);

        var result = (await _userModelTokenController.UpdateUserModelToken(
            UserModelTokenId, dto)).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task UpdateUserModelToken_Returns500_OnUnexpectedException()
    {
        var dto = new UpdateUserModelTokenRequestDto();
        _mockBusiness.Setup(b => b.UpdateUserModelToken(UserId, UserModelTokenId, dto))
                     .ThrowsAsync(new Exception("db error"));

        var result = (await _userModelTokenController.UpdateUserModelToken(
            UserModelTokenId, dto)).Result as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task UpdateUserModelToken_PassesCurrentUserIdToBusinessLayer()
    {
        var dto = new UpdateUserModelTokenRequestDto();

        _mockBusiness.Setup(b => b.UpdateUserModelToken(UserId, UserModelTokenId, dto))
                     .ReturnsAsync(new UserModelTokenResponseDto());

        await _userModelTokenController.UpdateUserModelToken(UserModelTokenId, dto);

        _mockBusiness.Verify(b => b.UpdateUserModelToken(UserId, UserModelTokenId, dto), Times.Once);
    }

    [Fact]
    public void UpdateUserModelToken_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(UserModelTokenController.UpdateUserModelToken),
            "userModelTokenId",
            "dto");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
    }

    #endregion

    // =========================================================================
    // DeleteUserModelToken Tests
    // =========================================================================

    #region DeleteUserModelToken Tests

    [Fact]
    public async Task DeleteUserModelToken_Returns200_OnSuccess()
    {
        _mockBusiness.Setup(b => b.DeleteUserModelToken(UserId, UserModelTokenId))
                     .Returns(Task.FromResult(true));

        var result = await _userModelTokenController.DeleteUserModelToken(UserModelTokenId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task DeleteUserModelToken_Returns500_OnUnexpectedException()
    {
        _mockBusiness.Setup(b => b.DeleteUserModelToken(UserId, UserModelTokenId))
                     .ThrowsAsync(new Exception("db error"));

        var result = await _userModelTokenController.DeleteUserModelToken(UserModelTokenId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task DeleteUserModelToken_PassesCurrentUserIdToBusinessLayer()
    {
        _mockBusiness.Setup(b => b.DeleteUserModelToken(UserId, UserModelTokenId))
                     .Returns(Task.FromResult(true));

        await _userModelTokenController.DeleteUserModelToken(UserModelTokenId);

        _mockBusiness.Verify(b => b.DeleteUserModelToken(UserId, UserModelTokenId), Times.Once);
    }

    [Fact]
    public void DeleteUserModelToken_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(UserModelTokenController.DeleteUserModelToken),
            "userModelTokenId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
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
        return Assert.Single(typeof(UserModelTokenController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}