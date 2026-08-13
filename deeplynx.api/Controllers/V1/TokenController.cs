using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V1;

/// <summary>
///     Controller for creating tokens and managing API keys.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create JWT tokens, manage API keys, and handle token revocation.
/// </remarks>
[ApiController]
[ApiVersion(1, Deprecated = true)]
[Authorize]
[Route("oauth")]
public class TokenController : ControllerBase
{
    private readonly IEventBusiness _eventBusiness;
    private readonly ILogger<TokenController> _logger;
    private readonly IOauthDeviceAuthorizationBusiness _oauthDeviceAuthorizationBusiness;
    private readonly ITokenBusiness _tokenBusiness;

    public TokenController(
        IEventBusiness eventBusiness,
        ITokenBusiness tokenBusiness,
        IOauthDeviceAuthorizationBusiness oauthDeviceAuthorizationBusiness,
        ILogger<TokenController> logger)
    {
        _eventBusiness = eventBusiness;
        _tokenBusiness = tokenBusiness;
        _oauthDeviceAuthorizationBusiness = oauthDeviceAuthorizationBusiness;
        _logger = logger;
    }


    /// <summary>
    ///     Create JWT Token
    /// </summary>
    /// <param name="tokenDto">Token creation request with API key, secret, and optional expiration</param>
    /// <returns>JWT token string</returns>
    [AllowAnonymous]
    [HttpPost("tokens", Name = "api_create_token")]
    public async Task<IActionResult> CreateToken([FromBody] CreateTokenDto tokenDto)
    {
        try
        {
            var token = await _tokenBusiness.CreateToken(tokenDto.ApiKey, tokenDto.ApiSecret,
                tokenDto.ExpirationMinutes);
            return Ok(token);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating token");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while creating the token" });
        }
    }

    /// <summary>
    ///     OAuth 2.0 Token Endpoint
    /// </summary>
    /// <param name="grantType">The OAuth grant type</param>
    /// <param name="deviceCode">The device code returned by the device authorization endpoint</param>
    /// <param name="refreshToken">The refresh token returned by a previous OAuth token response</param>
    /// <param name="clientId">The OAuth application's client ID</param>
    /// <returns>OAuth token response or polling error</returns>
    [AllowAnonymous]
    [HttpPost("token", Name = "api_oauth_token")]
    public async Task<IActionResult> ExchangeOauthToken(
        [FromForm(Name = "grant_type")] string? grantType,
        [FromForm(Name = "device_code")] string? deviceCode,
        [FromForm(Name = "refresh_token")] string? refreshToken,
        [FromForm(Name = "client_id")] string? clientId)
    {
        const string deviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";
        const string refreshTokenGrantType = "refresh_token";

        try
        {
            if (string.IsNullOrWhiteSpace(grantType))
            {
                throw new ArgumentException("grant_type is required");
            }

            OauthTokenGrantResponseDto response;

            if (grantType == deviceCodeGrantType)
            {
                response = await _oauthDeviceAuthorizationBusiness.ExchangeDeviceCodeForToken(deviceCode, clientId);
            }
            else if (grantType == refreshTokenGrantType)
            {
                response = await _oauthDeviceAuthorizationBusiness.ExchangeRefreshTokenForToken(refreshToken, clientId);
            }
            else
            {
                return BadRequest(new OauthErrorResponseDto
                {
                    Error = "unsupported_grant_type",
                    ErrorDescription = "Unsupported grant_type"
                });
            }

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid OAuth token request");
            return BadRequest(new OauthErrorResponseDto
            {
                Error = "invalid_request",
                ErrorDescription = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "OAuth application not found for token request");
            return NotFound(new OauthErrorResponseDto
            {
                Error = "invalid_client",
                ErrorDescription = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "OAuth device token request is not ready");
            return BadRequest(new OauthErrorResponseDto
            {
                Error = ex.Message
            });
        }
        catch (Exception ex)
        {
            const string message = "An unexpected error occurred in the OAuth token flow";
            _logger.LogError(ex, message);

            return StatusCode(StatusCodes.Status500InternalServerError, new OauthErrorResponseDto
            {
                Error = "server_error",
                ErrorDescription = message
            });
        }
    }


    /// <summary>
    ///     Create API Key and Secret
    /// </summary>
    /// <param name="clientId">Optional OAuth client ID to associate with the API key</param>
    /// <returns>API key and secret (secret only returned once)</returns>
    [HttpPost("keys", Name = "api_create_api_key")]
    [AllowAnonymous]
    [ForbidServiceAccounts] // service accounts can only act on the project level
    public async Task<IActionResult> CreateApiKey([FromQuery] string? clientId = null)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var tokenDto = await _tokenBusiness.CreateApiKey(currentUserId, clientId);
            return Ok(tokenDto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API key");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while creating the API key" });
        }
    }



    /// <summary>
    ///     Generate API Key for a service account
    /// </summary>
    /// <param name="organizationId">ID of the organization the service account belongs to</param>
    /// <param name="projectId">ID of the project the service account belongs to</param>
    /// <param name="serviceAccountId">ID of the service account to generate a key for</param>
    /// <returns>API key and secret (secret only returned once)</returns>
    [Tags("Service Accounts")]
    [HttpPost("organizations/{organizationId}/projects/{projectId}/keys/service/{serviceAccountId}",
        Name = "api_create_service_account_api_key")]
    [ProjectAdmin]
    public async Task<IActionResult> GenerateServiceAccountApiKey(
        long organizationId,
        long projectId,
        long serviceAccountId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var tokenDto = await _tokenBusiness.GenerateServiceAccountApiKey(currentUserId, organizationId, projectId, serviceAccountId);
            return Ok(tokenDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service account API key for account {ServiceAccountId}", serviceAccountId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while creating the API key" });
        }
    }



    /// <summary>
    ///     Generate API Key for a test account
    /// </summary>
    /// <param name="testAccountId">ID of the test account to generate a key for</param>
    /// <returns>API key and secret (secret only returned once)</returns>
    [Tags("Test Accounts")]
    [HttpPost("keys/test/{testAccountId}", Name = "api_create_test_account_api_key")]
    [SysAdmin]
    public async Task<IActionResult> GenerateTestAccountApiKey(
        long testAccountId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var tokenDto = await _tokenBusiness.GenerateTestAccountApiKey(
                currentUserId, testAccountId);
            return Ok(tokenDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test account API key for account {TestAccountId}", testAccountId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while creating the API key" });
        }
    }



    /// <summary>
    ///     Delete API Key
    /// </summary>
    /// <param name="key">API key to be deleted</param>
    /// <returns>Success message</returns>
    [HttpDelete("keys/{key}", Name = "api_delete_api_key")]
    public async Task<IActionResult> DeleteApiKey(string key)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _tokenBusiness.DeleteApiKey(currentUserId, key);
            return Ok(new { message = "Successfully deleted API key" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting API key {key}");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while deleting the API key" });
        }
    }



    /// <summary>
    ///     Get all API Keys Associated with the Current User
    /// </summary>
    /// <returns>List of API keys (secrets are never returned)</returns>
    [HttpGet("keys", Name = "api_get_all_user_keys")]
    public async Task<ActionResult<List<string>>> GetAllUserKeys()
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var keys = await _tokenBusiness.GetAllUserKeys(currentUserId);
            return Ok(keys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user API keys");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving API keys" });
        }
    }



    /// <summary>
    ///     Revoke All Active Tokens for the Current User
    /// </summary>
    /// <returns>Number of tokens revoked</returns>
    [HttpDelete("tokens/revoke", Name = "api_revoke_all_user_tokens")]
    public async Task<IActionResult> RevokeAllUserTokens()
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var revokedCount = await _tokenBusiness.RevokeAllUserTokens(currentUserId);

            return Ok(new
            {
                message = $"Successfully revoked {revokedCount} token(s)",
                revokedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all user tokens");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while revoking tokens" });
        }
    }


}
