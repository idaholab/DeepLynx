using Asp.Versioning;
using deeplynx.helpers.Context;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers.V2;

[ApiController]
[ApiVersion(2)]
[Route("oauth/device")]
[Tags("OauthDeviceAuthorization")]
public class OauthDeviceAuthorizationController : ControllerBase
{
    private const string DeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";
    private const string RefreshTokenGrantType = "refresh_token";

    private readonly IOauthDeviceAuthorizationBusiness _oauthDeviceAuthorizationBusiness;

    public OauthDeviceAuthorizationController(
        IOauthDeviceAuthorizationBusiness oauthDeviceAuthorizationBusiness)
    {
        _oauthDeviceAuthorizationBusiness = oauthDeviceAuthorizationBusiness;
    }

    /// <summary>
    ///     OAuth 2.0 Device Authorization Endpoint
    /// </summary>
    /// <returns>Device and user codes for starting the device authorization flow</returns>
    [AllowAnonymous]
    [HttpPost("code", Name = "api_oauth_device_code")]
    public async Task<IActionResult> CreateDeviceCode(
        [FromForm(Name = "client_id")] string? clientId,
        [FromForm] string? scope)
    {
        var verificationUri = BuildVerificationUri();
        var response = await _oauthDeviceAuthorizationBusiness.CreateDeviceAuthorizationRequest(
            clientId,
            scope,
            verificationUri);

        return Ok(response);
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
    [HttpPost("/oauth/token", Name = "api_oauth_token")]
    public async Task<IActionResult> ExchangeOauthToken(
        [FromForm(Name = "grant_type")] string? grantType,
        [FromForm(Name = "device_code")] string? deviceCode,
        [FromForm(Name = "refresh_token")] string? refreshToken,
        [FromForm(Name = "client_id")] string? clientId)
    {
        if (string.IsNullOrWhiteSpace(grantType))
        {
            throw new OauthException("invalid_request", "grant_type is required", StatusCodes.Status400BadRequest);
        }

        OauthTokenGrantResponseDto response;

        if (grantType == DeviceCodeGrantType)
        {
            response = await _oauthDeviceAuthorizationBusiness.ExchangeDeviceCodeForToken(deviceCode, clientId);
        }
        else if (grantType == RefreshTokenGrantType)
        {
            response = await _oauthDeviceAuthorizationBusiness.ExchangeRefreshTokenForToken(refreshToken, clientId);
        }
        else
        {
            throw new OauthException("unsupported_grant_type", "Unsupported grant_type", StatusCodes.Status400BadRequest);
        }

        return Ok(response);
    }

    /// <summary>
    ///     Look up an OAuth device authorization request by user code
    /// </summary>
    /// <param name="userCode">The user code shown by the device</param>
    /// <returns>Device authorization request details for verification</returns>
    [Authorize]
    [HttpGet("verify", Name = "api_oauth_device_verify_lookup")]
    public async Task<IActionResult> GetDeviceAuthorizationRequest([FromQuery(Name = "user_code")] string? userCode)
    {
        var response = await _oauthDeviceAuthorizationBusiness.GetDeviceAuthorizationRequest(userCode);

        return Ok(response);
    }

    /// <summary>
    ///     Approve or deny an OAuth device authorization request
    /// </summary>
    /// <param name="requestDto">The user code and approval decision</param>
    /// <returns>The updated device authorization request</returns>
    [Authorize]
    [HttpPost("verify", Name = "api_oauth_device_verify_decision")]
    public async Task<IActionResult> SetDeviceAuthorizationDecision(
        [FromBody] DeviceAuthorizationDecisionRequestDto requestDto)
    {
        var userId = UserContextStorage.UserId;
        var response = await _oauthDeviceAuthorizationBusiness.SetDeviceAuthorizationDecision(
            requestDto.UserCode,
            requestDto.Approve,
            userId);

        return Ok(response);
    }

    private string BuildVerificationUri()
    {
        var hostedLink = Environment.GetEnvironmentVariable("HOSTED_LINK")?.TrimEnd('/');

        return $"{hostedLink}/oauth/device/verify";
    }
}
