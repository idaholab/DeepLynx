using deeplynx.api.Controllers;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class TokenControllerTests : IDisposable
{
    private readonly Mock<IEventBusiness> _mockEventBusiness;
    private readonly Mock<ILogger<TokenController>> _mockLogger;
    private readonly Mock<ITokenBusiness> _mockTokenBusiness;
    private readonly TokenController _tokenController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long PermissionId = 15L;
    private static readonly long[] ProjectList = { 13L, 14L };
    private static readonly long[] PermissionList = { 13L, 14L };
    private const long RelationshipId = 22L;
    private const long TagId = 67L;


    public TokenControllerTests()
    {
        _mockEventBusiness = new Mock<IEventBusiness>();
        _mockLogger = new Mock<ILogger<TokenController>>();
        _mockTokenBusiness = new Mock<ITokenBusiness>();

        _tokenController = new TokenController(
            _mockEventBusiness.Object,
            _mockTokenBusiness.Object,
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
    // CreateToken Tests
    // =========================================================================

    #region CreateToken Tests

    [Fact]
    public async Task CreateToken_Returns200_WithToken()
    {
        // Arrange
        var expected = "jwt-token";

        CreateTokenDto tokenDto = new CreateTokenDto
        {
            ApiKey = "api-key",
            ApiSecret = "api-secret",
            ExpirationMinutes = 60
        };

        _mockTokenBusiness
            .Setup(b => b.CreateToken(
                tokenDto.ApiKey,
                tokenDto.ApiSecret,
                tokenDto.ExpirationMinutes))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tokenController.CreateToken(tokenDto);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateToken_Returns200_WithEmptyToken()
    {
        // Arrange
        CreateTokenDto tokenDto = new CreateTokenDto
        {
            ApiKey = "api-key",
            ApiSecret = "api-secret",
            ExpirationMinutes = 60
        };

        _mockTokenBusiness
            .Setup(b => b.CreateToken(
                tokenDto.ApiKey,
                tokenDto.ApiSecret,
                tokenDto.ExpirationMinutes))
            .ReturnsAsync(string.Empty);

        // Act
        var actionResult = await _tokenController.CreateToken(tokenDto);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task CreateToken_Returns500_OnUnexpectedException()
    {
        // Arrange
        CreateTokenDto tokenDto = new CreateTokenDto
        {
            ApiKey = "api-key",
            ApiSecret = "api-secret",
            ExpirationMinutes = 60
        };

        _mockTokenBusiness
            .Setup(b => b.CreateToken(
                tokenDto.ApiKey,
                tokenDto.ApiSecret,
                tokenDto.ExpirationMinutes))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _tokenController.CreateToken(tokenDto);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task CreateToken_PassesToBusinessLayer()
    {
        // Arrange
        var expected = "jwt-token";

        CreateTokenDto tokenDto = new CreateTokenDto
        {
            ApiKey = "api-key",
            ApiSecret = "api-secret",
            ExpirationMinutes = 60
        };

        _mockTokenBusiness
            .Setup(b => b.CreateToken(
                tokenDto.ApiKey,
                tokenDto.ApiSecret,
                tokenDto.ExpirationMinutes))
            .ReturnsAsync(expected);

        // Act
        await _tokenController.CreateToken(tokenDto);

        // Assert
        _mockTokenBusiness.Verify(
            b => b.CreateToken(
                tokenDto.ApiKey,
                tokenDto.ApiSecret,
                tokenDto.ExpirationMinutes),
            Times.Once);
    }

    [Fact]
    public void CreateToken_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(TokenController.CreateToken),
            "tokenDto");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // CreateApiKey Tests
    // =========================================================================

    #region CreateApiKey Tests

    [Fact]
    public async Task CreateApiKey_Returns200_WithApiKey()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        var clientId = "client-id";

        TokenResponseDto expected =
            new TokenResponseDto();

        _mockTokenBusiness
            .Setup(b => b.CreateApiKey(UserId, clientId))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tokenController.CreateApiKey(clientId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CreateApiKey_Returns200_WithEmptyApiKey()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        var clientId = "client-id";

        _mockTokenBusiness
            .Setup(b => b.CreateApiKey(UserId, clientId))
            .ReturnsAsync((TokenResponseDto)null!);

        // Act
        var actionResult = await _tokenController.CreateApiKey(clientId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task CreateApiKey_Returns500_OnUnexpectedException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        var clientId = "client-id";

        _mockTokenBusiness
            .Setup(b => b.CreateApiKey(UserId, clientId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _tokenController.CreateApiKey(clientId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task CreateApiKey_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        var clientId = "client-id";

        var expected = new TokenResponseDto();

        _mockTokenBusiness
            .Setup(b => b.CreateApiKey(UserId, clientId))
            .ReturnsAsync(expected);

        // Act
        await _tokenController.CreateApiKey(clientId);

        // Assert
        _mockTokenBusiness.Verify(
            b => b.CreateApiKey(UserId, clientId),
            Times.Once);
    }

    [Fact]
    public void CreateApiKey_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(TokenController.CreateApiKey),
            "clientId");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // DeleteApiKey Tests
    // =========================================================================

    #region DeleteApiKey Tests

    [Fact]
    public async Task DeleteApiKey_Returns200_WithSuccessMessage()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        var key = "api-key";

        _mockTokenBusiness
            .Setup(b => b.DeleteApiKey(UserId, key))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _tokenController.DeleteApiKey(key);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task DeleteApiKey_Returns500_OnUnexpectedException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        var key = "api-key";

        _mockTokenBusiness
            .Setup(b => b.DeleteApiKey(UserId, key))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _tokenController.DeleteApiKey(key);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task DeleteApiKey_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        var key = "api-key";

        _mockTokenBusiness
            .Setup(b => b.DeleteApiKey(UserId, key))
            .ReturnsAsync(true);

        // Act
        await _tokenController.DeleteApiKey(key);

        // Assert
        _mockTokenBusiness.Verify(
            b => b.DeleteApiKey(UserId, key),
            Times.Once);
    }

    [Fact]
    public void DeleteApiKey_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(TokenController.DeleteApiKey),
            "key");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // GetAllUserKeys Tests
    // =========================================================================

    #region GetAllUserKeys Tests

    [Fact]
    public async Task GetAllUserKeys_Returns200_WithKeys()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        List<string> expected =
            new List<string>();

        _mockTokenBusiness
            .Setup(b => b.GetAllUserKeys(UserId))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tokenController.GetAllUserKeys();

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetAllUserKeys_Returns200_WithEmptyList()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockTokenBusiness
            .Setup(b => b.GetAllUserKeys(UserId))
            .ReturnsAsync([]);

        // Act
        var actionResult = await _tokenController.GetAllUserKeys();

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.NotNull(result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

    }

    [Fact]
    public async Task GetAllUserKeys_Returns500_OnUnexpectedException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockTokenBusiness
            .Setup(b => b.GetAllUserKeys(UserId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _tokenController.GetAllUserKeys();

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task GetAllUserKeys_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        var expected = new List<string>();

        _mockTokenBusiness
            .Setup(b => b.GetAllUserKeys(UserId))
            .ReturnsAsync(expected);

        // Act
        await _tokenController.GetAllUserKeys();

        // Assert
        _mockTokenBusiness.Verify(
            b => b.GetAllUserKeys(UserId),
            Times.Once);
    }

    [Fact]
    public void GetAllUserKeys_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(TokenController.GetAllUserKeys));

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // RevokeAllUserTokens Tests
    // =========================================================================

    #region RevokeAllUserTokens Tests

    [Fact]
    public async Task RevokeAllUserTokens_Returns200_WithRevokedCount()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        var expected = 3;

        _mockTokenBusiness
            .Setup(b => b.RevokeAllUserTokens(UserId))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _tokenController.RevokeAllUserTokens();

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task RevokeAllUserTokens_Returns500_OnUnexpectedException()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        _mockTokenBusiness
            .Setup(b => b.RevokeAllUserTokens(UserId))
            .ThrowsAsync(new Exception("db error"));

        // Act
        var actionResult = await _tokenController.RevokeAllUserTokens();

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task RevokeAllUserTokens_PassesToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;

        var expected = 3;

        _mockTokenBusiness
            .Setup(b => b.RevokeAllUserTokens(UserId))
            .ReturnsAsync(expected);

        // Act
        await _tokenController.RevokeAllUserTokens();

        // Assert
        _mockTokenBusiness.Verify(
            b => b.RevokeAllUserTokens(UserId),
            Times.Once);
    }

    [Fact]
    public void RevokeAllUserTokens_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(TokenController.RevokeAllUserTokens));

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
        return Assert.Single(typeof(TokenController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }
}