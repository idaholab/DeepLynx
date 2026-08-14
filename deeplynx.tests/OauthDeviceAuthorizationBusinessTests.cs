using System.IdentityModel.Tokens.Jwt;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers.exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class OauthDeviceAuthorizationBusinessTests : IntegrationTestBase
{
    private const string VerificationUri = "https://nexus.example.com/oauth/device/verify";

    private Mock<ILogger<OauthDeviceAuthorizationBusiness>> _mockLogger = null!;
    private OauthDeviceAuthorizationBusiness _oauthDeviceAuthorizationBusiness = null!;
    private TokenBusiness _tokenBusiness = null!;

    private long applicationId;
    private string clientId = null!;
    private long userId;
    private string userEmail = null!;

    public OauthDeviceAuthorizationBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _mockLogger = new Mock<ILogger<OauthDeviceAuthorizationBusiness>>();
        _tokenBusiness = new TokenBusiness(Context);
        _oauthDeviceAuthorizationBusiness = new OauthDeviceAuthorizationBusiness(
            Context,
            _tokenBusiness,
            _mockLogger.Object);
    }

    [Fact]
    public async Task DeviceAuthorizationGrantFlow_Succeeds()
    {
        // Device starts the flow
        var deviceResponse = await _oauthDeviceAuthorizationBusiness.CreateDeviceAuthorizationRequest(
            clientId,
            "read write",
            VerificationUri);

        Assert.False(string.IsNullOrWhiteSpace(deviceResponse.DeviceCode));
        Assert.False(string.IsNullOrWhiteSpace(deviceResponse.UserCode));
        Assert.Equal(VerificationUri, deviceResponse.VerificationUri);
        Assert.Contains(deviceResponse.UserCode, deviceResponse.VerificationUriComplete);

        // Headless client polls while user approval is pending
        var pending = await Assert.ThrowsAsync<OauthException>(() =>
            _oauthDeviceAuthorizationBusiness.ExchangeDeviceCodeForToken(deviceResponse.DeviceCode, clientId));
        Assert.Equal("authorization_pending", pending.ErrorCode);

        var slowDown = await Assert.ThrowsAsync<OauthException>(() =>
            _oauthDeviceAuthorizationBusiness.ExchangeDeviceCodeForToken(deviceResponse.DeviceCode, clientId));
        Assert.Equal("slow_down", slowDown.ErrorCode);

        var storedRequest = await Context.OauthDeviceAuthorizationRequests.SingleAsync();
        Assert.Equal(10, storedRequest.PollingIntervalSeconds);

        // Authenticated user verifies and approves the request
        var lookup = await _oauthDeviceAuthorizationBusiness.GetDeviceAuthorizationRequest(deviceResponse.UserCode);
        Assert.Equal(clientId, lookup.ClientId);
        Assert.Equal("Test OAuth Device App", lookup.ApplicationName);
        Assert.Equal("read write", lookup.Scope);
        Assert.Equal(OauthDeviceAuthorizationStatus.Pending, lookup.Status);

        var approved = await _oauthDeviceAuthorizationBusiness.SetDeviceAuthorizationDecision(
            deviceResponse.UserCode,
            true,
            userId);
        Assert.Equal(OauthDeviceAuthorizationStatus.Approved, approved.Status);

        // Headless client receives access and refresh tokens
        var tokenResponse = await _oauthDeviceAuthorizationBusiness.ExchangeDeviceCodeForToken(
            deviceResponse.DeviceCode,
            clientId);

        Assert.False(string.IsNullOrWhiteSpace(tokenResponse.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokenResponse.RefreshToken));
        Assert.Equal("Bearer", tokenResponse.TokenType);
        Assert.Equal(480 * 60, tokenResponse.ExpiresIn);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenResponse.AccessToken);
        Assert.Equal(userEmail, jwtToken.Claims.First(claim => claim.Type == "sub").Value);

        Context.ChangeTracker.Clear();
        Assert.False(await Context.OauthDeviceAuthorizationRequests.AnyAsync());

        var refreshTokenRecord = await Context.OauthRefreshTokens.SingleAsync();
        Assert.Equal(applicationId, refreshTokenRecord.ApplicationId);
        Assert.Equal(userId, refreshTokenRecord.UserId);
        Assert.False(refreshTokenRecord.Revoked);

        // Refresh token can silently get a new access token
        var refreshResponse = await _oauthDeviceAuthorizationBusiness.ExchangeRefreshTokenForToken(
            tokenResponse.RefreshToken,
            clientId);

        Assert.False(string.IsNullOrWhiteSpace(refreshResponse.AccessToken));
        Assert.Equal(tokenResponse.RefreshToken, refreshResponse.RefreshToken);
        Assert.Equal("Bearer", refreshResponse.TokenType);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User
        {
            Email = "oauth-device-test@example.com",
            Name = "OAuth Device Test User"
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        userId = user.Id;
        userEmail = user.Email;
        clientId = Guid.NewGuid().ToString("N");

        var oauthApplication = new OauthApplication
        {
            Name = "Test OAuth Device App",
            Description = "Test application for device authorization flow",
            ClientId = clientId,
            ClientSecretHash = BCrypt.Net.BCrypt.HashPassword("device-secret", 12),
            CallbackUrl = "https://example.com/callback",
            BaseUrl = "https://example.com",
            AppOwnerEmail = userEmail,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = userId,
            IsArchived = false
        };

        Context.OauthApplications.Add(oauthApplication);
        await Context.SaveChangesAsync();

        applicationId = oauthApplication.Id;
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "test-jwt-secret-key-min-32-chars");
    }
}
