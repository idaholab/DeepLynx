using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using deeplynx.api.Controllers.V1;
using deeplynx.business;
using deeplynx.datalayer;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Pgvector.EntityFrameworkCore;

namespace deeplynx.tests.Controllers;

/// <summary>
///     HTTP-level coverage for the OAuth device flow. Most controller tests call controllers directly,
///     but this flow uses a minimal local app because the device grant is an HTTP polling contract.
/// </summary>
[Collection("Test Suite Collection")]
public class OauthDeviceAuthorizationHttpFlowTests : IntegrationTestBase
{
    private const string DeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";
    private const string HostedLink = "https://nexus.example.com";
    private const string VerificationUri = HostedLink + "/oauth/device/verify";

    private readonly TestSuiteFixture _fixture;

    private HttpClient _httpClient = null!;
    private WebApplication _app = null!;

    private string clientId = null!;
    private long userId;
    private string userEmail = null!;

    public OauthDeviceAuthorizationHttpFlowTests(TestSuiteFixture fixture) : base(fixture)
    {
        _fixture = fixture;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await StartTestNexusAsync();
    }

    public override async Task DisposeAsync()
    {
        _httpClient?.Dispose();

        if (_app != null)
        {
            await _app.DisposeAsync();
        }

        Environment.SetEnvironmentVariable("HOSTED_LINK", null);
        UserContextStorage.UserId = 0;
        UserContextStorage.Email = string.Empty;

        await base.DisposeAsync();
    }

