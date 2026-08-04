using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for creating tokens and managing API keys.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create JWT tokens, manage API keys, and handle token revocation.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Authorize]
[Route("oauth")]
public class TokenController : ControllerBase
{
    private readonly IEventBusiness _eventBusiness;
    private readonly ILogger<TokenController> _logger;
    private readonly ITokenBusiness _tokenBusiness;

    public TokenController(IEventBusiness eventBusiness, ITokenBusiness tokenBusiness, ILogger<TokenController> logger)
    {
        _eventBusiness = eventBusiness;
        _tokenBusiness = tokenBusiness;
        _logger = logger;
    }



    /// <summary>
    ///     Create JWT Token
    /// </summary>
    /// <param name="tokenDto">Token creation request with API key, secret, and optional expiration</param>
    /// <returns>The generated JWT token string.</returns>
    [AllowAnonymous]
    [HttpPost("tokens", Name = "api_create_token")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<IActionResult> CreateToken([FromBody] CreateTokenDto tokenDto)
    {
        var token = await _tokenBusiness.CreateToken(
            tokenDto.ApiKey,
            tokenDto.ApiSecret,
            tokenDto.ExpirationMinutes);
        return Ok(token);
    }



    /// <summary>
    ///     Create API Key and Secret
    /// </summary>
    /// <param name="clientId">Optional OAuth client ID to associate with the API key</param>
    /// <returns>The generated API key and secret; the secret is returned only once.</returns>
    [HttpPost("keys", Name = "api_create_api_key")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [AllowAnonymous]
    [ForbidServiceAccounts]
    public async Task<IActionResult> CreateApiKey([FromQuery] string? clientId = null)
    {
        var currentUserId = UserContextStorage.UserId;
        var tokenDto = await _tokenBusiness.CreateApiKey(currentUserId, clientId);
        return Ok(tokenDto);
    }



    /// <summary>
    ///     Generate API Key for a Service Account
    /// </summary>
    /// <param name="organizationId">ID of the organization the service account belongs to</param>
    /// <param name="projectId">ID of the project the service account belongs to</param>
    /// <param name="serviceAccountId">ID of the service account to generate a key for</param>
    /// <returns>The generated API key and secret; the secret is returned only once.</returns>
    [Tags("Service Accounts")]
    [HttpPost("organizations/{organizationId}/projects/{projectId}/keys/service/{serviceAccountId}",
        Name = "api_create_service_account_api_key")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ProjectAdmin]
    public async Task<IActionResult> GenerateServiceAccountApiKey(
        long organizationId,
        long projectId,
        long serviceAccountId)
    {
        var currentUserId = UserContextStorage.UserId;
        var tokenDto = await _tokenBusiness.GenerateServiceAccountApiKey(
            currentUserId,
            organizationId,
            projectId,
            serviceAccountId);
        return Ok(tokenDto);
    }



    /// <summary>
    ///     Generate API Key for a Test Account
    /// </summary>
    /// <param name="testAccountId">ID of the test account to generate a key for</param>
    /// <returns>The generated API key and secret; the secret is returned only once.</returns>
    [Tags("Test Accounts")]
    [HttpPost("keys/test/{testAccountId}", Name = "api_create_test_account_api_key")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<IActionResult> GenerateTestAccountApiKey(long testAccountId)
    {
        var currentUserId = UserContextStorage.UserId;
        var tokenDto = await _tokenBusiness.GenerateTestAccountApiKey(currentUserId, testAccountId);
        return Ok(tokenDto);
    }



    /// <summary>
    ///     Delete API Key
    /// </summary>
    /// <param name="key">API key to be deleted</param>
    /// <returns>A boolean indicating whether the API key was deleted.</returns>
    [HttpDelete("keys/{key}", Name = "api_delete_api_key")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<IActionResult> DeleteApiKey(string key)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _tokenBusiness.DeleteApiKey(currentUserId, key);
        return Ok(response);
    }



    /// <summary>
    ///     Get All API Keys Associated with the Current User
    /// </summary>
    /// <returns>A list of API keys; secrets are never returned.</returns>
    [HttpGet("keys", Name = "api_get_all_user_keys")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<List<string>>> GetAllUserKeys()
    {
        var currentUserId = UserContextStorage.UserId;
        var keys = await _tokenBusiness.GetAllUserKeys(currentUserId);
        return Ok(keys);
    }



    /// <summary>
    ///     Revoke All Active Tokens for the Current User
    /// </summary>
    /// <returns>The number of tokens revoked.</returns>
    [HttpDelete("tokens/revoke", Name = "api_revoke_all_user_tokens")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<IActionResult> RevokeAllUserTokens()
    {
        var currentUserId = UserContextStorage.UserId;
        var revokedCount = await _tokenBusiness.RevokeAllUserTokens(currentUserId);

        return Ok(revokedCount);
    }
}
