using System.Reflection;
using Asp.Versioning;
using deeplynx.api.Controllers.V2;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Scalar.AspNetCore;
using V1TokenController = deeplynx.api.Controllers.V1.TokenController;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]
public class TokenControllerTests : IDisposable
{
    private const long UserId = 10L;
    private const long OrganizationId = 20L;
    private const long ProjectId = 30L;
    private const long ServiceAccountId = 40L;
    private const long TestAccountId = 50L;
    private const string ClientId = "oauth-client";
    private const string ApiKey = "api-key";

    private readonly Mock<ITokenBusiness> _mockTokenBusiness = new();
    private readonly TokenController _controller;

    public TokenControllerTests()
    {
        _controller = new TokenController(
            Mock.Of<IEventBusiness>(),
            _mockTokenBusiness.Object,
            Mock.Of<ILogger<TokenController>>());

        UserContextStorage.UserId = UserId;
    }

    public void Dispose()
    {
        UserContextStorage.UserId = default;
        UserContextStorage.OrganizationId = default;
        UserContextStorage.IsSysAdmin = default;
        UserContextStorage.IsOrgAdmin = default;
        UserContextStorage.IsProjectAdmin = default;
    }

    [Fact]
    public async Task CreateToken_ReturnsTokenAndForwardsCredentials()
    {
        var request = new CreateTokenDto
        {
            ApiKey = ApiKey,
            ApiSecret = "api-secret",
            ExpirationMinutes = 60
        };
        const string expected = "jwt-token";
        _mockTokenBusiness
            .Setup(business => business.CreateToken(
                request.ApiKey,
                request.ApiSecret,
                request.ExpirationMinutes))
            .ReturnsAsync(expected);

        var result = await _controller.CreateToken(request);

        AssertOkObject(result, expected);
        _mockTokenBusiness.Verify(
            business => business.CreateToken(
                request.ApiKey,
                request.ApiSecret,
                request.ExpirationMinutes),
            Times.Once);
    }

    [Fact]
    public async Task CreateApiKey_ReturnsKeyAndUsesCurrentUser()
    {
        var expected = CreateTokenResponse();
        _mockTokenBusiness
            .Setup(business => business.CreateApiKey(UserId, ClientId, null, false))
            .ReturnsAsync(expected);

        var result = await _controller.CreateApiKey(ClientId);

        AssertOkObject(result, expected);
        _mockTokenBusiness.Verify(
            business => business.CreateApiKey(UserId, ClientId, null, false),
            Times.Once);
    }

    [Fact]
    public async Task GenerateServiceAccountApiKey_ReturnsKeyAndForwardsContext()
    {
        var expected = CreateTokenResponse();
        _mockTokenBusiness
            .Setup(business => business.GenerateServiceAccountApiKey(
                UserId,
                OrganizationId,
                ProjectId,
                ServiceAccountId))
            .ReturnsAsync(expected);

        var result = await _controller.GenerateServiceAccountApiKey(
            OrganizationId,
            ProjectId,
            ServiceAccountId);

        AssertOkObject(result, expected);
        _mockTokenBusiness.Verify(
            business => business.GenerateServiceAccountApiKey(
                UserId,
                OrganizationId,
                ProjectId,
                ServiceAccountId),
            Times.Once);
    }

    [Fact]
    public async Task GenerateTestAccountApiKey_ReturnsKeyAndUsesCurrentUser()
    {
        var expected = CreateTokenResponse();
        _mockTokenBusiness
            .Setup(business => business.GenerateTestAccountApiKey(UserId, TestAccountId))
            .ReturnsAsync(expected);

        var result = await _controller.GenerateTestAccountApiKey(TestAccountId);

        AssertOkObject(result, expected);
        _mockTokenBusiness.Verify(
            business => business.GenerateTestAccountApiKey(UserId, TestAccountId),
            Times.Once);
    }

    [Fact]
    public async Task DeleteApiKey_ReturnsBooleanAndUsesCurrentUser()
    {
        _mockTokenBusiness
            .Setup(business => business.DeleteApiKey(UserId, ApiKey))
            .ReturnsAsync(true);

        var result = await _controller.DeleteApiKey(ApiKey);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(true, ok.Value);
        _mockTokenBusiness.Verify(
            business => business.DeleteApiKey(UserId, ApiKey),
            Times.Once);
    }

