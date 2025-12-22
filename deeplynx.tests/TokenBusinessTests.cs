// TokenBusinessTests.cs

using System.IdentityModel.Tokens.Jwt;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.helpers.Hubs;
using deeplynx.models;
using deeplynx.interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging;

namespace deeplynx.tests
{
    [Collection("Test Suite Collection")]
    public class TokenBusinessTests : IntegrationTestBase
    {
        private TokenBusiness _tokenBusiness;
        
        private long uid1;
        private long uid2;
        private string userEmail;
        private string clientId;
        private long applicationId;
        private string apiKey1;
        private string plaintextSecret1;
        private string hashedSecret1;

        public TokenBusinessTests(TestSuiteFixture fixture) : base(fixture)
        {
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            _tokenBusiness = new TokenBusiness(Context);
        }

        #region CreateToken Tests

        [Fact]
        public async Task CreateToken_ReturnsJwt_WhenVerifySucceeds_AndSecretExists()
        {
            // Act - Pass the PLAINTEXT secret to CreateToken
            var jwt = await _tokenBusiness.CreateToken(apiKey1, plaintextSecret1, expiration: 5);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(jwt));
            var handler = new JwtSecurityTokenHandler();
            var parsed = handler.ReadJwtToken(jwt);
            Assert.Equal(apiKey1, parsed.Claims.First(c => c.Type == "apiKey").Value);
            Assert.True(parsed.ValidTo > DateTime.UtcNow);
            
            // Verify token was saved to database
            var jti = parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
            var tokenHash = HashToken(jti);
            var savedToken = Context.OauthTokens.FirstOrDefault(t => t.TokenHash == tokenHash);
            Assert.NotNull(savedToken);
            Assert.Equal(uid1, savedToken!.UserId);
            Assert.Equal(applicationId, savedToken.ApplicationId);
            Assert.False(savedToken.Revoked);
        }

