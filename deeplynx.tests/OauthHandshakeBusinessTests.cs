using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class OauthHandshakeBusinesTests : IntegrationTestBase
{
    private Mock<ILogger<OauthHandshakeBusiness>> _mockLogger = null!;
    private OauthHandshakeBusiness _oauthHandshakeBusiness;
    private TokenBusiness _tokenBusiness;
    private long applicationId;
    private string callbackUrl;
    private string clientId;
    private string clientSecret;
    private string clientSecretHash;
    private string state;

    private long uid1;
    private string userEmail;

    public OauthHandshakeBusinesTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mockLogger = new Mock<ILogger<OauthHandshakeBusiness>>();
        _tokenBusiness = new TokenBusiness(Context);
        _oauthHandshakeBusiness = new OauthHandshakeBusiness(
            Context,
            _mockLogger.Object,
            _tokenBusiness);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Create test users
        var user1 = new User
        {
            Email = "oauth-test@example.com",
            Name = "OAuth Test User"
        };
        Context.Users.Add(user1);
        await Context.SaveChangesAsync();
        uid1 = user1.Id;
        userEmail = user1.Email;

        // Create OAuth application
        clientId = GenerateClientId();
        clientSecret = GenerateClientSecret();
        clientSecretHash = BCrypt.Net.BCrypt.HashPassword(clientSecret, 12);
        callbackUrl = "https://example.com/callback";

        var oauthApp = new OauthApplication
        {
            Name = "Test OAuth App",
            Description = "Test application for handshake tests",
            ClientId = clientId,
            ClientSecretHash = clientSecretHash,
            CallbackUrl = callbackUrl,
            BaseUrl = "https://example.com",
            AppOwnerEmail = userEmail,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid1,
            IsArchived = false
        };

        Context.OauthApplications.Add(oauthApp);
        await Context.SaveChangesAsync();
        applicationId = oauthApp.Id;

        state = "test-state";
    }

    #region GenerateAuthCode Tests

    [Fact]
    public async Task GenerateAuthCode_Success_ReturnsUrlSafeCode()
    {
        // Act
        var authCode = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, state);

        // Assert
        Assert.NotNull(authCode);
        Assert.NotEmpty(authCode);

        // Verify URL-safe characters
        Assert.DoesNotContain("+", authCode);
        Assert.DoesNotContain("/", authCode);
        Assert.DoesNotContain("=", authCode);
    }

    [Fact]
    public async Task GenerateAuthCode_Success_StoresDataInCache()
    {
        // Act
        var authCode = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, state);

        // Assert
        var cacheKey = $"auth_code:{authCode}";
        var cachedData = await CacheService.Instance.GetAsync<OauthCodeData>(cacheKey);

        Assert.NotNull(cachedData);
        Assert.Equal(authCode, cachedData.Code);
        Assert.Equal(applicationId, cachedData.ApplicationId);
        Assert.Equal(callbackUrl, cachedData.RedirectUri);
        Assert.Equal(uid1, cachedData.UserId);
        Assert.Equal(state, cachedData.State);
        Assert.False(cachedData.IsUsed);
        Assert.True(cachedData.ExpiresAt > DateTime.UtcNow.AddMinutes(9));
    }

    [Fact]
    public async Task GenerateAuthCode_Success_GeneratesUniqueCode()
    {
        // Act
        var authCode1 = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, "state1");
        var authCode2 = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, "state2");

        // Assert
        Assert.NotEqual(authCode1, authCode2);
    }

    #endregion

    #region ExchangeAuthCodeForToken Tests

    [Fact]
    public async Task ExchangeAuthCodeForToken_Success_ReturnsToken()
    {
        // Arrange
        var authCode = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, state);

        // Act
        var token = await _oauthHandshakeBusiness.ExchangeAuthCodeForToken(
            authCode, clientId, clientSecret, callbackUrl, state, null);

        // Assert
        Assert.NotNull(token);

        // Decode and verify JWT
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token); // Decode without validation
        Assert.NotNull(jwtToken);

        // Verify that the token claims match what is expected
        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
        var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
        Assert.NotNull(subClaim);
        Assert.NotNull(nameClaim);
        Assert.NotNull(jti);
        Assert.Equal(userEmail, subClaim);
        Assert.Equal(userEmail, nameClaim);

        // Verify that a token matching the hashed jti was inserted into the DB
        var jtiHash = HashToken(jti);
        var dbToken = await Context.OauthTokens.Where(t => t.TokenHash == jtiHash).FirstOrDefaultAsync();
        Assert.NotNull(dbToken);

        // Verify that the stored DB info matches what is expected
        Assert.Equal(dbToken.ApplicationId, applicationId);
        Assert.Equal(dbToken.UserId, uid1);
    }

    [Fact]
    public async Task ExchangeAuthCodeForToken_Success_WithCustomExpiration()
    {
        // Arrange
        var authCode = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, state);

        // Act
        var token = await _oauthHandshakeBusiness.ExchangeAuthCodeForToken(
            authCode, clientId, clientSecret, callbackUrl, state, 123);

        // Assert
        Assert.NotNull(token);

        // Decode and verify JWT
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token); // Decode without validation
        Assert.NotNull(jwtToken);

        // Verify that a token matching the hashed jti was inserted into the DB
        var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
        Assert.NotNull(jti);
        var jtiHash = HashToken(jti);
        var dbToken = await Context.OauthTokens.Where(t => t.TokenHash == jtiHash).FirstOrDefaultAsync();
        Assert.NotNull(dbToken);

        // Verify that the stored DB expiration is 123 minutes past CreatedAt
        var expectedExpiration = dbToken.CreatedAt.AddMinutes(123);
        // account for slight time differences
        var timeDifference = (dbToken.ExpiresAt - dbToken.CreatedAt).TotalMinutes;
        Assert.Equal(123, timeDifference, 0); // 0 decimal places
    }

    [Fact]
    public async Task ExchangeAuthCodeForToken_Success_MarksCodeAsUsed()
    {
        // Arrange
        var authCode = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, state);

        // Act
        await _oauthHandshakeBusiness.ExchangeAuthCodeForToken(
            authCode, clientId, clientSecret, callbackUrl, state, null);

        // Assert
        var cacheKey = $"auth_code:{authCode}";
        var cachedData = await CacheService.Instance.GetAsync<OauthCodeData>(cacheKey);
        Assert.True(cachedData.IsUsed);
    }

    #endregion

    #region ValidateAndConsumeAuthCode Tests (Private Method)

    [Fact]
    public async Task ValidateAndConsumeAuthCode_Throws_WhenCodeNotFound()
    {
        // Arrange
        var invalidCode = "invalid-code";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokePrivateMethodAsync<OauthCodeData>(
                "ValidateAndConsumeAuthCode",
                invalidCode, clientId, clientSecret, callbackUrl, state));

        Assert.Contains("Invalid authorization code", ex.Message);
    }

    [Fact]
    public async Task ValidateAndConsumeAuthCode_Throws_WhenCodeAlreadyUsed()
    {
        // Arrange
        var authCode = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, state);

        // Use the code once
        await _oauthHandshakeBusiness.ExchangeAuthCodeForToken(
            authCode, clientId, clientSecret, callbackUrl, state, null);

        // Act & Assert - Try to use it again
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokePrivateMethodAsync<OauthCodeData>(
                "ValidateAndConsumeAuthCode",
                authCode, clientId, clientSecret, callbackUrl, state));

        Assert.Contains("Authorization code has already been used", ex.Message);
    }

    [Fact]
    public async Task ValidateAndConsumeAuthCode_Throws_WhenCodeExpired()
    {
        // Arrange
        var authCode = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, state);

        // Manually expire the code in cache
        var cacheKey = $"auth_code:{authCode}";
        var cachedData = await CacheService.Instance.GetAsync<OauthCodeData>(cacheKey);
        cachedData.ExpiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-1), DateTimeKind.Unspecified);
        await CacheService.Instance.SetAsync(cacheKey, cachedData, TimeSpan.FromMinutes(1));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokePrivateMethodAsync<OauthCodeData>(
                "ValidateAndConsumeAuthCode",
                authCode, clientId, clientSecret, callbackUrl, state));

        Assert.Contains("Authorization code has expired", ex.Message);
    }

    [Fact]
    public async Task ValidateAndConsumeAuthCode_Throws_WhenApplicationIdMismatch()
    {
        // Arrange
        // Create a second application
        var secondApp = new OauthApplication
        {
            Name = "Second App",
            ClientId = "second-client-id",
            ClientSecretHash = BCrypt.Net.BCrypt.HashPassword("second-secret", 12),
            CallbackUrl = callbackUrl,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid1,
            IsArchived = false
        };
        Context.OauthApplications.Add(secondApp);
        await Context.SaveChangesAsync();

        var authCode = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, state);

        // Try to exchange with different client ID
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokePrivateMethodAsync<OauthCodeData>(
                "ValidateAndConsumeAuthCode",
                authCode, "second-client-id", "second-secret", callbackUrl, state));

        Assert.Contains("mismatched client_id", ex.Message);
    }

    [Fact]
    public async Task ValidateAndConsumeAuthCode_Throws_WhenUserNotFound()
    {
        // Arrange
        var authCode = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, state);

        // Delete the user
        var user = await Context.Users.FindAsync(uid1);
        Context.Users.Remove(user!);
        await Context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokePrivateMethodAsync<OauthCodeData>(
                "ValidateAndConsumeAuthCode",
                authCode, clientId, clientSecret, callbackUrl, state));

        Assert.Contains("missing userId", ex.Message);
    }

    [Fact]
    public async Task ValidateAndConsumeAuthCode_Success_ReturnsAuthCodeData()
    {
        // Arrange
        var authCode = await _oauthHandshakeBusiness.GenerateAuthCode(
            clientId, uid1, callbackUrl, state);

        // Act
        var result = await InvokePrivateMethodAsync<OauthCodeData>(
            "ValidateAndConsumeAuthCode",
            authCode, clientId, clientSecret, callbackUrl, state);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(authCode, result.Code);
        Assert.Equal(applicationId, result.ApplicationId);
        Assert.Equal(uid1, result.UserId);
        Assert.True(result.IsUsed);
    }

    #endregion

    #region ValidateOauthApplication Tests (Private Method)

    [Fact]
    public async Task ValidateOauthApplication_Success_WhenExists()
    {
        // Act
        var result = await InvokePrivateMethodAsync<OauthApplication>(
            "ValidateOauthApplication",
            clientId, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(clientId, result.ClientId);
        Assert.Equal(applicationId, result.Id);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task ValidateOauthApplication_Throws_WhenNotFound()
    {
        // Arrange
        var invalidClientId = "invalid-client-id";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            InvokePrivateMethodAsync<OauthApplication>(
                "ValidateOauthApplication",
                invalidClientId, null));

        Assert.Contains("invalid client_id", ex.Message);
    }

    [Fact]
    public async Task ValidateOauthApplication_Throws_WhenArchived()
    {
        // Arrange
        var app = await Context.OauthApplications.FindAsync(applicationId);
        app!.IsArchived = true;
        await Context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            InvokePrivateMethodAsync<OauthApplication>(
                "ValidateOauthApplication",
                clientId, null));

        Assert.Contains("invalid client_id", ex.Message);
    }

    [Fact]
    public async Task ValidateOauthApplication_Throws_WhenApplicationIdMismatch()
    {
        // Arrange
        var wrongApplicationId = 99999L;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokePrivateMethodAsync<OauthApplication>(
                "ValidateOauthApplication",
                clientId, wrongApplicationId));

        Assert.Contains("mismatched client_id", ex.Message);
    }

    #endregion

    #region ValidateRedirectUri Tests (Private Method)

    [Fact]
    public void ValidateRedirectUri_Success_WhenExactMatch()
    {
        // Act & Assert - Should not throw
        InvokePrivateMethod(
            "ValidateRedirectUri",
            callbackUrl, callbackUrl);
    }

    [Fact]
    public void ValidateRedirectUri_Success_WithDifferentCase()
    {
        // Arrange
        var upperCaseCallbackUrl = callbackUrl.ToUpper();

        // Act & Assert - Should not throw
        InvokePrivateMethod(
            "ValidateRedirectUri",
            upperCaseCallbackUrl, callbackUrl);
    }

    [Fact]
    public void ValidateRedirectUri_Throws_WhenMismatch()
    {
        // Arrange
        var wrongRedirectUri = "https://wrong.com/callback";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InvokePrivateMethod(
                "ValidateRedirectUri",
                wrongRedirectUri, callbackUrl));

        Assert.Contains("unknown redirect_uri", ex.Message);
    }

    #endregion

    #region VerifyClientSecret Tests (Private Method)

    [Fact]
    public void VerifyClientSecret_Success_WhenSecretMatches()
    {
        // Act & Assert - Should not throw
        InvokePrivateMethod(
            "VerifyClientSecret",
            clientSecret, clientSecretHash, clientId);
    }

    [Fact]
    public void VerifyClientSecret_Throws_WhenSecretInvalid()
    {
        // Arrange
        var wrongSecret = "wrong-secret";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InvokePrivateMethod(
                "VerifyClientSecret",
                wrongSecret, clientSecretHash, clientId));

        Assert.Contains("invalid client_secret", ex.Message);
    }

    #endregion

    #region ValidateState Tests (Private Method)

    [Fact]
    public void ValidateState_Success_WhenStateMatches()
    {
        // Arrange
        var state = "test-state-value";

        // Act & Assert - Should not throw
        InvokePrivateMethod(
            "ValidateState",
            state, state, clientId);
    }

    [Fact]
    public void ValidateState_Throws_WhenStateMismatch()
    {
        // Arrange
        var providedState = "state1";
        var storedState = "state2";

        // Act & Assert
        var ex = Assert.Throws<SecurityException>(() =>
            InvokePrivateMethod(
                "ValidateState",
                providedState, storedState, clientId));

        Assert.Contains("State parameter mismatch - possible CSRF attack", ex.Message);
    }

    [Fact]
    public void ValidateState_Throws_WhenCaseMismatch()
    {
        // Arrange
        var providedState = "CaseSensitiveState";
        var storedState = "casesensitivestate";

        // Act & Assert
        var ex = Assert.Throws<SecurityException>(() =>
            InvokePrivateMethod(
                "ValidateState",
                providedState, storedState, clientId));

        Assert.Contains("State parameter mismatch - possible CSRF attack", ex.Message);
    }

    #endregion

    #region Helper Methods

    // Helper methods for generating test data
    private string GenerateClientId()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var base64 = Convert.ToBase64String(bytes);
        return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private string GenerateClientSecret()
    {
        var bytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var base64 = Convert.ToBase64String(bytes);
        return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private void InvokePrivateMethod(string methodName, params object[] parameters)
    {
        var method = _oauthHandshakeBusiness.GetType().GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);

        try
        {
            method?.Invoke(_oauthHandshakeBusiness, parameters);
        }
        catch (TargetInvocationException ex)
        {
            // Unwrap and rethrow the actual exception
            throw ex.InnerException ?? ex;
        }
    }

    private async Task<T> InvokePrivateMethodAsync<T>(string methodName, params object[] parameters)
    {
        var method = _oauthHandshakeBusiness.GetType().GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);

        try
        {
            var task = (Task<T>)method?.Invoke(_oauthHandshakeBusiness, parameters)!;
            return await task;
        }
        catch (TargetInvocationException ex)
        {
            // Unwrap and rethrow the actual exception
            throw ex.InnerException ?? ex;
        }
    }

    // Helpers copied from TokenBusiness
    private string HashToken(string jti)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(jti));
        return Convert.ToBase64String(hashBytes);
    }

    #endregion
}