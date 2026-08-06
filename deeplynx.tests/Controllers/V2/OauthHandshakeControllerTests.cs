using System.Reflection;
using Asp.Versioning;
using deeplynx.api.Controllers.V2;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Scalar.AspNetCore;
using V1OauthHandshakeController = deeplynx.api.Controllers.V1.OauthHandshakeController;

namespace deeplynx.tests.Controllers.V2;

[Collection("Test Suite Collection")]
public class OauthHandshakeControllerTests : IDisposable
{
    private const long UserId = 10L;
    private const string ClientId = "client-id";
    private const string ClientSecret = "client-secret";
    private const string Code = "authorization-code";
    private const string RedirectUri = "https://client.example/callback";
    private const string State = "csrf-state";

    private readonly Mock<IOauthHandshakeBusiness> _mockBusiness = new();
    private readonly OauthHandshakeController _controller;

    public OauthHandshakeControllerTests()
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
    public async Task Authorize_RedirectsWithAuthorizationCodeAndState()
    {
        _mockBusiness
            .Setup(business => business.GenerateAuthCode(ClientId, UserId, RedirectUri, State))
            .ReturnsAsync(Code);

        var result = await _controller.Authorize(ClientId, RedirectUri, State);

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
    public async Task Exchange_ReturnsOAuthTokenResponseAndForwardsArguments()
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

        var result = await _controller.Exchange(
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
    public async Task Exchange_WhenExpirationIsNull_ReturnsToken()
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

        var result = await _controller.Exchange(
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

        var v1Controller = new V1OauthHandshakeController(
            _mockBusiness.Object,
            Mock.Of<ILogger<V1OauthHandshakeController>>());

        var result = await v1Controller.Exchange(
            Code,
            ClientId,
            ClientSecret,
            RedirectUri,
            State,
            null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Authorize_DoesNotConvertBusinessExceptionToLegacyResponse()
    {
        var expected = new KeyNotFoundException("invalid client");
        _mockBusiness
            .Setup(business => business.GenerateAuthCode(ClientId, UserId, RedirectUri, State))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _controller.Authorize(ClientId, RedirectUri, State));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task Exchange_DoesNotConvertBusinessExceptionToLegacyResponse()
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
            _controller.Exchange(
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
        var version = Assert.Single(versions);
        Assert.False(version.Deprecated);
        Assert.Equal(2, Assert.Single(version.Versions).MajorVersion);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
    }

    public static TheoryData<string> VersionedActionPairs => new()
    {
        { nameof(OauthHandshakeController.Authorize) },
        { nameof(OauthHandshakeController.Exchange) }
    };

    [Theory]
    [MemberData(nameof(VersionedActionPairs))]
    public void Version2Actions_HaveExpectedMetadata(string methodName)
    {
        var method = GetAction(methodName);

        Assert.DoesNotContain(method.GetCustomAttributesData(), metadata =>
            metadata.AttributeType == typeof(MapToApiVersionAttribute));
        Assert.NotNull(method.GetCustomAttribute<BadgeAttribute>());
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
    }

    private static MethodInfo GetAction(string name)
    {
        return Assert.Single(
            typeof(OauthHandshakeController).GetMethods(),
            method => method.Name == name);
    }


    private static (string Attribute, string? Template, string? Name) GetHttpMetadata(MethodInfo method)
    {
        var attribute = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
        return (attribute.GetType().Name, attribute.Template, attribute.Name);
    }
}
