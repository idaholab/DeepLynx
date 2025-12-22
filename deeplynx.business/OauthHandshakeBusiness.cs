using System.Security;
using System.Security.Cryptography;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace deeplynx.business;

public class OauthHandshakeBusiness : IOauthHandshakeBusiness
{
    private readonly DeeplynxContext _context;
    private readonly ILogger<OauthHandshakeBusiness> _logger;
    private readonly ITokenBusiness _tokenBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OauthHandshakeBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for oauth application CRUD operations</param>
    /// <param name="logger">Used for logging</param>
    /// <param name="tokenBusiness">Used for creating API keys and tokens with the authenticated user</param>
    public OauthHandshakeBusiness(
        DeeplynxContext context,
        ILogger<OauthHandshakeBusiness> logger,
        ITokenBusiness tokenBusiness
    )
    {
        _context = context;
        _logger = logger;
        _tokenBusiness = tokenBusiness;
    }

    /// <summary>
    ///     Generates an authorization code for the OAuth2 authorization code flow
    /// </summary>
    /// <param name="clientId">The client ID of the OAuth application</param>
    /// <param name="userId">The ID of the user granting authorization</param>
    /// <param name="redirectUri">The redirect URI to validate against</param>
    /// <param name="state">The state parameter for CSRF protection</param>
    /// <returns>A secure authorization code</returns>
    public async Task<string> GenerateAuthCode(
        string clientId,
        long userId,
        string redirectUri,
        string state)
    {
        // Ensure application exists under the supplied client ID
        var application = await ValidateOauthApplication(clientId);

        // Ensure the supplied redirect URL matches the one we have on record
        ValidateRedirectUri(redirectUri, application.CallbackUrl);

        // Create an auth code using a cryptographically secure random number generator
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);

