using System.Security;
using System.Web;
using Asp.Versioning;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

[ApiController]

[ApiVersion(2)]
[Authorize]
[Route("oauth")]
[Tags("OauthHandshake")]
public class OauthHandshakeController : ControllerBase
{
    private readonly ILogger<OauthHandshakeController> _logger;
    private readonly IOauthHandshakeBusiness _oauthBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OauthHandshakeController" /> class
    /// </summary>
    /// <param name="oauthBusiness">The business logic interface for handling record operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public OauthHandshakeController(
        IOauthHandshakeBusiness oauthBusiness,
        ILogger<OauthHandshakeController> logger
    )
    {
        _oauthBusiness = oauthBusiness;
        _logger = logger;
    }



    /// <summary>
    ///     Oauth 2.0 Authorization Endpoint
    /// </summary>
    /// <param name="clientId">The known client ID of the requesting application</param>
    /// <param name="redirectUri">The callback URL of the requesting application</param>
    /// <param name="state">CSRF protection token</param>
    /// <returns>A redirect to the callback URL containing the authorization code and state.</returns>
    /// <remarks>
    ///     This endpoint requires authentication. The Next.js proxy ensures the user
    ///     is authenticated before forwarding the request here.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("authorize", Name = "api_oauth_authorize")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<IActionResult> Authorize(
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        [FromQuery] string state)
    {
        var userId = UserContextStorage.UserId;

        var authCode = await _oauthBusiness.GenerateAuthCode(clientId, userId, redirectUri, state);
        var callbackUrl = BuildCallbackUrl(redirectUri, authCode, state);

        return Redirect(callbackUrl);
    }



    /// <summary>
    ///     Oauth 2.0 Token Endpoint
    /// </summary>
    /// <param name="code">The authorization code received from the authorize endpoint</param>
    /// <param name="clientId">The Oauth application's client ID</param>
    /// <param name="clientSecret">The Oauth application's client secret</param>
    /// <param name="redirectUri">The same redirect URI used in the authorize request</param>
    /// <param name="state">The same CSRF state used in the authorize request</param>
    /// <param name="expiration">Optional token expiration time in minutes; defaults to 480.</param>
    /// <returns>An OAuth token response containing the access token.</returns>
    /// <remarks>
    ///     This endpoint does not require user authentication. It uses client credentials
    ///     to authenticate the OAuth application.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("exchange", Name = "api_oauth_exchange")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<IActionResult> Exchange(
        [FromQuery] string code,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "client_secret")] string clientSecret,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        [FromQuery] string state,
        [FromQuery] double? expiration)
    {
        var token = await _oauthBusiness.ExchangeAuthCodeForToken(
            code,
            clientId,
            clientSecret,
            redirectUri,
            state,
            expiration);

        return Ok(token);
    }

    private string BuildCallbackUrl(string baseUrl, string code, string state)
    {
        try
        {
            var uriBuilder = new UriBuilder(baseUrl);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["code"] = code;
            query["state"] = state;

            uriBuilder.Query = query.ToString();
            return uriBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error building callback URL. BaseUrl: {baseUrl}, Code: {code?.Substring(0, Math.Min(10, code?.Length ?? 0))}..., State: {state}");
            throw;
        }
    }
}
