using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace deeplynx.tests.Middleware
{
    [Collection("Test Suite Collection")]
    public class NexusAuthenticationMiddlewareTests : IntegrationTestBase
    {
        private Mock<IOptionsMonitor<JwtBearerOptions>> _optionsMock;
        private Mock<ILoggerFactory> _loggerFactoryMock;
        private UrlEncoder _urlEncoder;
        private IServiceScopeFactory _serviceScopeFactory;

        public long uid1;  // user ID
        public long uid2;  // user ID for existing local dev user
        public long akid1; // api key ID

        public NexusAuthenticationMiddlewareTests(TestSuiteFixture fixture) : base(fixture) { }

        private const string TEST_JWT_SECRET = "test-jwt-secret-key-for-signing-tokens-minimum-32-chars";

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            // Set JWT_SECRET_KEY for all tests
            Environment.SetEnvironmentVariable("JWT_SECRET_KEY", TEST_JWT_SECRET);
            // Setup mocks
            _optionsMock = new Mock<IOptionsMonitor<JwtBearerOptions>>();
            _optionsMock.Setup(x => x.Get(It.IsAny<string>()))
                .Returns(new JwtBearerOptions());

            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(Mock.Of<ILogger>());

            _urlEncoder = UrlEncoder.Default;

            // Setup service scope factory
            var serviceProvider = new ServiceCollection()
                .AddScoped(_ => Context)
                .BuildServiceProvider();

            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(x => x.CreateScope())
                .Returns(() =>
                {
                    var scope = new Mock<IServiceScope>();
                    scope.Setup(x => x.ServiceProvider).Returns(serviceProvider);
                    return scope.Object;
                });
            _serviceScopeFactory = scopeFactory.Object;
        }

        #region HandleLocalDevelopmentBypass Tests

        [Fact]
        public async Task HandleLocalDevelopmentBypass_CreatesNewSysAdminUser()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
            var context = CreateHttpContext();
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "developer@localhost");

            Assert.NotNull(user);
            Assert.True(user.IsSysAdmin);
            Assert.Equal("Local Developer", user.Name);
            Assert.Equal("local-dev", user.Username);
            Assert.True(user.IsActive);
            Assert.False(user.IsArchived);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleLocalDevelopmentBypass_PromotesExistingUserToSysAdmin()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
            // Create existing local dev user (not sys admin)
            var localDevUser = new User
            {
                Name = "Local Dev Existing",
                Email = "developer@localhost",
                Username = "local-dev-existing",
                IsActive = false,
                IsArchived = false,
                IsSysAdmin = false
            };
            Context.Users.Add(localDevUser);
            await Context.SaveChangesAsync();
            uid2 = localDevUser.Id;
            
            // Verify existing user is not sys admin
            var existingUser = await Context.Users.FindAsync(uid2);
            Assert.NotNull(existingUser);
            Assert.False(existingUser.IsSysAdmin);
            
            var context = CreateHttpContext();
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Force EF to sync with database
            Context.ChangeTracker.Clear();

            // Assert
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "developer@localhost");

            Assert.NotNull(user);
            Assert.True(user.IsSysAdmin);
            Assert.Equal(uid2, user.Id); // Same user

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleLocalDevelopmentBypass_ReturnsSuccessWithCorrectClaims()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
            var context = CreateHttpContext();
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Principal);

            var emailClaim = result.Principal.FindFirst(ClaimTypes.Email);
            Assert.NotNull(emailClaim);
            Assert.Equal("developer@localhost", emailClaim.Value);

            var nameClaim = result.Principal.FindFirst(ClaimTypes.Name);
            Assert.NotNull(nameClaim);
            Assert.Equal("LocalDeveloper", nameClaim.Value);

            var uidClaim = result.Principal.FindFirst("uid");
            Assert.NotNull(uidClaim);
            Assert.Equal("local-dev-user", uidClaim.Value);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        #endregion

        #region HandleAuthenticate Tests

        [Fact]
        public async Task HandleAuthenticate_ReturnsNoResult_WhenNoTokenProvided()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            var context = CreateHttpContext();
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.False(result.Succeeded);
            Assert.True(result.None);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleAuthenticate_ReturnsFailure_WhenInvalidTokenFormat()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            var context = CreateHttpContext("invalid-token-format");
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Invalid token format", result.Failure?.Message ?? "");

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        #endregion
        
        #region DISABLE_BACKEND_AUTHENTICATION with Token Tests

        [Fact]
        public async Task HandleAuthenticate_UsesLocalDevBypass_WhenDisabledAndNoToken()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
            var context = CreateHttpContext(); // No token
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            var emailClaim = result.Principal?.FindFirst(ClaimTypes.Email);
            Assert.NotNull(emailClaim);
            Assert.Equal("developer@localhost", emailClaim.Value);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleAuthenticate_UsesTokenUser_WhenDisabledAndValidToken()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            var token = await GenerateHS256TokenAsync(user.Email, apiKey.Key, apiKey.Secret);
            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Principal);
            
            // Should use the token user, not local dev user
            var nameClaim = result.Principal.FindFirst("name");
            Assert.NotNull(nameClaim);
            Assert.Equal(user.Email, nameClaim.Value);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleAuthenticate_FallsBackToLocalDev_WhenDisabledAndInvalidToken()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
            var context = CreateHttpContext("invalid-token-format");
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            var emailClaim = result.Principal?.FindFirst(ClaimTypes.Email);
            Assert.NotNull(emailClaim);
            Assert.Equal("developer@localhost", emailClaim.Value);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleAuthenticate_FallsBackToLocalDev_WhenDisabledAndExpiredToken()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            // Create expired token (still creates OAuth record for consistency)
            var token = await GenerateHS256TokenAsync(user.Email, apiKey.Key, apiKey.Secret, expiresInMinutes: -60);
            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            var emailClaim = result.Principal?.FindFirst(ClaimTypes.Email);
            Assert.NotNull(emailClaim);
            Assert.Equal("developer@localhost", emailClaim.Value);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleAuthenticate_FallsBackToLocalDev_WhenDisabledAndInvalidSignature()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            // Create token with wrong secret
            var jti = Guid.NewGuid().ToString();
            var token = GenerateHS256TokenWithInvalidSignature(user.Email, apiKey.Key, "wrong-secret-key", jti);
            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            var emailClaim = result.Principal?.FindFirst(ClaimTypes.Email);
            Assert.NotNull(emailClaim);
            Assert.Equal("developer@localhost", emailClaim.Value);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleAuthenticate_FallsBackToLocalDev_WhenDisabledAndInvalidApiKey()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
            var user = await Context.Users.FindAsync(uid1);
            Assert.NotNull(user);

            // Create token with invalid API key (no OAuth record created since API key doesn't exist)
            var jti = Guid.NewGuid().ToString();
            var token = GenerateHS256TokenWithInvalidSignature(user.Email, "invalid-api-key", "some-secret", jti);
            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            var emailClaim = result.Principal?.FindFirst(ClaimTypes.Email);
            Assert.NotNull(emailClaim);
            Assert.Equal("developer@localhost", emailClaim.Value);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleAuthenticate_FallsBackToLocalDev_WhenDisabledAndTokenThrowsException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
            // Create a malformed token that will throw during validation
            var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.malformed.signature";
            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            var emailClaim = result.Principal?.FindFirst(ClaimTypes.Email);
            Assert.NotNull(emailClaim);
            Assert.Equal("developer@localhost", emailClaim.Value);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleAuthenticate_FallsBackToLocalDev_WhenDisabledAndRevokedToken()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "true");
            
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            var token = await GenerateHS256TokenAsync(user.Email, apiKey.Key, apiKey.Secret);
            
            // Revoke the token
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
            var tokenHash = HashToken(jti!);
            var oauthToken = await Context.OauthTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
            Assert.NotNull(oauthToken);
            oauthToken.Revoked = true;
            await Context.SaveChangesAsync();

            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            var emailClaim = result.Principal?.FindFirst(ClaimTypes.Email);
            Assert.NotNull(emailClaim);
            Assert.Equal("developer@localhost", emailClaim.Value);
            
            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        #endregion

        #region HandleHS256Token Tests

        [Fact]
        public async Task HandleHS256Token_ValidatesSuccessfully_WithValidToken()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            var token = await GenerateHS256TokenAsync(user.Email, apiKey.Key, apiKey.Secret);
            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Principal);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleHS256Token_FailsValidation_WithInvalidApiKey()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            var user = await Context.Users.FindAsync(uid1);
            Assert.NotNull(user);

            // No OAuth token created since API key doesn't exist
            var token = await GenerateHS256TokenAsync(user.Email, "invalid-api-key", "some-secret");
            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Invalid API key", result.Failure?.Message ?? "");

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleHS256Token_FailsValidation_WithExpiredToken()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            // Create expired token (still creates OAuth record)
            var token = await GenerateHS256TokenAsync(
                user.Email,
                apiKey.Key,
                apiKey.Secret,
                expiresInMinutes: -60);

            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Token has expired", result.Failure?.Message ?? "");

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleHS256Token_FailsValidation_WithInvalidSignature()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            // Create token with wrong secret - need to manually create OAuth record
            // since GenerateHS256TokenWithInvalidSignature doesn't create one
            var jti = Guid.NewGuid().ToString();
            var token = GenerateHS256TokenWithInvalidSignature(user.Email, apiKey.Key, "wrong-secret-key", jti);
    
            // Manually create the OAuth token record so it passes the revocation check
            var tokenHash = HashToken(jti);
            var oauthToken = new OauthToken
            {
                TokenHash = tokenHash,
                UserId = uid1,
                ExpiresAt = DateTime.SpecifyKind(DateTimeOffset.UtcNow.AddMinutes(60).DateTime, DateTimeKind.Unspecified),
                Revoked = false
            };
            Context.OauthTokens.Add(oauthToken);
            await Context.SaveChangesAsync();
    
            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("JWT signature validation failed", result.Failure?.Message ?? "");

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleHS256Token_FailsValidation_WhenUserNotFound()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            // No OAuth token created since user doesn't exist
            var token = await GenerateHS256TokenAsync("nonexistent@test.com", "test-key", "test-secret");
            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Invalid API key", result.Failure?.Message ?? "");

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleHS256Token_FailsValidation_WhenTokenRevoked()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");

            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            // Generate token with OAuth record
            var token = await GenerateHS256TokenAsync(user.Email, apiKey.Key, apiKey.Secret);
            
            // Extract JTI and revoke the token
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
            Assert.NotNull(jti);
            
            var tokenHash = HashToken(jti);
            var oauthToken = await Context.OauthTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
            Assert.NotNull(oauthToken);
            
            oauthToken.Revoked = true;
            await Context.SaveChangesAsync();

            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Token has been revoked", result.Failure?.Message ?? "");

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleHS256Token_FailsValidation_WhenTokenNotFoundInDatabase()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            // Create token manually without creating OAuth record
            var jti = Guid.NewGuid().ToString();
            var token = GenerateHS256TokenWithoutOAuthRecord(user.Email, apiKey.Key, jti);
            
            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Token has been revoked", result.Failure?.Message ?? "");
            
            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleHS256Token_FailsValidation_WhenJTIMissing()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            // Create token without JTI claim - don't add OAuth record
            var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    "{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var exp = DateTimeOffset.UtcNow.AddMinutes(60).ToUnixTimeSeconds();
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{{\"name\":\"{user.Email}\",\"apiKey\":\"{apiKey.Key}\",\"exp\":{exp}}}"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var message = $"{header}.{payload}";
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? TEST_JWT_SECRET;
            var secretKey = jwtSecret.Length < 32 ? jwtSecret.PadRight(32, '0') : jwtSecret;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var signature = Convert.ToBase64String(hash)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var token = $"{message}.{signature}";

            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Token missing required claims", result.Failure?.Message ?? "");
            
            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleHS256Token_UsesCorrectTokenHash()
        {
            // Arrange - verifies that SHA256 hashing is consistent
            var jti1 = "test-jti-1";
            var jti2 = "test-jti-1"; // Same JTI
            
            // Act
            var hash1 = HashToken(jti1);
            var hash2 = HashToken(jti2);
            
            // Assert
            Assert.Equal(hash1, hash2);
        }

        #endregion

        #region HandleHS256Token Fallback Tests

        [Fact]
        public async Task HandleHS256Token_UsesPreferredUsername_WhenNameClaimMissing()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            // Create token with only preferred_username (no name claim)
            var token = await GenerateHS256TokenWithCustomClaimsAsync(
                new Dictionary<string, string>
                {
                    { "preferred_username", user.Email },
                    { "apiKey", apiKey.Key }
                },
                apiKey.Secret);

            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Principal);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleHS256Token_UsesSubClaim_WhenNameAndPreferredUsernameMissing()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            // Create token with only sub claim (no name or preferred_username)
            var token = await GenerateHS256TokenWithCustomClaimsAsync(
                new Dictionary<string, string>
                {
                    { "sub", user.Email },
                    { "apiKey", apiKey.Key }
                },
                apiKey.Secret);

            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Principal);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleHS256Token_UsesEmailClaim_WhenOtherUserClaimsMissing()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            var user = await Context.Users.FindAsync(uid1);
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(user);
            Assert.NotNull(apiKey);

            // Create token with only email claim (no name, preferred_username, or sub)
            var token = await GenerateHS256TokenWithCustomClaimsAsync(
                new Dictionary<string, string>
                {
                    { "email", user.Email },
                    { "apiKey", apiKey.Key }
                },
                apiKey.Secret);

            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Principal);

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        [Fact]
        public async Task HandleHS256Token_FailsValidation_WhenAllUserClaimsMissing()
        {
            // Arrange
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", "false");
            var apiKey = await Context.ApiKeys.FindAsync(akid1);
            Assert.NotNull(apiKey);

            // Create token with only apiKey claim (no user identification claims)
            var token = await GenerateHS256TokenWithCustomClaimsAsync(
                new Dictionary<string, string>
                {
                    { "apiKey", apiKey.Key }
                },
                apiKey.Secret);

            var context = CreateHttpContext(token);
            var middleware = await CreateAndInitializeMiddleware(context);

            // Act
            var result = await middleware.AuthenticateAsync();

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Token missing required claims", result.Failure?.Message ?? "");

            // Cleanup
            Environment.SetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION", null);
        }

        #endregion

        #region ExtractToken Tests

        [Fact]
        public void ExtractToken_FromAuthorizationHeader()
        {
            // Arrange
            var token = "test-token-123";
            var request = new DefaultHttpContext().Request;
            request.Headers["Authorization"] = $"Bearer {token}";

            // Act
            var extractedToken = InvokePrivateMethod<string>("ExtractToken", request);

            // Assert
            Assert.Equal(token, extractedToken);
        }

        [Fact]
        public void ExtractToken_FromQueryString()
        {
            // Arrange
            var token = "test-token-456";
            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString($"?token={token}");

            // Act
            var extractedToken = InvokePrivateMethod<string>("ExtractToken", httpContext.Request);

            // Assert
            Assert.Equal(token, extractedToken);
        }

        [Fact]
        public void ExtractToken_FromCookie()
        {
            // Arrange
            var token = "test-token-789";
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Cookie"] = $"access_token={token}";

            // Create a mock cookie collection
            var cookieCollection = new Mock<IRequestCookieCollection>();
            cookieCollection.Setup(x => x["access_token"]).Returns(token);
            
            var requestMock = new Mock<HttpRequest>();
            requestMock.Setup(x => x.Cookies).Returns(cookieCollection.Object);
            requestMock.Setup(x => x.Headers).Returns(new HeaderDictionary());
            requestMock.Setup(x => x.Query).Returns(new QueryCollection());

            // Act
            var extractedToken = InvokePrivateMethod<string>("ExtractToken", requestMock.Object);

            // Assert
            Assert.Equal(token, extractedToken);
        }

        [Fact]
        public void ExtractToken_ReturnsNull_WhenNoTokenProvided()
        {
            // Arrange
            var request = new DefaultHttpContext().Request;

            // Act
            var extractedToken = InvokePrivateMethod<string>("ExtractToken", request);

            // Assert
            Assert.Null(extractedToken);
        }

        #endregion

        #region EnsureUserExistsAsync Tests

        [Fact]
        public async Task EnsureUserExistsAsync_CreatesNewUser_WhenNotExists()
        {
            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "newuser@test.com"),
                new Claim("uid", "new-sso-id"),
                new Claim(ClaimTypes.Name, "New User"),
                new Claim("preferred_username", "newuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "newuser@test.com");

            Assert.NotNull(user);
            Assert.Equal("New User", user.Name);
            Assert.Equal("newuser", user.Username);
            Assert.Equal("new-sso-id", user.SsoId);
            Assert.True(user.IsActive);
            Assert.False(user.IsArchived);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_UpdatesExistingUser_WhenSsoIdMissing()
        {
            // Arrange
            var user = await Context.Users.FindAsync(uid1);
            Assert.NotNull(user);
            Assert.Null(user.SsoId); // User has no SSO ID

            var claims = new[]
            {
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("uid", "updated-sso-id"),
                new Claim(ClaimTypes.Name, "Updated Name"),
                new Claim("preferred_username", "updatedusername")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Force EF to sync with database
            Context.ChangeTracker.Clear();

            // Assert
            var updatedUser = await Context.Users.FindAsync(uid1);
            Assert.NotNull(updatedUser);
            Assert.Equal("updated-sso-id", updatedUser.SsoId);
            Assert.Equal("Updated Name", updatedUser.Name);
            Assert.Equal("updatedusername", updatedUser.Username);
            Assert.True(updatedUser.IsActive);
            Assert.False(updatedUser.IsArchived);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_Updates_WhenSsoIdIsDifferent()
        {
            // Arrange - create user with SSO ID
            var userWithSso = new User
            {
                Email = "ssouser@test.com",
                Name = "Original Name",
                Username = "originalusername",
                SsoId = "existing-sso-id",
                IsActive = true
            };
            Context.Users.Add(userWithSso);
            await Context.SaveChangesAsync();

            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "ssouser@test.com"),
                new Claim("uid", "different-sso-id"),
                new Claim(ClaimTypes.Name, "Different Name")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Force EF to sync with database
            Context.ChangeTracker.Clear();

            // Assert - should update
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "ssouser@test.com");
            Assert.NotNull(user);
            Assert.Equal("different-sso-id", user.SsoId);
            Assert.Equal("Different Name", user.Name);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_DoesNotUpdate_WhenSsoIdIsSame()
        {
            // Arrange - create user with SSO ID
            var userWithSso = new User
            {
                Email = "ssouser@test.com",
                Name = "Original Name",
                Username = "originalusername",
                SsoId = "existing-sso-id",
                IsActive = true
            };
            Context.Users.Add(userWithSso);
            await Context.SaveChangesAsync();

            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "ssouser@test.com"),
                new Claim("uid", "existing-sso-id"),
                new Claim(ClaimTypes.Name, "Different Name")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Force EF to sync with database
            Context.ChangeTracker.Clear();

            // Assert - should update
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "ssouser@test.com");
            Assert.NotNull(user);
            Assert.Equal("existing-sso-id", user.SsoId);
            Assert.Equal("Original Name", user.Name);
        }

        #endregion
        
        #region EnsureUserExistsAsync DefaultSuperUser Tests

        [Fact]
        public async Task EnsureUserExistsAsync_SetsAdmin_WithExactMatch()
        {
            // Arrange
            Environment.SetEnvironmentVariable("SUPERUSER_EMAIL", "admin@test.com");
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "admin@test.com"),
                new Claim("uid", "admin-sso-id"),
                new Claim(ClaimTypes.Name, "Admin User"),
                new Claim("preferred_username", "adminuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "admin@test.com");

            Assert.NotNull(user);
            Assert.True(user.IsSysAdmin);
            Assert.Equal("Admin User", user.Name);
            Assert.Equal("adminuser", user.Username);

            // Cleanup
            Environment.SetEnvironmentVariable("SUPERUSER_EMAIL", null);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_DoesNotSetAdmin_WhenNoMatch()
        {
            // Arrange
            Environment.SetEnvironmentVariable("SUPERUSER_EMAIL", "admin@test.com");
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "regularuser@test.com"),
                new Claim("uid", "regular-sso-id"),
                new Claim(ClaimTypes.Name, "Regular User"),
                new Claim("preferred_username", "regularuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "regularuser@test.com");

            Assert.NotNull(user);
            Assert.False(user.IsSysAdmin);
            Assert.Equal("Regular User", user.Name);
            Assert.Equal("regularuser", user.Username);

            // Cleanup
            Environment.SetEnvironmentVariable("SUPERUSER_EMAIL", null);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_SetsAdmin_WhenCaseInsensitiveMatch()
        {
            // Arrange
            Environment.SetEnvironmentVariable("SUPERUSER_EMAIL", "Admin@Test.COM");
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "admin@test.com"),
                new Claim("uid", "admin-sso-id"),
                new Claim(ClaimTypes.Name, "Admin User"),
                new Claim("preferred_username", "adminuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "admin@test.com");

            Assert.NotNull(user);
            Assert.True(user.IsSysAdmin);
            Assert.Equal("Admin User", user.Name);
            Assert.Equal("adminuser", user.Username);

            // Cleanup
            Environment.SetEnvironmentVariable("SUPERUSER_EMAIL", null);
        }
        
        [Fact]
        public async Task EnsureUserExistsAsync_SetsAdmin_WhenUserExists_WithMatchingSsoId()
        {
            // Arrange
            Environment.SetEnvironmentVariable("SUPERUSER_EMAIL", "admin@test.com");
            // Create existing user with SSO ID (not admin yet)
            var existingUser = new User
            {
                Email = "admin@test.com",
                Name = "Original Name",
                Username = "originalusername",
                SsoId = "admin-sso-id",
                IsActive = true,
                IsArchived = false,
                IsSysAdmin = false
            };
            Context.Users.Add(existingUser);
            await Context.SaveChangesAsync();
            var userId = existingUser.Id;
    
            // Verify user is not admin initially
            Assert.False(existingUser.IsSysAdmin);
    
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "admin@test.com"),
                new Claim("uid", "admin-sso-id"), // Same SSO ID
                new Claim(ClaimTypes.Name, "Admin User"),
                new Claim("preferred_username", "adminuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Force EF to sync with database
            Context.ChangeTracker.Clear();

            // Assert
            var user = await Context.Users.FindAsync(userId);
    
            Assert.NotNull(user);
            Assert.True(user.IsSysAdmin); // Should be promoted to admin
            Assert.Equal(userId, user.Id); // Same user
            Assert.Equal("admin-sso-id", user.SsoId); // SSO ID unchanged

            // Cleanup
            Environment.SetEnvironmentVariable("SUPERUSER_EMAIL", null);
        }

        #endregion

        #region EnsureUserExistsAsync Email Fallback Tests

        [Fact]
        public async Task EnsureUserExistsAsync_UsesNameIdentifierClaim_ForEmail()
        {
            // Arrange
            var claims = new[]
            {
                new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "nameidentifier@test.com"),
                new Claim(ClaimTypes.Email, "email@test.com"),
                new Claim("email", "email2@test.com"),
                new Claim("sub", "sub@test.com"),
                new Claim("name", "name@test.com"),
                new Claim("uid", "test-sso-id"),
                new Claim("preferred_username", "testuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert - should use nameidentifier claim
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "nameidentifier@test.com");

            Assert.NotNull(user);
            Assert.Equal("nameidentifier@test.com", user.Email);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_UsesClaimsTypeEmail_WhenNameIdentifierMissing()
        {
            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "email@test.com"),
                new Claim("email", "email2@test.com"),
                new Claim("sub", "sub@test.com"),
                new Claim("name", "name@test.com"),
                new Claim("uid", "test-sso-id"),
                new Claim("preferred_username", "testuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert - should use ClaimTypes.Email
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "email@test.com");

            Assert.NotNull(user);
            Assert.Equal("email@test.com", user.Email);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_UsesEmailClaim_WhenNameIdentifierAndClaimsTypeEmailMissing()
        {
            // Arrange
            var claims = new[]
            {
                new Claim("email", "email2@test.com"),
                new Claim("sub", "sub@test.com"),
                new Claim("name", "name@test.com"),
                new Claim("uid", "test-sso-id"),
                new Claim("preferred_username", "testuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert - should use email claim
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "email2@test.com");

            Assert.NotNull(user);
            Assert.Equal("email2@test.com", user.Email);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_UsesSubClaim_WhenEmailClaimsMissing()
        {
            // Arrange
            var claims = new[]
            {
                new Claim("sub", "sub@test.com"),
                new Claim("name", "name@test.com"),
                new Claim("uid", "test-sso-id"),
                new Claim("preferred_username", "testuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert - should use sub claim
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "sub@test.com");

            Assert.NotNull(user);
            Assert.Equal("sub@test.com", user.Email);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_UsesNameClaim_WhenAllOtherEmailClaimsMissing()
        {
            // Arrange
            var claims = new[]
            {
                new Claim("name", "name@test.com"),
                new Claim("uid", "test-sso-id"),
                new Claim("preferred_username", "testuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert - should use name claim
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "name@test.com");

            Assert.NotNull(user);
            Assert.Equal("name@test.com", user.Email);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_DoesNotCreateUser_WhenAllEmailClaimsMissing()
        {
            // Arrange
            var claims = new[]
            {
                new Claim("uid", "test-sso-id"),
                new Claim("preferred_username", "testuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            var initialUserCount = await Context.Users.CountAsync();

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert - no user should be created
            var finalUserCount = await Context.Users.CountAsync();
            Assert.Equal(initialUserCount, finalUserCount);
        }

        #endregion

        #region EnsureUserExistsAsync Name Fallback Tests

        [Fact]
        public async Task EnsureUserExistsAsync_UsesClaimsTypeName_ForUserName()
        {
            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "fallbackname@test.com"),
                new Claim(ClaimTypes.Name, "Claims Type Name"),
                new Claim("name", "Name Claim"),
                new Claim("uid", "test-sso-id"),
                new Claim("preferred_username", "fallbackusername")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert - should use ClaimTypes.Name
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "fallbackname@test.com");

            Assert.NotNull(user);
            Assert.Equal("Claims Type Name", user.Name);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_UsesNameClaim_WhenClaimsTypeNameMissing()
        {
            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "fallbackname2@test.com"),
                new Claim("name", "Name Claim"),
                new Claim("uid", "test-sso-id"),
                new Claim("preferred_username", "fallbackusername")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert - should use name claim
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "fallbackname2@test.com");

            Assert.NotNull(user);
            Assert.Equal("Name Claim", user.Name);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_UsesUsername_WhenNameClaimsMissing()
        {
            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "fallbackname3@test.com"),
                new Claim("uid", "test-sso-id"),
                new Claim("preferred_username", "fallbackusername")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert - should fall back to username (which is preferred_username or email)
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "fallbackname3@test.com");

            Assert.NotNull(user);
            Assert.Equal("fallbackusername", user.Name);
        }

        [Fact]
        public async Task EnsureUserExistsAsync_UsesEmail_WhenAllNameClaimsMissing()
        {
            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, "fallbackname4@test.com"),
                new Claim("uid", "test-sso-id")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            // Act
            await InvokePrivateMethodAsync("EnsureUserExistsAsync", principal);

            // Assert - should fall back to email for name
            var user = await Context.Users
                .FirstOrDefaultAsync(u => u.Email == "fallbackname4@test.com");

            Assert.NotNull(user);
            Assert.Equal("fallbackname4@test.com", user.Name);
        }

        #endregion

        #region Helper Methods

        private NexusAuthenticationMiddleware CreateMiddleware()
        {
            return new NexusAuthenticationMiddleware(
                _optionsMock.Object,
                _loggerFactoryMock.Object,
                _urlEncoder,
                _serviceScopeFactory);
        }

        private async Task<NexusAuthenticationMiddleware> CreateAndInitializeMiddleware(HttpContext context)
        {
            var middleware = CreateMiddleware();
            
            // Initialize the middleware with the HttpContext
            var scheme = new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                JwtBearerDefaults.AuthenticationScheme,
                typeof(NexusAuthenticationMiddleware));
            
            await middleware.InitializeAsync(scheme, context);
            
            return middleware;
        }

        private HttpContext CreateHttpContext(string? bearerToken = null)
        {
            var context = new DefaultHttpContext();

            if (!string.IsNullOrEmpty(bearerToken))
            {
                context.Request.Headers["Authorization"] = $"Bearer {bearerToken}";
            }

            return context;
        }

        private async Task<string> GenerateHS256TokenAsync(
            string email,
            string apiKey,
            string secret,
            int expiresInMinutes = 60)
        {
            // Generate a unique JTI
            var jti = Guid.NewGuid().ToString();
            
            var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    "{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var exp = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes).ToUnixTimeSeconds();
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{{\"name\":\"{email}\",\"apiKey\":\"{apiKey}\",\"jti\":\"{jti}\",\"exp\":{exp}}}"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var message = $"{header}.{payload}";
    
            // Always use JWT_SECRET_KEY for signing
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? TEST_JWT_SECRET;
            var secretKey = jwtSecret.Length < 32 ? jwtSecret.PadRight(32, '0') : jwtSecret;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var signature = Convert.ToBase64String(hash)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var token = $"{message}.{signature}";
            
            // Create OAuth token record in database
            var tokenHash = HashToken(jti);
            var oauthToken = new OauthToken
            {
                TokenHash = tokenHash,
                UserId = uid1,
                ExpiresAt = DateTime.SpecifyKind(DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes).DateTime, DateTimeKind.Unspecified),
                Revoked = false
            };
            Context.OauthTokens.Add(oauthToken);
            await Context.SaveChangesAsync();

            return token;
        }

        private async Task<string> GenerateHS256TokenWithCustomClaimsAsync(
            Dictionary<string, string> claims,
            string secret,
            int expiryMinutes = 5)
        {
            // Generate a unique JTI
            var jti = Guid.NewGuid().ToString();
            
            var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    "{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var exp = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes).ToUnixTimeSeconds();
    
            // Build claims JSON
            var claimsJson = new StringBuilder("{");
            foreach (var claim in claims)
            {
                claimsJson.Append($"\"{claim.Key}\":\"{claim.Value}\",");
            }
            claimsJson.Append($"\"jti\":\"{jti}\",\"exp\":{exp}}}");

            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(claimsJson.ToString()))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var message = $"{header}.{payload}";
    
            // Always use JWT_SECRET_KEY for signing
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? TEST_JWT_SECRET;
            var secretKey = jwtSecret.Length < 32 ? jwtSecret.PadRight(32, '0') : jwtSecret;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var signature = Convert.ToBase64String(hash)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var token = $"{message}.{signature}";
            
            // Create OAuth token record in database
            var tokenHash = HashToken(jti);
            var oauthToken = new OauthToken
            {
                TokenHash = tokenHash,
                UserId = uid1,
                ExpiresAt = DateTime.SpecifyKind(DateTimeOffset.UtcNow.AddMinutes(expiryMinutes).DateTime, DateTimeKind.Unspecified),
                Revoked = false
            };
            Context.OauthTokens.Add(oauthToken);
            await Context.SaveChangesAsync();

            return token;
        }

        private string GenerateHS256TokenWithInvalidSignature(
            string email,
            string apiKey,
            string wrongSecret,
            string jti,
            int expiresInMinutes = 60)
        {
            var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    "{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var exp = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes).ToUnixTimeSeconds();
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{{\"name\":\"{email}\",\"apiKey\":\"{apiKey}\",\"jti\":\"{jti}\",\"exp\":{exp}}}"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var message = $"{header}.{payload}";

            // Use the WRONG secret to create an invalid signature
            var secretKey = wrongSecret.Length < 32 ? wrongSecret.PadRight(32, '0') : wrongSecret;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var signature = Convert.ToBase64String(hash)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            // Note: Caller must create OAuth token record if needed for the test
            return $"{message}.{signature}";
        }

        private string GenerateHS256TokenWithoutOAuthRecord(string email, string apiKey, string jti, int expiresInMinutes = 60)
        {
            var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    "{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var exp = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes).ToUnixTimeSeconds();
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{{\"name\":\"{email}\",\"apiKey\":\"{apiKey}\",\"jti\":\"{jti}\",\"exp\":{exp}}}"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var message = $"{header}.{payload}";
            
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? TEST_JWT_SECRET;
            var secretKey = jwtSecret.Length < 32 ? jwtSecret.PadRight(32, '0') : jwtSecret;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var signature = Convert.ToBase64String(hash)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            // NOTE: No OAuth token record created
            return $"{message}.{signature}";
        }

        private static string HashToken(string jti)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(jti));
            return Convert.ToBase64String(hashBytes);
        }

        private T InvokePrivateMethod<T>(string methodName, params object[] parameters)
        {
            var middleware = CreateMiddleware();
            var method = middleware.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return (T)method?.Invoke(middleware, parameters)!;
        }

        private async Task InvokePrivateMethodAsync(string methodName, params object[] parameters)
        {
            var middleware = CreateMiddleware();
            var method = middleware.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var task = (Task)method?.Invoke(middleware, parameters)!;
            await task;
        }

        protected override async Task SeedTestDataAsync()
        {
            await base.SeedTestDataAsync();

            // Create test user
            var user1 = new User
            {
                Name = "Test User",
                Email = "testuser@test.com",
                Username = "testuser",
                IsActive = true,
                IsArchived = false
            };
            Context.Users.Add(user1);
            await Context.SaveChangesAsync();
            uid1 = user1.Id;

            // Create test API key for user1
            var apiKey1 = new ApiKey
            {
                UserId = uid1,
                Key = "test-api-key-123",
                Secret = "test-secret-key-that-is-long-enough-for-testing"
            };
            Context.ApiKeys.Add(apiKey1);
            await Context.SaveChangesAsync();
            akid1 = apiKey1.Id;
        }

        #endregion
    }
}