        // Convert to URL-safe base64 string
        var authCode = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // Store data associated with the auth code in the cache
        var authCodeData = new OauthCodeData
        {
            Code = authCode,
            ApplicationId = application.Id,
            RedirectUri = redirectUri,
            UserId = userId,
            State = state,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            ExpiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(10), DateTimeKind.Unspecified),
            IsUsed = false
        };

        var cacheKey = $"auth_code:{authCode}";
        await CacheService.Instance.SetAsync(
            cacheKey,
            authCodeData,
            TimeSpan.FromMinutes(10));

        return authCode;
    }

    /// <summary>
    ///     Exchanges an authorization code for access tokens
    /// </summary>
    /// <param name="code">The authorization code to exchange</param>
    /// <param name="clientId">The client ID of the OAuth application</param>
    /// <param name="clientSecret">The client secret for authentication</param>
    /// <param name="redirectUri">The redirect URI to validate against</param>
    /// <param name="state">The state parameter for CSRF protection</param>
    /// <param name="expiration">Expiry time in minutes. Defaults to 8 hours.</param>
    /// <returns>Token response containing access and refresh tokens</returns>
    public async Task<string> ExchangeAuthCodeForToken(
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        string state,
        double? expiration)
    {
        // Validate and consume the auth code
        var authCodeData = await ValidateAndConsumeAuthCode(
            code, clientId, clientSecret, redirectUri, state);

        // Generate an api key/secret for the authenticated user
        var keyPair = await _tokenBusiness.CreateApiKey(
            authCodeData.UserId, clientId);

        // Exchange the generated key/secret for a token
        var token = await _tokenBusiness.CreateToken(
            keyPair.apiKey, keyPair.apiSecret, expiration ?? 480);

        return token;
    }

    /// <summary>
    ///     Validates an authorization code and marks it as consumed
    /// </summary>
    /// <param name="code">The authorization code to validate</param>
    /// <param name="clientId">The client ID of the OAuth application</param>
    /// <param name="clientSecret">The client secret for authentication</param>
    /// <param name="redirectUri">The redirect URI to validate against</param>
    /// <param name="state">The state parameter for CSRF protection</param>
    /// <returns>The auth code data if validation succeeds</returns>
    /// <exception cref="InvalidOperationException">Thrown when auth code is invalid, expired, or used</exception>
    /// <exception cref="KeyNotFoundException">Thrown when application is not found</exception>
    private async Task<OauthCodeData> ValidateAndConsumeAuthCode(
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        string state)
    {
        // Retrieve auth code data from cache
        var cacheKey = $"auth_code:{code}";
        var authCodeData = await CacheService.Instance.GetAsync<OauthCodeData>(cacheKey);

        // Check if auth code exists, has been used, or is expired
        var nowWithoutTz = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        if (authCodeData == null)
        {
            _logger.LogWarning("Token exchange attempt with non-existent authorization code");
            throw new InvalidOperationException("Invalid authorization code");
        }

        if (authCodeData.IsUsed)
        {
            _logger.LogWarning(
                $"Token exchange attempt with already-used authorization code - potential replay attack. Code: {code}, ApplicationId: {authCodeData.ApplicationId}");
            throw new InvalidOperationException("Authorization code has already been used");
        }

        if (authCodeData.ExpiresAt < nowWithoutTz)
        {
            _logger.LogWarning(
                $"Token exchange attempt with expired authorization code. Code expired at: {authCodeData.ExpiresAt}, Current time: {nowWithoutTz}");
            throw new InvalidOperationException("Authorization code has expired");
        }

        // Validate application and credentials
        var application = await ValidateOauthApplication(clientId, authCodeData.ApplicationId);
        ValidateRedirectUri(redirectUri, application.CallbackUrl);
        VerifyClientSecret(clientSecret, application.ClientSecretHash, clientId);

        // Verify state matches for CSRF protection
        ValidateState(state, authCodeData.State, clientId);

        // Verify user exists
        var user = await _context.Users.FindAsync(authCodeData.UserId);
        if (user == null)
        {
            var message = $"Token exchange attempt with missing userId: {authCodeData.UserId}";
            _logger.LogError(message);
            throw new InvalidOperationException(message);
        }

        // Mark code as consumed to prevent replay attacks
        authCodeData.IsUsed = true;
        authCodeData.ExpiresAt = nowWithoutTz;
        await CacheService.Instance.SetAsync(cacheKey, authCodeData, TimeSpan.FromMinutes(1));

        return authCodeData;
    }

    /// <summary>
    ///     Validates that an OAuth application exists and optionally matches an expected application ID
    /// </summary>
    /// <param name="clientId">The client ID to validate</param>
    /// <param name="expectedApplicationId">Optional application ID that must match</param>
    /// <returns>The validated OAuth application</returns>
    /// <exception cref="KeyNotFoundException">Thrown when application is not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when client ID does not match associated application</exception>
    private async Task<OauthApplication> ValidateOauthApplication(
        string clientId,
        long? expectedApplicationId = null)
    {
        var application = await _context.OauthApplications
            .Where(a => a.ClientId == clientId)
            .Where(a => !a.IsArchived)
            .FirstOrDefaultAsync();

        if (application == null)
        {
            var message = $"Authorization attempt with invalid client_id: {clientId}";
            _logger.LogWarning(message);
            throw new KeyNotFoundException(message);
        }

        if (expectedApplicationId.HasValue && expectedApplicationId.Value != application.Id)
        {
            var message =
                $"Token exchange attempt with mismatched client_id. Expected application ID: {expectedApplicationId.Value}, Got: {application.Id}";
            _logger.LogWarning(message);
            throw new InvalidOperationException(message);
        }

        return application;
    }

    /// <summary>
    ///     Validates that a redirect URI matches the registered callback URL
    /// </summary>
    /// <param name="redirectUri">The redirect URI to validate</param>
    /// <param name="callbackUrl">The registered callback URL</param>
    /// <exception cref="InvalidOperationException">Thrown when redirect URI doesn't match</exception>
    private void ValidateRedirectUri(string redirectUri, string callbackUrl)
    {
        var isValid = string.Equals(redirectUri, callbackUrl, StringComparison.InvariantCultureIgnoreCase);

        if (!isValid)
        {
            var message = $"Token exchange attempt with unknown redirect_uri: {redirectUri}";
            _logger.LogWarning(message);
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    ///     Verifies that a client secret matches the stored hash
    /// </summary>
    /// <param name="secret">The client secret to verify</param>
    /// <param name="storedHash">The stored BCrypt hash</param>
    /// <param name="clientId">The client ID for logging purposes</param>
    /// <exception cref="InvalidOperationException">Thrown when secret doesn't match</exception>
    private void VerifyClientSecret(string secret, string storedHash, string clientId)
    {
        var isValid = BCrypt.Net.BCrypt.Verify(secret, storedHash);

        if (!isValid)
        {
            var message = $"Token exchange attempt with invalid client_secret for client_id: {clientId}";
            _logger.LogWarning(message);
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    ///     Validates that the state parameter matches for CSRF protection
    /// </summary>
    /// <param name="providedState">The state parameter provided in the token request</param>
    /// <param name="storedState">The state parameter stored during authorization</param>
    /// <param name="clientId">The client ID for logging purposes</param>
    /// <exception cref="InvalidOperationException">Thrown when state doesn't match</exception>
    private void ValidateState(string providedState, string storedState, string clientId)
    {
        var isValid = string.Equals(providedState, storedState, StringComparison.Ordinal);

        if (!isValid)
        {
            // CSRF protection failure is a security event
            _logger.LogError(
                $"CSRF protection failure - state mismatch for client_id: {clientId}. Expected: {storedState}, Got: {providedState}");
            throw new SecurityException("State parameter mismatch - possible CSRF attack");
        }
    }
}