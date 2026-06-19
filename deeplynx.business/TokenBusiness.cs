using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace deeplynx.business;

public class TokenBusiness : ITokenBusiness
{
    private readonly DeeplynxContext _context;

    public TokenBusiness(DeeplynxContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Create a JWT token given an API key and secret. Store in the DB.
    /// </summary>
    /// <param name="apiSecret">User-supplied secret</param>
    /// <param name="apiKey">User-supplied key</param>
    /// <param name="expiration">Expiry time in minutes</param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">Returned if key not found</exception>
    /// <exception cref="InvalidOperationException">Returned if associated user not found</exception>
    /// <exception cref="UnauthorizedAccessException">Returned if secret does not match stored hash</exception>
    public async Task<string> CreateToken(string apiKey, string apiSecret, double? expiration)
    {
        // 1. Look up the API key record
        var apiKeyRecord = await _context.ApiKeys.FirstOrDefaultAsync(x => x.Key == apiKey);
        if (apiKeyRecord == null)
            throw new KeyNotFoundException("API key not found");

        // 2. Get the User Email for the Token
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == apiKeyRecord.UserId);
        if (user == null)
            throw new InvalidOperationException("Associated user not found");

        // 3. Verify the provided plaintext secret against the stored hash
        if (!VerifyApiSecret(apiSecret, apiKeyRecord.Secret))
            throw new UnauthorizedAccessException("Invalid API credentials");

        // 4. Verify the application exists if ApplicationId is present
        if (apiKeyRecord.ApplicationId.HasValue)
        {
            var oauthApp = await _context.OauthApplications
                .FirstOrDefaultAsync(app => app.Id == apiKeyRecord.ApplicationId.Value && !app.IsArchived);

            if (oauthApp == null)
            {
                throw new InvalidOperationException(
                    $"OAuth application with ID '{apiKeyRecord.ApplicationId.Value}' not found or has been archived. Cannot create token.");
            }
        }

        // 5. Use the JWT signing secret
        var jwtSigningSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

        if (string.IsNullOrEmpty(jwtSigningSecret))
            throw new InvalidOperationException("JWT signing secret not configured");

        var secretCheck = jwtSigningSecret.Length < 32
            ? jwtSigningSecret.PadRight(32, '0')
            : jwtSigningSecret;

        var key = Encoding.UTF8.GetBytes(secretCheck);
        double expirationMinutes = expiration > 0 ? (double)expiration : 60;
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // Generate a unique token identifier (jti) to store in DB
        var jti = Guid.NewGuid().ToString();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Email.ToLower()),
            new Claim(JwtRegisteredClaimNames.Name, user.Email.ToLower()),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new Claim("apiKey", apiKey)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        // Store the token hash in the database for revocation tracking
        var tokenHash = HashToken(jti);
        _context.OauthTokens.Add(new OauthToken
        {
            TokenHash = tokenHash,
            UserId = user.Id,
            ApplicationId = apiKeyRecord.ApplicationId,
            ExpiresAt = DateTime.SpecifyKind(expiresAt, DateTimeKind.Unspecified),
            Revoked = false,
            CreatedAt = now,
        });

        user.LastLogin = now;
        await _context.SaveChangesAsync();

