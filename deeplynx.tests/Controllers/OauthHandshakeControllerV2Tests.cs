using System.Reflection;
using Asp.Versioning;
using deeplynx.api.controllers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Scalar.AspNetCore;

namespace deeplynx.tests.Controllers;

[Collection("Test Suite Collection")]
public class OauthHandshakeControllerV2Tests : IDisposable
{
    private const long UserId = 10L;
    private const string ClientId = "client-id";
    private const string ClientSecret = "client-secret";
    private const string Code = "authorization-code";
    private const string RedirectUri = "https://client.example/callback";
    private const string State = "csrf-state";

    private readonly Mock<IOauthHandshakeBusiness> _mockBusiness = new();
    private readonly OauthHandshakeController _controller;

    public OauthHandshakeControllerV2Tests()
    {
        _controller = new OauthHandshakeController(
            _mockBusiness.Object,
            Mock.Of<ILogger<OauthHandshakeController>>());

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
    public async Task AuthorizeV2_RedirectsWithAuthorizationCodeAndState()
    {
        _mockBusiness
            .Setup(business => business.GenerateAuthCode(ClientId, UserId, RedirectUri, State))
            .ReturnsAsync(Code);

        var result = await _controller.AuthorizeV2(ClientId, RedirectUri, State);

        var redirect = Assert.IsType<RedirectResult>(result);
        var callback = new Uri(redirect.Url);
        Assert.Equal("https", callback.Scheme);
        Assert.Equal("client.example", callback.Host);
        Assert.Equal("/callback", callback.AbsolutePath);
        Assert.Contains($"code={Code}", redirect.Url);
        Assert.Contains($"state={State}", redirect.Url);
        _mockBusiness.Verify(
            business => business.GenerateAuthCode(ClientId, UserId, RedirectUri, State),
            Times.Once);
    }

    [Fact]
    public async Task ExchangeV2_ReturnsOAuthTokenResponseAndForwardsArguments()
    {
        const string token = "access-token";
        const double expirationMinutes = 60;
        _mockBusiness
            .Setup(business => business.ExchangeAuthCodeForToken(
                Code,
                ClientId,
                ClientSecret,
                RedirectUri,
                State,
                expirationMinutes))
            .ReturnsAsync(token);

        var result = await _controller.ExchangeV2(
            Code,
            ClientId,
            ClientSecret,
            RedirectUri,
            State,
            expirationMinutes);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(token, Assert.IsType<string>(ok.Value));
        _mockBusiness.Verify(
            business => business.ExchangeAuthCodeForToken(
                Code,
                ClientId,
                ClientSecret,
                RedirectUri,
                State,
                expirationMinutes),
            Times.Once);
    }

    [Fact]
    public async Task ExchangeV2_WhenExpirationIsNull_ReturnsToken()
    {
        _mockBusiness
            .Setup(business => business.ExchangeAuthCodeForToken(
                Code,
                ClientId,
                ClientSecret,
                RedirectUri,
                State,
                null))
            .ReturnsAsync("access-token");

        var result = await _controller.ExchangeV2(
            Code,
            ClientId,
            ClientSecret,
            RedirectUri,
            State,
            null);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("access-token", Assert.IsType<string>(ok.Value));
    }

    [Fact]
    public async Task V1Action_StillReturnsLegacyOAuthErrorResponse()
    {
        _mockBusiness
            .Setup(business => business.ExchangeAuthCodeForToken(
                Code,
                ClientId,
                ClientSecret,
                RedirectUri,
                State,
                null))
            .ThrowsAsync(new InvalidOperationException("invalid grant"));

        var result = await _controller.Exchange(
            Code,
            ClientId,
            ClientSecret,
            RedirectUri,
            State,
            null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AuthorizeV2_DoesNotConvertBusinessExceptionToLegacyResponse()
    {
        var expected = new KeyNotFoundException("invalid client");
        _mockBusiness
            .Setup(business => business.GenerateAuthCode(ClientId, UserId, RedirectUri, State))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _controller.AuthorizeV2(ClientId, RedirectUri, State));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task ExchangeV2_DoesNotConvertBusinessExceptionToLegacyResponse()
    {
        var expected = new InvalidOperationException("invalid grant");
        _mockBusiness
            .Setup(business => business.ExchangeAuthCodeForToken(
                Code,
                ClientId,
                ClientSecret,
                RedirectUri,
                State,
                null))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _controller.ExchangeV2(
                Code,
                ClientId,
                ClientSecret,
                RedirectUri,
                State,
                null));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Controller_DeclaresExpectedRouteVersionsAndAuthorization()
    {
        var controller = typeof(OauthHandshakeController);
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
        { nameof(OauthHandshakeController.Authorize), nameof(OauthHandshakeController.AuthorizeV2) },
        { nameof(OauthHandshakeController.Exchange), nameof(OauthHandshakeController.ExchangeV2) }
    };

    [Theory]
    [MemberData(nameof(VersionedActionPairs))]
    public void VersionedActions_PreserveRouteAndAnonymousMetadata(string v1Name, string v2Name)
    {
        var v1 = GetAction(v1Name);
        var v2 = GetAction(v2Name);

        AssertMappedVersion(v1, 1D);
        AssertMappedVersion(v2, 2D);
        Assert.NotNull(v2.GetCustomAttribute<BadgeAttribute>());
        Assert.NotNull(v1.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(v2.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(GetHttpMetadata(v1), GetHttpMetadata(v2));
    }

    private static MethodInfo GetAction(string name)
    {
        return Assert.Single(
            typeof(OauthHandshakeController).GetMethods(),
            method => method.Name == name);
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
}
