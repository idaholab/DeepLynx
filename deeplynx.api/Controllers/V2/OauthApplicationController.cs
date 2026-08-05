using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

[ApiController]

[ApiVersion(2)]
[Authorize]
[SysAdmin]
[Route("oauth/applications")]
public class OauthApplicationController : ControllerBase
{
    private readonly ILogger<OauthApplicationController> _logger;
    private readonly IOauthApplicationBusiness _oauthApplicationBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OauthApplicationController" /> class
    /// </summary>
    /// <param name="oauthApplicationBusiness">The business logic interface for handling OAuth Application operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public OauthApplicationController(
        IOauthApplicationBusiness oauthApplicationBusiness,
        ILogger<OauthApplicationController> logger)
    {
        _oauthApplicationBusiness = oauthApplicationBusiness;
        _logger = logger;
    }



    /// <summary>
    ///     Get All OAuth Applications
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived applications</param>
    /// <returns>A list of OAuth applications.</returns>
    [HttpGet(Name = "api_get_all_oauth_applications")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<IEnumerable<OauthApplicationResponseDto>>> GetAllOauthApplications(
        [FromQuery] bool hideArchived = true)
    {
        var applications = await _oauthApplicationBusiness.GetAllOauthApplications(hideArchived);
        return Ok(applications);
    }



    /// <summary>
    ///     Get OAuth Application by ID
    /// </summary>
    /// <param name="applicationId">ID of OAuth application</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived applications</param>
    /// <returns>The requested OAuth application.</returns>
    [HttpGet("{applicationId:long}", Name = "api_get_oauth_application")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<OauthApplicationResponseDto>> GetOauthApplication(
        long applicationId,
        [FromQuery] bool hideArchived = true)
    {
        var application = await _oauthApplicationBusiness.GetOauthApplication(applicationId, hideArchived);
        return Ok(application);
    }



    /// <summary>
    ///     Create an OAuth Application
    /// </summary>
    /// <param name="dto">Data structure of OAuth application to create</param>
    /// <returns>The newly created OAuth application, including its generated client secret.</returns>
    [HttpPost(Name = "api_create_oauth_application")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<OauthApplicationSecureResponseDto>> CreateOauthApplication(
        [FromBody] CreateOauthApplicationRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var application = await _oauthApplicationBusiness.CreateOauthApplication(dto, currentUserId);
        return Ok(application);
    }



    /// <summary>
    ///     Update an OAuth Application
    /// </summary>
    /// <param name="applicationId">ID of the OAuth application</param>
    /// <param name="dto">Fields to update</param>
    /// <returns>The updated OAuth application.</returns>
    [HttpPut("{applicationId:long}", Name = "api_update_oauth_application")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<OauthApplicationResponseDto>> UpdateOauthApplication(
        long applicationId,
        [FromBody] UpdateOauthApplicationRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var application = await _oauthApplicationBusiness.UpdateOauthApplication(
            applicationId,
            dto,
            currentUserId);
        return Ok(application);
    }



    /// <summary>
    ///     Delete an OAuth Application
    /// </summary>
    /// <param name="applicationId">ID of the OAuth application to hard delete</param>
    /// <returns>A 200 OK response containing a boolean indicating whether the OAuth application was deleted.</returns>
    [HttpDelete("{applicationId:long}", Name = "api_delete_oauth_application")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult> DeleteOauthApplication(long applicationId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _oauthApplicationBusiness.DeleteOauthApplication(applicationId, currentUserId);
        return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive an OAuth Application
    /// </summary>
    /// <param name="applicationId">ID of the OAuth Application to archive or unarchive</param>
    /// <param name="archive">True to archive the application, false to unarchive it</param>
    /// <returns>A 200 OK response containing a boolean indicating whether the archive operation succeeded.</returns>
    [HttpPatch("{applicationId:long}", Name = "api_archive_oauth_application")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<IActionResult> ArchiveOauthApplication(
        long applicationId,
        [FromQuery] bool archive)
    {
        var userId = UserContextStorage.UserId;
        if (archive)
        {
            var archiveResponse = await _oauthApplicationBusiness.ArchiveOauthApplication(applicationId, userId);
            return Ok(archiveResponse);
        }

        var response = await _oauthApplicationBusiness.UnarchiveOauthApplication(applicationId, userId);
        return Ok(response);
    }
}
