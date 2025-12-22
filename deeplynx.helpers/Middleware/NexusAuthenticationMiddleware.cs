using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using deeplynx.datalayer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace deeplynx.helpers;

public class NexusAuthenticationMiddleware : JwtBearerHandler
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private static IConfigurationManager<OpenIdConnectConfiguration>? _configManager;
    private static readonly object _configManagerLock = new();

    public NexusAuthenticationMiddleware(
        IOptionsMonitor<JwtBearerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IServiceScopeFactory serviceScopeFactory)
        : base(options, logger, encoder)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Extract token
        var token = ExtractToken(Request);
        var disableAuth = Environment.GetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION");

        // If local bypass is enabled, try to use the token but fall back to superuser on any issues
        if (disableAuth == "true")
        {
            // No token provided - use superuser
            if (string.IsNullOrEmpty(token))
            {
                Log.Information("Local bypass enabled with no token - using local development superuser");
                return await HandleLocalDevelopmentBypass();
            }

            // Token provided - try to validate it
            try
            {
                var result = await ValidateTokenAsync(token);

                // If token validation succeeded, use the authenticated user
                if (result.Succeeded)
                {
                    Log.Information("Valid token detected - using authenticated user from token");
                    return result;
                }

                // Token validation failed - fall back to superuser
                Log.Warning("Local bypass enabled but token validation failed - using local development superuser");
                return await HandleLocalDevelopmentBypass();
            }
            catch (Exception ex)
            {
                Log.Warning(ex,
                    "Local bypass enabled but token validation threw exception - using local development superuser");
                return await HandleLocalDevelopmentBypass();
            }
        }

        // Normal flow - auth bypass NOT enabled
        if (string.IsNullOrEmpty(token))
        {
            Log.Warning("No token found in request");
            return AuthenticateResult.NoResult();
        }

        return await ValidateTokenAsync(token);
    }

    private async Task<AuthenticateResult> ValidateTokenAsync(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            Log.Error("Token cannot be read by handler");
            return AuthenticateResult.Fail("Invalid token format");
        }

        var jwtToken = handler.ReadJwtToken(token);
        var algorithm = jwtToken.Header.Alg;

        // Handle HS256
        if (algorithm.StartsWith("HS", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleHS256Token(token, jwtToken);
        }

        // Handle RS256 (Okta)
        if (algorithm.StartsWith("RS", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleRS256Token(token);
        }

        return AuthenticateResult.Fail($"Unsupported token algorithm: {algorithm}");
    }

    private async Task<AuthenticateResult> HandleLocalDevelopmentBypass()
    {
        const string localDevEmail = "developer@localhost";
        const string localDevUserId = "local-dev-user";

        var mockClaims = new[]
        {
            new Claim(ClaimTypes.Name, "LocalDeveloper"),
            new Claim(ClaimTypes.NameIdentifier, localDevUserId),
            new Claim("uid", localDevUserId),
            new Claim("email", localDevEmail),
            new Claim(ClaimTypes.Email, localDevEmail),
            new Claim(ClaimTypes.Role, "Developer")
        };

        var mockIdentity = new ClaimsIdentity(mockClaims, "LocalDevelopment");
        var mockPrincipal = new ClaimsPrincipal(mockIdentity);

        await EnsureLocalDevUserExistsAsync(localDevEmail, localDevUserId);

        var ticket = new AuthenticationTicket(mockPrincipal, Scheme.Name);
        Log.Information("Local development authentication successful");
        return AuthenticateResult.Success(ticket);
    }

    private async Task<AuthenticateResult> HandleHS256Token(string token, JwtSecurityToken jwtToken)
    {
        try
        {
            // Extract username with fallback priority: name -> preferred_username -> sub -> email
            var username = jwtToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value
                           ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value
                           ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                           ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

            var apiKey = jwtToken.Claims.FirstOrDefault(c => c.Type == "apiKey")?.Value;
            var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(jti))
            {
                Log.Warning("Token missing required claims");
                return AuthenticateResult.Fail("Token missing required claims");
            }

            // Check if token is revoked
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DeeplynxContext>();
    
                // Hash the JTI for lookup
                var tokenHash = HashToken(jti);
                var tokenRecord = await dbContext.OauthTokens
                    .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
    
                if (tokenRecord == null || tokenRecord.Revoked)
                {
                        Log.Warning($"Token not found or revoked - JTI: {jti}");
                        return AuthenticateResult.Fail("Token has been revoked");
                }
            }

            // Verify the API key still exists and is valid
            var apiKeySecret = await GetSecretForUserAsync(username, apiKey);
            if (string.IsNullOrWhiteSpace(apiKeySecret))
            {
                Log.Warning($"API key not found or revoked - Username: {username}");
                return AuthenticateResult.Fail("Invalid API key");
            }

            // Use JWT_SECRET_KEY for signature validation
            var jwtSigningSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

            if (string.IsNullOrEmpty(jwtSigningSecret))
            {
                Log.Error("JWT_SECRET_KEY not configured");
                return AuthenticateResult.Fail("Server configuration error");
            }

            var secretKey = jwtSigningSecret.Length < 32
                ? jwtSigningSecret.PadRight(32, '0')
                : jwtSigningSecret;

            if (!ValidateJwtSignature(token, secretKey))
            {
                Log.Warning("JWT signature validation failed");
                return AuthenticateResult.Fail("JWT signature validation failed");
            }

            // Validate expiration
            var exp = jwtToken.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
            if (!string.IsNullOrEmpty(exp))
            {
                var expirationTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp));
                if (expirationTime < DateTimeOffset.UtcNow)
                {
                    Log.Warning("Token has expired");
                    return AuthenticateResult.Fail("Token has expired");
                }
            }

            var claimsIdentity = new ClaimsIdentity(jwtToken.Claims, Scheme.Name);
            var principal = new ClaimsPrincipal(claimsIdentity);

            await EnsureUserExistsAsync(principal);

            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            Log.Information("HS256 token validated successfully");
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"HS256 validation failed: {ex.Message}");
            return AuthenticateResult.Fail(ex);
        }
    }

    private async Task<AuthenticateResult> HandleRS256Token(string token)
    {
        try
        {
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");

            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            {
                Log.Error("JWT_ISSUER or JWT_AUDIENCE not configured");
                return AuthenticateResult.Fail("JWT configuration error");
            }

            var metadataAddress = $"{issuer.TrimEnd('/')}/.well-known/openid-configuration";
            if (_configManager == null)
            {
                lock (_configManagerLock)
                {
                    _configManager ??= new ConfigurationManager<OpenIdConnectConfiguration>(
                        metadataAddress,
                        new OpenIdConnectConfigurationRetriever());
                }
            }

            var oidcConfig = await _configManager.GetConfigurationAsync(CancellationToken.None);

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = issuer,
                ValidateIssuer = true,
                ValidAudience = audience,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = oidcConfig.SigningKeys,
                RequireSignedTokens = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            await EnsureUserExistsAsync(principal);

            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            Log.Information("RS256 token validated successfully");
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"RS256 validation failed: {ex.Message}");
            return AuthenticateResult.Fail(ex);
        }
    }

    private string? ExtractToken(HttpRequest request)
    {
        var authHeader = request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..];
        }

        var tokenQuery = request.Query["token"].FirstOrDefault();
        if (!string.IsNullOrEmpty(tokenQuery))
        {
            return tokenQuery;
        }

        var tokenCookie = request.Cookies["access_token"];
        return string.IsNullOrEmpty(tokenCookie) ? null : tokenCookie;
    }

    private bool ValidateJwtSignature(string token, string secret)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return false;

            var message = $"{parts[0]}.{parts[1]}";
            var keyBytes = Encoding.UTF8.GetBytes(secret);

            using var hmac = new HMACSHA256(keyBytes);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var computedSignature = Base64UrlEncode(computedHash);

            return computedSignature == parts[2];
        }
        catch
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] input)
    {
        var output = Convert.ToBase64String(input);
        output = output.Split('=')[0];
        output = output.Replace('+', '-');
        output = output.Replace('/', '_');
        return output;
    }

    private async Task<string?> GetSecretForUserAsync(string username, string apiKeyValue)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(apiKeyValue))
            return null;

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DeeplynxContext>();

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == username.ToLower());
        if (user == null)
        {
            Log.Warning($"User not found: {username}");
            return null;
        }

        var apiKey = await dbContext.ApiKeys.FirstOrDefaultAsync(a => a.Key == apiKeyValue && a.UserId == user.Id);
        return apiKey?.Secret;
    }

    private async Task EnsureUserExistsAsync(ClaimsPrincipal principal)
    {
        try
        {
            var email = principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                            ?.Value
                        ?? principal.FindFirst(ClaimTypes.Email)?.Value
                        ?? principal.FindFirst("email")?.Value
                        ?? principal.FindFirst("sub")?.Value
                        ?? principal.FindFirst("name")?.Value;

            if (string.IsNullOrEmpty(email))
            {
                Log.Warning("Could not extract email from claims for user provisioning");
                return;
            }

            var ssoId = principal.FindFirst("uid")?.Value;
            var username = principal.FindFirst("preferred_username")?.Value ?? email;
            var name = principal.FindFirst(ClaimTypes.Name)?.Value
                       ?? principal.FindFirst("name")?.Value
                       ?? username;

            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DeeplynxContext>();

            var isDefaultSuperUser =
                email.ToLower() == Environment.GetEnvironmentVariable("SUPERUSER_EMAIL")?.ToLower();
            var existingUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

            if (existingUser != null)
            {
                // update if admin needs to be set or if SSO ID is improperly configured
                if ((isDefaultSuperUser && !existingUser.IsSysAdmin)
                    || existingUser.SsoId != principal.FindFirst("uid")?.Value)
                {
                    existingUser.SsoId = ssoId;
                    existingUser.Username = username;
                    existingUser.Name = name;
                    existingUser.IsActive = true;
                    existingUser.IsArchived = false;
                    existingUser.IsSysAdmin = isDefaultSuperUser || existingUser.IsSysAdmin;
                    await dbContext.SaveChangesAsync();
                    Log.Information($"Updated SSO ID for existing user {email}");
                }
                
                // Add user to the default org if not already a member
                var defaultOrg = await dbContext.Organizations
                    .Where(o => o.DefaultOrg).FirstOrDefaultAsync();

                if (defaultOrg != null)
                {
                    var isMember = await dbContext.OrganizationUsers
                        .AnyAsync(ou => ou.OrganizationId == defaultOrg.Id && ou.UserId == existingUser.Id);
    
                    if (!isMember)
                    {
                        var orgUser = new OrganizationUser
                        {
                            OrganizationId = defaultOrg.Id,
                            UserId = existingUser.Id,
                        };
        
                        dbContext.OrganizationUsers.Add(orgUser);
                        await dbContext.SaveChangesAsync();
                    }
                }
            }
            else
            {
                var newUser = new User
                {
                    Name = name,
                    Email = email,
                    Username = username,
                    SsoId = ssoId,
                    IsActive = true,
                    IsArchived = false,
                    IsSysAdmin = isDefaultSuperUser,
                };
                dbContext.Users.Add(newUser);
                await dbContext.SaveChangesAsync();
                Log.Information($"User with email {email} created successfully");
                
                // Add user to the default org
                var defaultOrg = await dbContext.Organizations
                    .Where(o => o.DefaultOrg).FirstOrDefaultAsync();

                if (defaultOrg != null)
                {
                    var orgUser = new OrganizationUser
                    {
                        OrganizationId = defaultOrg.Id,
                        UserId = newUser.Id,
                    };
                    
                    dbContext.OrganizationUsers.Add(orgUser);
                    await dbContext.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during user provisioning");
        }
    }

    private async Task EnsureLocalDevUserExistsAsync(string email, string ssoId)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DeeplynxContext>();

            var existingUser = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

            if (existingUser == null)
            {
                var newUser = new User
                {
                    Name = "Local Developer",
                    Email = email,
                    Username = "local-dev",
                    SsoId = ssoId,
                    IsActive = true,
                    IsArchived = false,
                    IsSysAdmin = true
                };

                dbContext.Users.Add(newUser);
                await dbContext.SaveChangesAsync();
                Log.Information($"Local development sys admin created: {email}");
                existingUser = newUser;
            }
            else if (!existingUser.IsSysAdmin)
            {
                // if user exists but is not sysadmin, make them admin
                existingUser.IsSysAdmin = true;
                existingUser.IsActive = true;
                existingUser.IsArchived = false;
                await dbContext.SaveChangesAsync();
                Log.Information($"Existing user {email} promoted to sys admin for local development");
            }
            
            // Add user to the default org if not already a member
            var defaultOrg = await dbContext.Organizations
                .Where(o => o.DefaultOrg).FirstOrDefaultAsync();

            if (defaultOrg != null)
            {
                var isMember = await dbContext.OrganizationUsers
                    .AnyAsync(ou => ou.OrganizationId == defaultOrg.Id && ou.UserId == existingUser.Id);
    
                if (!isMember)
                {
                    var orgUser = new OrganizationUser
                    {
                        OrganizationId = defaultOrg.Id,
                        UserId = existingUser.Id,
                    };
        
                    dbContext.OrganizationUsers.Add(orgUser);
                    await dbContext.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during local dev user provisioning");
        }
    }
    
    private static string HashToken(string jti)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(jti));
        return Convert.ToBase64String(hashBytes);
    }
}