    [Fact]
    public async Task GetAllUserKeys_ReturnsKeysAndUsesCurrentUser()
    {
        var expected = new PaginatedResponse<string>
        {
            Items = new List<string> { "key-one", "key-two" },
            PageNumber = 1,
            PageSize = 25,
            TotalCount = 2
        };

        _mockTokenBusiness
            .Setup(business => business.GetAllUserKeysPaginated(UserId, It.IsAny<PaginatedRequestDto>()))
            .ReturnsAsync(expected);

        var result = (await _controller.GetAllUserKeys()).Result;

        AssertOkObject(result, expected);
        _mockTokenBusiness.Verify(
            business => business.GetAllUserKeysPaginated(UserId, It.IsAny<PaginatedRequestDto>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllUserKeys_Returns200_WithEmptyList()
    {
        _mockTokenBusiness
            .Setup(business => business.GetAllUserKeysPaginated(It.IsAny<long>(), It.IsAny<PaginatedRequestDto>()))
            .ReturnsAsync(new PaginatedResponse<string>
            {
                Items = [],
                PageNumber = 1,
                PageSize = 25,
                TotalCount = 0
            });

        var result = (await _controller.GetAllUserKeys()).Result as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.IsAssignableFrom<PaginatedResponse<string>>(result.Value);
    }

    [Fact]
    public async Task GetAllUserKeys_ThrowsException_WhenBusinessThrows()
    {
        _mockTokenBusiness
            .Setup(business => business.GetAllUserKeysPaginated(It.IsAny<long>(), It.IsAny<PaginatedRequestDto>()))
            .ThrowsAsync(new Exception("db error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.GetAllUserKeys());
    }

    [Fact]
    public async Task GetAllUserKeys_DefaultsPaginatedRequestDto_WhenNotProvided()
    {
        _mockTokenBusiness
            .Setup(business => business.GetAllUserKeysPaginated(
                UserId, It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25)))
            .ReturnsAsync(new PaginatedResponse<string>
            {
                Items = [],
                PageNumber = 1,
                PageSize = 25,
                TotalCount = 0
            });

        await _controller.GetAllUserKeys();

        _mockTokenBusiness.Verify(
            business => business.GetAllUserKeysPaginated(
                UserId, It.Is<PaginatedRequestDto>(p => p.PageNumber == 1 && p.PageSize == 25)),
            Times.Once);
    }

    [Fact]
    public async Task GetAllUserKeys_PassesProvidedPaginatedRequestDto_WhenGiven()
    {
        var pagination = new PaginatedRequestDto { PageNumber = 4, PageSize = 50 };

        _mockTokenBusiness
            .Setup(business => business.GetAllUserKeysPaginated(
                UserId, It.Is<PaginatedRequestDto>(p => p.PageNumber == 4 && p.PageSize == 50)))
            .ReturnsAsync(new PaginatedResponse<string>
            {
                Items = [],
                PageNumber = 4,
                PageSize = 50,
                TotalCount = 0
            });

        await _controller.GetAllUserKeys(pagination);

        _mockTokenBusiness.Verify(
            business => business.GetAllUserKeysPaginated(
                UserId, It.Is<PaginatedRequestDto>(p => p.PageNumber == 4 && p.PageSize == 50)),
            Times.Once);
    }

    [Fact]
    public async Task RevokeAllUserTokens_ReturnsCountAndUsesCurrentUser()
    {
        const int expectedCount = 3;
        _mockTokenBusiness
            .Setup(business => business.RevokeAllUserTokens(UserId))
            .ReturnsAsync(expectedCount);

        var result = await _controller.RevokeAllUserTokens();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedCount, Assert.IsType<int>(ok.Value));
        _mockTokenBusiness.Verify(
            business => business.RevokeAllUserTokens(UserId),
            Times.Once);
    }

    [Fact]
    public async Task V1Action_StillReturnsLegacyUnauthorizedResponse()
    {
        var request = new CreateTokenDto
        {
            ApiKey = ApiKey,
            ApiSecret = "invalid-secret",
            ExpirationMinutes = 60
        };
        _mockTokenBusiness
            .Setup(business => business.CreateToken(
                request.ApiKey,
                request.ApiSecret,
                request.ExpirationMinutes))
            .ThrowsAsync(new UnauthorizedAccessException("invalid credentials"));

        var v1Controller = new V1TokenController(
            Mock.Of<IEventBusiness>(),
            _mockTokenBusiness.Object,
            Mock.Of<ILogger<V1TokenController>>());

        var result = await v1Controller.CreateToken(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task CreateToken_DoesNotConvertBusinessExceptionToLegacyResponse()
    {
        var request = new CreateTokenDto
        {
            ApiKey = ApiKey,
            ApiSecret = "invalid-secret",
            ExpirationMinutes = 60
        };
        var expected = new UnauthorizedAccessException("invalid credentials");
        _mockTokenBusiness
            .Setup(business => business.CreateToken(
                request.ApiKey,
                request.ApiSecret,
                request.ExpirationMinutes))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _controller.CreateToken(request));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task DeleteApiKey_DoesNotConvertBusinessExceptionToLegacyResponse()
    {
        var expected = new KeyNotFoundException("missing key");
        _mockTokenBusiness
            .Setup(business => business.DeleteApiKey(UserId, ApiKey))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _controller.DeleteApiKey(ApiKey));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Controller_DeclaresExpectedRouteVersionsAndAuthorization()
    {
        var controller = typeof(TokenController);
        var versions = controller.GetCustomAttributes<ApiVersionAttribute>().ToList();
        var route = Assert.IsType<RouteAttribute>(controller.GetCustomAttribute<RouteAttribute>());

        Assert.Equal("oauth", route.Template);
        var version = Assert.Single(versions);
        Assert.False(version.Deprecated);
        Assert.Equal(2, Assert.Single(version.Versions).MajorVersion);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
    }

    public static TheoryData<string> VersionedActionPairs => new()
    {
        { nameof(TokenController.CreateToken) },
        { nameof(TokenController.CreateApiKey) },
        { nameof(TokenController.GenerateServiceAccountApiKey) },
        { nameof(TokenController.GenerateTestAccountApiKey) },
        { nameof(TokenController.DeleteApiKey) },
        { nameof(TokenController.GetAllUserKeys) },
        { nameof(TokenController.RevokeAllUserTokens) }
    };

    [Theory]
    [MemberData(nameof(VersionedActionPairs))]
    public void Version2Actions_HaveExpectedMetadata(string methodName)
    {
        var method = GetAction(methodName);

        Assert.DoesNotContain(method.GetCustomAttributesData(), metadata =>
            metadata.AttributeType == typeof(MapToApiVersionAttribute));
        Assert.NotNull(method.GetCustomAttribute<BadgeAttribute>());
        Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
        Assert.True(
            GetProtectedMetadata(method).Length > 0 ||
            typeof(TokenController).GetCustomAttribute<AuthorizeAttribute>() is not null);
    }

    private static TokenResponseDto CreateTokenResponse()
    {
        return new TokenResponseDto
        {
            apiKey = ApiKey,
            apiSecret = "api-secret"
        };
    }

    private static MethodInfo GetAction(string name)
    {
        return Assert.Single(typeof(TokenController).GetMethods(), method => method.Name == name);
    }


    private static (string Attribute, string? Template, string? Name) GetHttpMetadata(MethodInfo method)
    {
        var attribute = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
        return (attribute.GetType().Name, attribute.Template, attribute.Name);
    }

    private static string[] GetProtectedMetadata(MethodInfo method)
    {
        return method.GetCustomAttributes()
            .Where(attribute => attribute is AllowAnonymousAttribute
                or ForbidServiceAccountsAttribute
                or ProjectAdminAttribute
                or SysAdminAttribute
                or TagsAttribute)
            .Select(attribute => attribute.GetType().Name)
            .OrderBy(name => name)
            .ToArray();
    }

    private static void AssertOkObject<T>(IActionResult? result, T expected)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }
}