        [Fact]
        public async Task CreateToken_Throws_WhenVerifyFails()
        {
            // Pass wrong plaintext secret
            var wrongSecret = "wrong-secret";

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _tokenBusiness.CreateToken(apiKey1, wrongSecret, expiration: 5));
            Assert.Contains("Invalid API credentials", ex.Message);
        }

        [Fact]
        public async Task CreateToken_Throws_WhenApiKeyNotFound()
        {
            // Arrange
            var nonExistentApiKey = "does-not-exist";
            var someSecret = "any-secret";

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _tokenBusiness.CreateToken(nonExistentApiKey, plaintextSecret1, expiration: 5));
            Assert.Contains("API key not found", ex.Message);
        }

        [Fact]
        public async Task CreateToken_Throws_WhenApplicationArchived()
        {
            // Arrange TODO fix
            UserContextStorage.Email = userEmail;

            // Archive the application
            var app = Context.OauthApplications.Find(applicationId);
            app!.IsArchived = true;
            await Context.SaveChangesAsync();
            
            Context.ApiKeys.Add(new ApiKey 
            { 
                Key = "archived-key", 
                Secret = hashedSecret1, 
                UserId = uid1,
                ApplicationId = applicationId
            });
            await Context.SaveChangesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _tokenBusiness.CreateToken(apiKey1, plaintextSecret1, expiration: 5));
            Assert.Contains("archived", ex.Message);
        }

        #endregion

        #region RevokeToken Tests

        [Fact]
        public async Task RevokeToken_Success_WhenTokenExists()
        {
            // Arrange
            var apiKey = "revoke-test-key";
            var plaintextSecret = "revoke-secret";
            var hashedSecret = _tokenBusiness.HashApiSecret(plaintextSecret);
            
            Context.ApiKeys.Add(new ApiKey 
            { 
                Key = apiKey, 
                Secret = hashedSecret, 
                UserId = uid1,
                ApplicationId = applicationId
            });
            await Context.SaveChangesAsync();

            var jwt = await _tokenBusiness.CreateToken(apiKey, plaintextSecret, expiration: 5);
            var handler = new JwtSecurityTokenHandler();
            var parsed = handler.ReadJwtToken(jwt);
            var jti = parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            // Act
            var result = await _tokenBusiness.RevokeToken(jti);

            // Assert
            Assert.True(result);
            var isRevoked = await _tokenBusiness.IsTokenRevoked(jti);
            Assert.True(isRevoked);
        }

        [Fact]
        public async Task RevokeToken_ReturnsFalse_WhenAlreadyRevoked()
        {
            // Arrange
            var apiKey = "already-revoked-key";
            var plaintextSecret = "already-revoked-secret";
            var hashedSecret = _tokenBusiness.HashApiSecret(plaintextSecret);
            
            Context.ApiKeys.Add(new ApiKey 
            { 
                Key = apiKey, 
                Secret = hashedSecret, 
                UserId = uid1,
                ApplicationId = applicationId
            });
            await Context.SaveChangesAsync();

            var jwt = await _tokenBusiness.CreateToken(apiKey, plaintextSecret, expiration: 5);
            var handler = new JwtSecurityTokenHandler();
            var parsed = handler.ReadJwtToken(jwt);
            var jti = parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            await _tokenBusiness.RevokeToken(jti);

            // Act
            var result = await _tokenBusiness.RevokeToken(jti);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RevokeToken_Throws_WhenTokenNotFound()
        {
            // Arrange
            var nonExistentJti = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _tokenBusiness.RevokeToken(nonExistentJti));
        }

        #endregion

        #region IsTokenRevoked Tests

        [Fact]
        public async Task IsTokenRevoked_ReturnsFalse_WhenTokenNotRevoked()
        {
            // Arrange
            var apiKey = "check-revoked-key";
            var plaintextSecret = "check-revoked-secret";
            var hashedSecret = _tokenBusiness.HashApiSecret(plaintextSecret);
            
            Context.ApiKeys.Add(new ApiKey 
            { 
                Key = apiKey, 
                Secret = hashedSecret, 
                UserId = uid1,
                ApplicationId = applicationId
            });
            await Context.SaveChangesAsync();

            var jwt = await _tokenBusiness.CreateToken(apiKey, plaintextSecret, expiration: 5);
            var handler = new JwtSecurityTokenHandler();
            var parsed = handler.ReadJwtToken(jwt);
            var jti = parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            // Act
            var isRevoked = await _tokenBusiness.IsTokenRevoked(jti);

            // Assert
            Assert.False(isRevoked);
        }

        [Fact]
        public async Task IsTokenRevoked_ReturnsTrue_WhenTokenRevoked()
        {
            // Arrange
            var apiKey = "revoked-check-key";
            var plaintextSecret = "revoked-check-secret";
            var hashedSecret = _tokenBusiness.HashApiSecret(plaintextSecret);
            
            Context.ApiKeys.Add(new ApiKey 
            { 
                Key = apiKey, 
                Secret = hashedSecret, 
                UserId = uid1,
                ApplicationId = applicationId
            });
            await Context.SaveChangesAsync();

            var jwt = await _tokenBusiness.CreateToken(apiKey, plaintextSecret, expiration: 5);
            var handler = new JwtSecurityTokenHandler();
            var parsed = handler.ReadJwtToken(jwt);
            var jti = parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            await _tokenBusiness.RevokeToken(jti);

            // Act
            var isRevoked = await _tokenBusiness.IsTokenRevoked(jti);

            // Assert
            Assert.True(isRevoked);
        }
        #endregion

        #region RevokeAllUserTokens Tests

        [Fact]
        public async Task RevokeAllUserTokens_RevokesAllTokensForUser()
        {
           // Create multiple tokens for the user
            var tokens = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var apiKey = $"bulk-revoke-key-{i}";
                var plaintextSecret = $"bulk-revoke-secret-{i}";
                var hashedSecret = _tokenBusiness.HashApiSecret(plaintextSecret);
                
                Context.ApiKeys.Add(new ApiKey 
                { 
                    Key = apiKey, 
                    Secret = hashedSecret, 
                    UserId = uid1,
                    ApplicationId = applicationId
                });
                Context.SaveChanges();

                var jwt = await _tokenBusiness.CreateToken(apiKey, plaintextSecret, expiration: 5);
                tokens.Add(jwt);
            }

            // Act
            var count = await _tokenBusiness.RevokeAllUserTokens(uid1);

            // Assert
            Assert.Equal(3, count);

            foreach (var jwt in tokens)
            {
                var handler = new JwtSecurityTokenHandler();
                var parsed = handler.ReadJwtToken(jwt);
                var jti = parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
                var isRevoked = await _tokenBusiness.IsTokenRevoked(jti);
                Assert.True(isRevoked);
            }
        }

        [Fact]
        public async Task RevokeAllUserTokens_DoesNotAffectOtherUsers()
        {
            // Create token for first user
            var apiKey = "user1-key";
            var secret1 = "user1-secret";
            var hashedSecret1 = _tokenBusiness.HashApiSecret(secret1);
            Context.ApiKeys.Add(new ApiKey 
            { 
                Key = apiKey, 
                Secret = hashedSecret1, 
                UserId = uid1,
                ApplicationId = applicationId
            });
            await Context.SaveChangesAsync();
            var jwt1 = await _tokenBusiness.CreateToken(apiKey, secret1, expiration: 5);

            // Create token for other user
            var apiKey2 = "user2-key";
            var secret2 = "user2-secret";
            var hashedSecret2 = _tokenBusiness.HashApiSecret(secret2);
            Context.ApiKeys.Add(new ApiKey 
            { 
                Key = apiKey2, 
                Secret = hashedSecret2, 
                UserId = uid2,
                ApplicationId = applicationId
            });
            await Context.SaveChangesAsync();
            var jwt2 = await _tokenBusiness.CreateToken(apiKey2, secret2, expiration: 5);

            // Act
            await _tokenBusiness.RevokeAllUserTokens(uid1);

            // Assert
            var handler = new JwtSecurityTokenHandler();
            
            var parsed1 = handler.ReadJwtToken(jwt1);
            var jti1 = parsed1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
            Assert.True(await _tokenBusiness.IsTokenRevoked(jti1));

            var parsed2 = handler.ReadJwtToken(jwt2);
            var jti2 = parsed2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
            Assert.False(await _tokenBusiness.IsTokenRevoked(jti2));
        }

        #endregion

        #region GetApiKey Tests

        [Fact]
        public async Task GetApiKey_ReturnsKey_WhenExists()
        {
            var apiKey = "findMe";
            Context.ApiKeys.Add(new ApiKey 
            { 
                Key = apiKey, 
                Secret = "hashMe", 
                UserId = uid1,
                ApplicationId = applicationId
            });
            await Context.SaveChangesAsync();

            // Act
            var found = await _tokenBusiness.GetApiKey(apiKey);

            // Assert
            Assert.NotNull(found);
            Assert.Equal(apiKey, found!.Key);
            Assert.Equal(uid1, found.UserId);
        }
        
        public async Task GetApiKey_Fails_WhenNotExists()
        {
            var apiKey = "cantFindMe";

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _tokenBusiness.GetApiKey(apiKey));
            Assert.Contains("Api Keypair with key", ex.Message);
        }

        #endregion

        #region CreateApiKey Tests

        [Fact]
        public async Task CreateApiKey_PersistsRow_And_ReturnsDto_WithVerifiableHash()
        {
            // Act
            TokenResponseDto dto = await _tokenBusiness.CreateApiKey(uid1, clientId);

            // Assert - DTO populated
            Assert.False(string.IsNullOrWhiteSpace(dto.apiKey));
            Assert.False(string.IsNullOrWhiteSpace(dto.apiSecret));

            // Assert - Persisted row (secret is stored as HASH)
            var saved = Context.ApiKeys.SingleOrDefault(k => k.Key == dto.apiKey && k.UserId == uid1);
            Assert.NotNull(saved);
            Assert.False(string.IsNullOrWhiteSpace(saved!.Secret));
            Assert.Equal(applicationId, saved.ApplicationId);
            
            Assert.True(_tokenBusiness.VerifyApiSecret(dto.apiSecret, saved.Secret));
    
            // Verify they are NOT the same (one is plaintext, one is hash)
            Assert.NotEqual(dto.apiSecret, saved.Secret);
        }

        [Fact]
        public async Task CreateApiKey_Throws_WhenUserNotFound()
        {
            // Arrange
            UserContextStorage.Email = "nouser@example.com";

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _tokenBusiness.CreateApiKey(99999, clientId));
            Assert.Contains("User with id", ex.Message);
        }

        [Fact]
        public async Task CreateApiKey_Throws_WhenApplicationNotFound()
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _tokenBusiness.CreateApiKey(uid1, "nonexistent-client-id"));
            Assert.Contains("OAuth application", ex.Message);
        }

        [Fact]
        public async Task CreateApiKey_Throws_WhenApplicationArchived()
        {
            var app = Context.OauthApplications.Find(applicationId);
            app!.IsArchived = true;
            await Context.SaveChangesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _tokenBusiness.CreateApiKey(uid1, clientId));
            Assert.Contains("archived", ex.Message);
        }

        #endregion

        #region DeleteApiKey Tests

        [Fact]
        public async Task DeleteApiKey_RemovesRow_And_ReturnsTrue()
        {
            var key = "delete-me";
            Context.ApiKeys.Add(new ApiKey 
            { 
                Key = key, 
                Secret = "sec", 
                UserId = uid1,
                ApplicationId = applicationId
            });
            await Context.SaveChangesAsync();

            // Act
            var ok = await _tokenBusiness.DeleteApiKey(uid1, key);

            // Assert
            Assert.True(ok);
            Assert.False(await Context.ApiKeys.AnyAsync(k => k.Key == key && k.UserId == uid1));
        }

        [Fact]
        public async Task DeleteApiKey_Throws_WhenNotFound()
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _tokenBusiness.DeleteApiKey(uid1, "nope"));

            Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Hash/Verify Tests

        [Fact]
        public async Task HashApiSecret_And_VerifyApiSecret_RoundTrip()
        {
            // Arrange
            var apiKey = "roundtrip-key";

            // Act
            var hash = _tokenBusiness.HashApiSecret(apiKey);

            // Assert
            Assert.True(_tokenBusiness.VerifyApiSecret(apiKey, hash));
            Assert.False(_tokenBusiness.VerifyApiSecret("wrong", hash));
        }

        #endregion

        #region GetAllUserKeys Tests

        [Fact]
        public async Task GetAllUserKeys_ReturnsOnlyKeys_ForUser()
        {
            Context.ApiKeys.AddRange(
                new ApiKey 
                { 
                    Key = "K1", 
                    Secret = "s1", 
                    UserId = uid1,
                    ApplicationId = applicationId
                },
                new ApiKey 
                { 
                    Key = "K2", 
                    Secret = "s2", 
                    UserId = uid1,
                    ApplicationId = applicationId
                },
                new ApiKey 
                { 
                    Key = "Z9", 
                    Secret = "s9", 
                    UserId = uid2,
                    ApplicationId = applicationId
                }
            );
            await Context.SaveChangesAsync();

            // Act
            var keys = await ((interfaces.ITokenBusiness)_tokenBusiness).GetAllUserKeys(uid1);

            // Assert
            Assert.Equal(3, keys.Count);
            Assert.Contains("K1", keys);
            Assert.Contains("K2", keys);
            Assert.Contains("api-key-123", keys); // application key
            Assert.DoesNotContain("Z9", keys);
        }

        #endregion

        # region Helper Methods

        // Helper methods (copied directly from OauthApplicationBusiness)
        private string GenerateClientId()
        {
            var bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            var base64 = Convert.ToBase64String(bytes);
            return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private string GenerateClientSecret()
        {
            var bytes = new byte[64];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            var base64 = Convert.ToBase64String(bytes);
            return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private string HashSecret(string secret)
        {
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                var salt = new byte[32];
                rng.GetBytes(salt);

                using (var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
                   secret,
                   salt,
                   100000,
                   System.Security.Cryptography.HashAlgorithmName.SHA256))
                {
                    var hash = pbkdf2.GetBytes(32);
                    var saltBase64 = Convert.ToBase64String(salt);
                    var hashBase64 = Convert.ToBase64String(hash);
                    return $"{saltBase64}:{hashBase64}";
                }
            }
        }
        
        // Helpers copied from TokenBusiness
        private string HashToken(string jti)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(jti));
            return Convert.ToBase64String(hashBytes);
        }
        
        public string HashApiSecret(string apiKey)
        {
            return BCrypt.Net.BCrypt.HashPassword(apiKey, workFactor: 12);
        }
        
        # endregion
        
        protected override async Task SeedTestDataAsync()
        {
            await base.SeedTestDataAsync();
            
            // Create test users
            var user = new User 
            { 
                Email = "tester@example.com", 
                Name = "Test User" 
            };
            Context.Users.Add(user);
            
            var otherUser = new User 
            { 
                Email = "other@example.com", 
                Name = "Other User" 
            };
            Context.Users.Add(otherUser);
            await Context.SaveChangesAsync();
            
            uid1 = user.Id;
            uid2 = otherUser.Id;
            userEmail = user.Email;
            UserContextStorage.Email = userEmail;

            // Create OAuth application DIRECTLY (copied logic from OauthApplicationBusiness)
            clientId = GenerateClientId();
            var clientSecret = GenerateClientSecret();
            var clientSecretHash = HashSecret(clientSecret);
            
            var oauthApp = new OauthApplication
            {
                Name = "Test OAuth Application",
                Description = "Test application for token tests",
                ClientId = clientId,
                ClientSecretHash = clientSecretHash,
                CallbackUrl = "https://example.com/callback",
                BaseUrl = "https://example.com",
                AppOwnerEmail = userEmail,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid1,
                IsArchived = false
            };
            
            Context.OauthApplications.Add(oauthApp);
            await Context.SaveChangesAsync();
            applicationId = oauthApp.Id;
            
            // Create API Key and Secret
            apiKey1 = "api-key-123";
            plaintextSecret1 = "my-plaintext-secret";

            // Store the HASHED secret in the database
            hashedSecret1 = HashApiSecret(plaintextSecret1);
            Context.ApiKeys.Add(new ApiKey 
            { 
                Key = apiKey1, 
                Secret = hashedSecret1, 
                UserId = uid1,
                ApplicationId = applicationId
            });
            await Context.SaveChangesAsync();
            
            // Set the JWT signing secret environment variable
            Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "test-jwt-secret-key-min-32-chars");
        }
    }
}