        return tokenString;
    }

    /// <summary>
    /// Revoke a given token by identifier
    /// </summary>
    /// <param name="jti">identifier for the given JWT</param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">Returned if token not found</exception>
    public async Task<bool> RevokeToken(string jti)
    {
        var tokenHash = HashToken(jti);
        var token = await _context.OauthTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (token == null)
            throw new KeyNotFoundException("Token not found");

        if (token.Revoked)
            return false; // Already revoked

        token.Revoked = true;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Check if the given token has been revoked
    /// </summary>
    /// <param name="jti">identifier for the given JWT</param>
    /// <returns></returns>
    public async Task<bool> IsTokenRevoked(string jti)
    {
        var tokenHash = HashToken(jti);
        var token = await _context.OauthTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (token == null)
        {
            return false;
        }

        return token.Revoked;
    }

    /// <summary>
    /// Revoke all tokens for a given user
    /// </summary>
    /// <param name="currentUserId">ID of the user for which to revoke tokens</param>
    /// <returns></returns>
    public async Task<int> RevokeAllUserTokens(long currentUserId)
    {
        var tokens = await _context.OauthTokens
            .Where(t => t.UserId == currentUserId && !t.Revoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.Revoked = true;
        }

        await _context.SaveChangesAsync();
        return tokens.Count;
    }

    /// <summary>
    /// Get the full API key database object given a key
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<ApiKey> GetApiKey(string key)
    {
        var apiKey = await _context.ApiKeys.FirstOrDefaultAsync(k => k.Key == key);
        if (apiKey == null)
        {
            throw new KeyNotFoundException($"Api Keypair with key {apiKey} not found");
        }

        return apiKey;
    }

    /// <summary>
    /// Create a new api keypair for a given user
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user</param>
    /// <param name="clientId">(optional) the client ID of the oauth application requesting</param>
    /// <param name="createdByUserId">(optional) the client ID of the oauth application requesting</param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">Returned if user or application not found</exception>
    public async Task<TokenResponseDto> CreateApiKey(long currentUserId, string? clientId = null, long? createdByUserId = null)
    {
        // Generate random key and secret
        string apiKey = KeyGenerator.GenerateKeyBase64();
        string apiSecret = KeyGenerator.GenerateKeyBase64();

        // Hash the SECRET
        string hashedSecret = HashApiSecret(apiSecret);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (user == null)
        {
            throw new KeyNotFoundException($"User with id {currentUserId} not found");
        }

        if (user.IsServiceAccount)
            throw new InvalidOperationException("Service accounts cannot perform this action");

        // Look up application by ClientId if provided
        long? applicationId = null;
        if (!string.IsNullOrEmpty(clientId))
        {
            var oauthApp = await _context.OauthApplications
                .FirstOrDefaultAsync(app => app.ClientId == clientId && !app.IsArchived);

            if (oauthApp == null)
            {
                throw new KeyNotFoundException(
                    $"OAuth application with ClientId '{clientId}' not found or has been archived.");
            }

            applicationId = oauthApp.Id;
        }

        // Store the plaintext key + hashed secret
        _context.ApiKeys.Add(new ApiKey
        {
            Key = apiKey,
            UserId = user.Id,
            Secret = hashedSecret,
            ApplicationId = applicationId,
            CreatedBy = createdByUserId
        });
        await _context.SaveChangesAsync();

        // Return: plaintext key + plaintext secret to user
        return new TokenResponseDto
        {
            apiKey = apiKey,
            apiSecret = apiSecret
        };
    }

    /// <summary>
    /// Admin only: Create a new api keypair for a given user
    /// </summary>
    /// <param name="currentUserId">The ID of the requesting user</param>
    /// <param name="serviceAccountId">The ID of the service account</param>
    public async Task<TokenResponseDto> GenerateServiceAccountApiKey(long currentUserId, long serviceAccountId)
    {
        // admin only 
        TokenResponseDto key = await CreateApiKey(serviceAccountId, createdByUserId: currentUserId);

        return key;
    }

    /// <summary>
    /// Delete the given API key
    /// </summary>
    /// <param name="currentUserId">Authorizing user</param>
    /// <param name="key">api key</param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">Returned if key not found</exception>
    public async Task<bool> DeleteApiKey(long currentUserId, string key)
    {
        var keyToRemove = await _context.ApiKeys.SingleOrDefaultAsync(k => k.Key == key && k.UserId == currentUserId);

        if (keyToRemove == null)
            throw new KeyNotFoundException($"Key {key} not found.");

        _context.ApiKeys.Remove(keyToRemove);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Hash an api secret before storage
    /// </summary>
    /// <param name="rawSecret">The un-hashed API secret</param>
    /// <returns></returns>
    public string HashApiSecret(string rawSecret)
    {
        return BCrypt.Net.BCrypt.HashPassword(rawSecret, workFactor: 12);
    }

    /// <summary>
    /// Verify that a given key and hashed secret match
    /// </summary>
    /// <param name="providedKey">Api key</param>
    /// <param name="storedHash">Hashed Api secret</param>
    /// <returns></returns>
    public bool VerifyApiSecret(string providedKey, string storedHash)
    {
        return BCrypt.Net.BCrypt.Verify(providedKey, storedHash);
    }

    /// <summary>
    /// Hash the JTI (JWT identifier) for storage
    /// </summary>
    /// <param name="jti"></param>
    /// <returns></returns>
    private string HashToken(string jti)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(jti));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// List all API keys for a user
    /// </summary>
    /// <param name="currentUserId">The ID of the user for which to list API keys</param>
    /// <returns></returns>
    async Task<List<string>> ITokenBusiness.GetAllUserKeys(long currentUserId)
    {
        // Query for existing records (excluding archived)
        var userApiKeys = await _context.ApiKeys
            .Where(r => r.UserId == currentUserId)
            .ToListAsync();
        // Send just the key, not the secret
        var keys = userApiKeys.Select(c => c.Key).ToList();
        return keys;
    }
}
