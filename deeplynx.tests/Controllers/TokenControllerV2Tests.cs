using System.Reflection;
using Asp.Versioning;
using deeplynx.api.Controllers;
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

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class TokenControllerV2Tests : IDisposable
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

    public TokenControllerV2Tests()
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
    public async Task CreateTokenV2_ReturnsTokenAndForwardsCredentials()
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

        var result = await _controller.CreateTokenV2(request);

        AssertOkObject(result, expected);
        _mockTokenBusiness.Verify(
            business => business.CreateToken(
                request.ApiKey,
                request.ApiSecret,
                request.ExpirationMinutes),
            Times.Once);
    }

    [Fact]
    public async Task CreateApiKeyV2_ReturnsKeyAndUsesCurrentUser()
    {
        var expected = CreateTokenResponse();
        _mockTokenBusiness
            .Setup(business => business.CreateApiKey(UserId, ClientId, null, false))
            .ReturnsAsync(expected);

        var result = await _controller.CreateApiKeyV2(ClientId);

        AssertOkObject(result, expected);
        _mockTokenBusiness.Verify(
            business => business.CreateApiKey(UserId, ClientId, null, false),
            Times.Once);
    }

    [Fact]
    public async Task GenerateServiceAccountApiKeyV2_ReturnsKeyAndForwardsContext()
    {
        var expected = CreateTokenResponse();
        _mockTokenBusiness
            .Setup(business => business.GenerateServiceAccountApiKey(
                UserId,
                OrganizationId,
                ProjectId,
                ServiceAccountId))
            .ReturnsAsync(expected);

        var result = await _controller.GenerateServiceAccountApiKeyV2(
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
    public async Task GenerateTestAccountApiKeyV2_ReturnsKeyAndUsesCurrentUser()
    {
        var expected = CreateTokenResponse();
        _mockTokenBusiness
            .Setup(business => business.GenerateTestAccountApiKey(UserId, TestAccountId))
            .ReturnsAsync(expected);

        var result = await _controller.GenerateTestAccountApiKeyV2(TestAccountId);

        AssertOkObject(result, expected);
        _mockTokenBusiness.Verify(
            business => business.GenerateTestAccountApiKey(UserId, TestAccountId),
            Times.Once);
    }

    [Fact]
    public async Task DeleteApiKeyV2_ReturnsBooleanAndUsesCurrentUser()
    {
        _mockTokenBusiness
            .Setup(business => business.DeleteApiKey(UserId, ApiKey))
            .ReturnsAsync(true);

        var result = await _controller.DeleteApiKeyV2(ApiKey);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(true, ok.Value);
        _mockTokenBusiness.Verify(
            business => business.DeleteApiKey(UserId, ApiKey),
            Times.Once);
    }

    [Fact]
    public async Task GetAllUserKeysV2_ReturnsKeysAndUsesCurrentUser()
    {
        var expected = new List<string> { "key-one", "key-two" };
        _mockTokenBusiness
            .Setup(business => business.GetAllUserKeys(UserId))
            .ReturnsAsync(expected);

        var result = (await _controller.GetAllUserKeysV2()).Result;

        AssertOkObject(result, expected);
        _mockTokenBusiness.Verify(
            business => business.GetAllUserKeys(UserId),
            Times.Once);
    }

    [Fact]
    public async Task RevokeAllUserTokensV2_ReturnsCountAndUsesCurrentUser()
    {
        const int expectedCount = 3;
        _mockTokenBusiness
            .Setup(business => business.RevokeAllUserTokens(UserId))
            .ReturnsAsync(expectedCount);

        var result = await _controller.RevokeAllUserTokensV2();

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

        var result = await _controller.CreateToken(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task CreateTokenV2_DoesNotConvertBusinessExceptionToLegacyResponse()
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
            () => _controller.CreateTokenV2(request));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task DeleteApiKeyV2_DoesNotConvertBusinessExceptionToLegacyResponse()
    {
        var expected = new KeyNotFoundException("missing key");
        _mockTokenBusiness
            .Setup(business => business.DeleteApiKey(UserId, ApiKey))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _controller.DeleteApiKeyV2(ApiKey));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Controller_DeclaresExpectedRouteVersionsAndAuthorization()
    {
        var controller = typeof(TokenController);
        var versions = controller.GetCustomAttributes<ApiVersionAttribute>().ToList();
        var route = Assert.IsType<RouteAttribute>(controller.GetCustomAttribute<RouteAttribute>());

        Assert.Equal("oauth", route.Template);
        Assert.Equal(2, versions.Count);
        Assert.All(versions, version => Assert.False(version.Deprecated));
        Assert.Contains(versions, version => version.Versions.Any(apiVersion => apiVersion.MajorVersion == 1));
        Assert.Contains(versions, version => version.Versions.Any(apiVersion => apiVersion.MajorVersion == 2));
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
    }

    public static TheoryData<string, string> VersionedActionPairs => new()
    {
        { nameof(TokenController.CreateToken), nameof(TokenController.CreateTokenV2) },
        { nameof(TokenController.CreateApiKey), nameof(TokenController.CreateApiKeyV2) },
        {
            nameof(TokenController.GenerateServiceAccountApiKey),
            nameof(TokenController.GenerateServiceAccountApiKeyV2)
        },
        {
            nameof(TokenController.GenerateTestAccountApiKey),
            nameof(TokenController.GenerateTestAccountApiKeyV2)
        },
        { nameof(TokenController.DeleteApiKey), nameof(TokenController.DeleteApiKeyV2) },
        { nameof(TokenController.GetAllUserKeys), nameof(TokenController.GetAllUserKeysV2) },
        { nameof(TokenController.RevokeAllUserTokens), nameof(TokenController.RevokeAllUserTokensV2) }
    };

    [Theory]
    [MemberData(nameof(VersionedActionPairs))]
    public void VersionedActions_PreserveRouteAndSecurityMetadata(string v1Name, string v2Name)
    {
        var v1 = GetAction(v1Name);
        var v2 = GetAction(v2Name);

        AssertMappedVersion(v1, 1D);
        AssertMappedVersion(v2, 2D);
        Assert.NotNull(v2.GetCustomAttribute<BadgeAttribute>());
        Assert.Equal(GetHttpMetadata(v1), GetHttpMetadata(v2));
        Assert.Equal(GetProtectedMetadata(v1), GetProtectedMetadata(v2));
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

    private static void AssertMappedVersion(MethodInfo method, double expectedVersion)
    {
        var attribute = Assert.Single(method.GetCustomAttributesData(), metadata =>
            metadata.AttributeType == typeof(MapToApiVersionAttribute));
        Assert.Equal(expectedVersion, Assert.IsType<double>(attribute.ConstructorArguments[0].Value));
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
