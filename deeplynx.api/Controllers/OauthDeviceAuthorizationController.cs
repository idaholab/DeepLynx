using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

[ApiController]
[Route("oauth/device")]
[Tags("OauthDeviceAuthorization")]
public class OauthDeviceAuthorizationController : ControllerBase
{
    private readonly IOauthDeviceAuthorizationBusiness _oauthDeviceAuthorizationBusiness;
    private readonly ILogger<OauthDeviceAuthorizationController> _logger;

    public OauthDeviceAuthorizationController(
        IOauthDeviceAuthorizationBusiness oauthDeviceAuthorizationBusiness,
        ILogger<OauthDeviceAuthorizationController> logger)
    {
        _oauthDeviceAuthorizationBusiness = oauthDeviceAuthorizationBusiness;
        _logger = logger;
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
        try
        {
            var verificationUri = BuildVerificationUri();
            var response = await _oauthDeviceAuthorizationBusiness.CreateDeviceAuthorizationRequest(
                clientId,
                scope,
                verificationUri);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid OAuth device authorization request");
            return BadRequest(new OauthErrorResponseDto
            {
                Error = "invalid_request",
                ErrorDescription = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "OAuth application not found for device authorization request");
            return NotFound(new OauthErrorResponseDto
            {
                Error = "invalid_client",
                ErrorDescription = ex.Message
            });
        }
        catch (Exception ex)
        {
            const string message = "An unexpected error occurred in the OAuth device authorization flow";
            _logger.LogError(ex, message);

            return StatusCode(StatusCodes.Status500InternalServerError, new OauthErrorResponseDto
            {
                Error = "server_error",
                ErrorDescription = message
            });
        }
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
        try
        {
            var response = await _oauthDeviceAuthorizationBusiness.GetDeviceAuthorizationRequest(userCode);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid OAuth device verification lookup request");
            return BadRequest(new OauthErrorResponseDto
            {
                Error = "invalid_request",
                ErrorDescription = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "OAuth device authorization request not found");
            return NotFound(new OauthErrorResponseDto
            {
                Error = "invalid_request",
                ErrorDescription = ex.Message
            });
        }
        catch (Exception ex)
        {
            const string message = "An unexpected error occurred while looking up the OAuth device authorization request";
            _logger.LogError(ex, message);

            return StatusCode(StatusCodes.Status500InternalServerError, new OauthErrorResponseDto
            {
                Error = "server_error",
                ErrorDescription = message
            });
        }
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
        try
        {
            var userId = UserContextStorage.UserId;
            var response = await _oauthDeviceAuthorizationBusiness.SetDeviceAuthorizationDecision(
                requestDto.UserCode,
                requestDto.Approve,
                userId);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid OAuth device verification decision request");
            return BadRequest(new OauthErrorResponseDto
            {
                Error = "invalid_request",
                ErrorDescription = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "OAuth device authorization request not found");
            return NotFound(new OauthErrorResponseDto
            {
                Error = "invalid_request",
                ErrorDescription = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid OAuth device verification decision");
            return BadRequest(new OauthErrorResponseDto
            {
                Error = "invalid_request",
                ErrorDescription = ex.Message
            });
        }
        catch (Exception ex)
        {
            const string message = "An unexpected error occurred while updating the OAuth device authorization request";
            _logger.LogError(ex, message);

            return StatusCode(StatusCodes.Status500InternalServerError, new OauthErrorResponseDto
            {
                Error = "server_error",
                ErrorDescription = message
            });
        }
    }

    private string BuildVerificationUri()
    {
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : string.Empty;

        return $"{Request.Scheme}://{Request.Host}{pathBase}/oauth/device/verify";
    }
}