    [Fact]
    public async Task DeviceAuthorizationGrantHttpFlow_Succeeds()
    {
        var deviceResponse = await CreateDeviceCode("read write");

        Assert.False(string.IsNullOrWhiteSpace(deviceResponse.DeviceCode));
        Assert.False(string.IsNullOrWhiteSpace(deviceResponse.UserCode));
        Assert.Equal(VerificationUri, deviceResponse.VerificationUri);
        Assert.Contains(deviceResponse.UserCode, deviceResponse.VerificationUriComplete);

        var pendingResponse = await PollDeviceCode(deviceResponse.DeviceCode);
        var pendingError = await ReadJson<OauthErrorResponseDto>(pendingResponse);
        Assert.Equal(HttpStatusCode.BadRequest, pendingResponse.StatusCode);
        Assert.Equal("authorization_pending", pendingError.Error);

        var slowDownResponse = await PollDeviceCode(deviceResponse.DeviceCode);
        var slowDownError = await ReadJson<OauthErrorResponseDto>(slowDownResponse);
        Assert.Equal(HttpStatusCode.BadRequest, slowDownResponse.StatusCode);
        Assert.Equal("slow_down", slowDownError.Error);

        Context.ChangeTracker.Clear();
        var storedRequest = await Context.OauthDeviceAuthorizationRequests.SingleAsync();
        Assert.Equal(10, storedRequest.PollingIntervalSeconds);

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var lookupResponse = await _httpClient.GetAsync(
            $"oauth/device/verify?user_code={Uri.EscapeDataString(deviceResponse.UserCode)}");
        var lookup = await ReadJson<DeviceVerificationLookupResponseDto>(lookupResponse);
        Assert.Equal(HttpStatusCode.OK, lookupResponse.StatusCode);
        Assert.Equal(clientId, lookup.ClientId);
        Assert.Equal("Test OAuth Device HTTP App", lookup.ApplicationName);
        Assert.Equal("read write", lookup.Scope);
        Assert.Equal(OauthDeviceAuthorizationStatus.Pending, lookup.Status);

        var approvalResponse = await _httpClient.PostAsJsonAsync("oauth/device/verify",
            new DeviceAuthorizationDecisionRequestDto
            {
                UserCode = deviceResponse.UserCode,
                Approve = true
            });
        var approval = await ReadJson<DeviceVerificationLookupResponseDto>(approvalResponse);
        Assert.Equal(HttpStatusCode.OK, approvalResponse.StatusCode);
        Assert.Equal(OauthDeviceAuthorizationStatus.Approved, approval.Status);

        _httpClient.DefaultRequestHeaders.Authorization = null;

        var tokenResponse = await PollDeviceCode(deviceResponse.DeviceCode);
        var token = await ReadJson<OauthTokenGrantResponseDto>(tokenResponse);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(token.RefreshToken));
        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal(480 * 60, token.ExpiresIn);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token.AccessToken);
        Assert.Equal(userEmail, jwtToken.Claims.First(claim => claim.Type == "sub").Value);

        var refreshResponse = await RefreshToken(token.RefreshToken!);
        var refreshToken = await ReadJson<OauthTokenGrantResponseDto>(refreshResponse);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(refreshToken.AccessToken));
        Assert.Equal(token.RefreshToken, refreshToken.RefreshToken);
        Assert.Equal("Bearer", refreshToken.TokenType);
    }

    [Fact]
    public async Task DeviceAuthorizationGrantHttpFlow_ReturnsAccessDenied_WhenDenied()
    {
        var deviceResponse = await CreateDeviceCode();

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var denialResponse = await _httpClient.PostAsJsonAsync("oauth/device/verify",
            new DeviceAuthorizationDecisionRequestDto
            {
                UserCode = deviceResponse.UserCode,
                Approve = false
            });
        var denial = await ReadJson<DeviceVerificationLookupResponseDto>(denialResponse);
        Assert.Equal(HttpStatusCode.OK, denialResponse.StatusCode);
        Assert.Equal(OauthDeviceAuthorizationStatus.Denied, denial.Status);

        _httpClient.DefaultRequestHeaders.Authorization = null;

        var tokenResponse = await PollDeviceCode(deviceResponse.DeviceCode);
        var error = await ReadJson<OauthErrorResponseDto>(tokenResponse);
        Assert.Equal(HttpStatusCode.BadRequest, tokenResponse.StatusCode);
        Assert.Equal("access_denied", error.Error);
    }

    [Fact]
    public async Task DeviceAuthorizationGrantHttpFlow_ReturnsExpiredToken_WhenExpired()
    {
        var deviceResponse = await CreateDeviceCode();

        Context.ChangeTracker.Clear();
        var request = await Context.OauthDeviceAuthorizationRequests.SingleAsync();
        request.ExpiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddSeconds(-1), DateTimeKind.Unspecified);
        await Context.SaveChangesAsync();

        _httpClient.DefaultRequestHeaders.Authorization = null;

        var tokenResponse = await PollDeviceCode(deviceResponse.DeviceCode);
        var error = await ReadJson<OauthErrorResponseDto>(tokenResponse);
        Assert.Equal(HttpStatusCode.BadRequest, tokenResponse.StatusCode);
        Assert.Equal("expired_token", error.Error);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User
        {
            Email = "oauth-device-http-test@example.com",
            Name = "OAuth Device HTTP Test User",
            IsActive = true,
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        userId = user.Id;
        userEmail = user.Email;
        clientId = Guid.NewGuid().ToString("N");

        Context.OauthApplications.Add(new OauthApplication
        {
            Name = "Test OAuth Device HTTP App",
            Description = "Test application for device authorization HTTP flow",
            ClientId = clientId,
            ClientSecretHash = BCrypt.Net.BCrypt.HashPassword("device-secret", 12),
            CallbackUrl = "https://example.com/callback",
            BaseUrl = "https://example.com",
            AppOwnerEmail = userEmail,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = userId,
            IsArchived = false
        });
        await Context.SaveChangesAsync();

        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "test-jwt-secret-key-min-32-chars");
        Environment.SetEnvironmentVariable("HOSTED_LINK", HostedLink);
    }

    private async Task StartTestNexusAsync()
    {
        var port = GetFreePort();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test"
        });

        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(TokenController).Assembly);
        builder.Services.AddDbContext<DeeplynxContext>(options =>
            options.UseNpgsql(_fixture.PostgresDataSource, npgsqlOptions => npgsqlOptions.UseVector()));
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddLogging();
        builder.Services.AddScoped<ITokenBusiness, TokenBusiness>();
        builder.Services.AddScoped<IOauthDeviceAuthorizationBusiness, OauthDeviceAuthorizationBusiness>();
        builder.Services.AddSingleton(new Mock<IEventBusiness>().Object);

        _app = builder.Build();
        _app.UsePathBase("/api/v1");
        _app.UseRouting();
        _app.UseAuthentication();
        _app.Use(async (context, next) =>
        {
            UserContextStorage.UserId = userId;
            UserContextStorage.Email = userEmail;
            await next();
        });
        _app.UseAuthorization();
        _app.MapControllers();

        await _app.StartAsync();

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}/api/v1/")
        };
    }

    private async Task<DeviceAuthorizationResponseDto> CreateDeviceCode(string scope = "read")
    {
        var response = await _httpClient.PostAsync("oauth/device/code", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["scope"] = scope
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJson<DeviceAuthorizationResponseDto>(response);
    }

    private Task<HttpResponseMessage> PollDeviceCode(string deviceCode)
    {
        return _httpClient.PostAsync("oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = DeviceCodeGrantType,
            ["device_code"] = deviceCode,
            ["client_id"] = clientId
        }));
    }

    private Task<HttpResponseMessage> RefreshToken(string refreshToken)
    {
        return _httpClient.PostAsync("oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId
        }));
    }

    private static async Task<T> ReadJson<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);

        return value!;
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }

    private class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.Authorization.ToString() != "Test")
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Email, "oauth-device-http-test@example.com")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}