using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

[ApiController]

[ApiVersion(2)]
[Route("organizations")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly IInvitationBusiness _invitationBusiness;
    private readonly ILogger<OrganizationController> _logger;
    private readonly IOrganizationBusiness _organizationBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OrganizationController" /> class
    /// </summary>
    /// <param name="organizationBusiness">The business logic interface for handling Organization operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public OrganizationController(
        IOrganizationBusiness organizationBusiness,
        IInvitationBusiness invitationBusiness,
        ILogger<OrganizationController> logger)
    {
        _organizationBusiness = organizationBusiness;
        _invitationBusiness = invitationBusiness;
        _logger = logger;
    }



    /// <summary>
    ///     Get All Organizations
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived orgs</param>
    /// <param name="paginatedRequestDto"> Pagination parameters</param>
    /// <returns>A list of organizations visible to the current user.</returns>
    [HttpGet(Name = "api_get_all_organizations")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<PaginatedResponse<OrganizationResponseDto>>> GetAllOrganizations(
        [FromQuery] bool hideArchived = true,
        [FromQuery] PaginatedRequestDto? paginatedRequestDto = null)
    {
        paginatedRequestDto ??= new PaginatedRequestDto();
        var userId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var organizations = await _organizationBusiness
            .GetAllOrganizationsPaginated(userId, paginatedRequestDto, hideArchived, isSysAdmin);
        return Ok(organizations);
    }


    /// <summary>
    ///     Get Organizations for User
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived orgs</param>
    /// <param name="paginatedRequestDto"> Pagination parameters</param>
    /// <returns>A list of organizations associated with the current user.</returns>
    [HttpGet("user", Name = "api_get_organizations_for_user")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<PaginatedResponse<OrganizationResponseDto>>> GetAllOrganizationsForUser(
        [FromQuery] bool hideArchived = true,
        [FromQuery] PaginatedRequestDto? paginatedRequestDto = null)
    {
        var currentUserId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var organizations = await _organizationBusiness
            .GetAllOrganizationsForUserPaginated(currentUserId, paginatedRequestDto, hideArchived, isSysAdmin);
        return Ok(organizations);
    }




    /// <summary>
    ///     Fetch Organization by ID
    /// </summary>
    /// <param name="organizationId">ID of organization</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived orgs</param>
    /// <returns>The requested organization.</returns>
    [HttpGet("{organizationId:long}", Name = "api_get_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "organization")]
    public async Task<ActionResult<OrganizationResponseDto>> GetOrganization(
        long organizationId, [FromQuery] bool hideArchived = true)
    {
        var organization = await _organizationBusiness.GetOrganization(organizationId, hideArchived);
        return Ok(organization);
    }



    /// <summary>
    ///     Create an Organization
    /// </summary>
    /// <param name="dto">Data structure of organization to create</param>
    /// <returns>The newly created organization.</returns>
    [HttpPost(Name = "api_create_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<OrganizationResponseDto>> CreateOrganization(
        [FromBody] CreateOrganizationRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var organization = await _organizationBusiness.CreateOrganization(currentUserId, dto);
        return Ok(organization);
    }



    /// <summary>
    ///     Update an Organization
    /// </summary>
    /// <param name="organizationId">ID of the organization</param>
    /// <param name="dto">Fields to update</param>
    /// <returns>The updated organization.</returns>
    [HttpPut("{organizationId:long}", Name = "api_update_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "organization")]
    public async Task<ActionResult<OrganizationResponseDto>> UpdateOrganization(
        long organizationId,
        [FromBody] UpdateOrganizationRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var organization = await _organizationBusiness.UpdateOrganization(currentUserId, organizationId, dto);
        return Ok(organization);
    }



    /// <summary>
    ///     Delete an Organization
    /// </summary>
    /// <param name="organizationId">ID of the organization to hard delete</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpDelete("{organizationId:long}", Name = "api_delete_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult> DeleteOrganization(long organizationId)
    {
        var response = await _organizationBusiness.DeleteOrganization(organizationId);
        return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive an Organization
    /// </summary>
    /// <param name="organizationId">The ID of the organization</param>
    /// <param name="archive">True to archive the organization, false to unarchive it.</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpPatch("{organizationId:long}", Name = "api_archive_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "organization", true)]
    public async Task<IActionResult> ArchiveOrganization(
        long organizationId,
        [FromQuery] bool archive)
    {
        var userId = UserContextStorage.UserId;

        if (archive)
        {
            var archiveResponse = await _organizationBusiness.ArchiveOrganization(userId, organizationId);
            return Ok(archiveResponse);
        }

        var response = await _organizationBusiness.UnarchiveOrganization(userId, organizationId);
        return Ok(response);
    }



    /// <summary>
    ///     Add User to Organization
    /// </summary>
    /// <param name="organizationId">ID of the organization</param>
    /// <param name="userId">ID of the user to be added</param>
    /// <param name="isAdmin"></param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpPost("{organizationId:long}/user", Name = "api_add_user_to_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [OrgAdmin]
    public async Task<ActionResult> AddUserToOrganization(
        long organizationId,
        [FromQuery] long userId,
        [FromQuery] bool isAdmin = false)
    {
        var response = await _organizationBusiness.AddUserToOrganization(organizationId, userId, isAdmin);
        return Ok(response);
    }



    /// <summary>
    ///     Set Admin Status for Organization User
    /// </summary>
    /// <param name="organizationId">ID of the organization</param>
    /// <param name="userId">ID of the user</param>
    /// <param name="isAdmin">isAdmin status</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpPut("{organizationId:long}/admin", Name = "api_update_organization_admin_status")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [OrgAdmin]
    [Auth("update", "organization")]
    [Auth("update", "user")]
    public async Task<ActionResult> SetOrganizationAdminStatus(
        long organizationId,
        [FromQuery] long userId,
        [FromQuery] bool isAdmin)
    {
        var response = await _organizationBusiness.SetOrganizationAdminStatus(organizationId, userId, isAdmin);
        return Ok(response);
    }



    /// <summary>
    ///     Remove User from Organization
    /// </summary>
    /// <param name="organizationId">ID of the organization to remove from</param>
    /// <param name="userId">ID of user to be removed</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpDelete("{organizationId:long}/user", Name = "api_remove_user_from_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [OrgAdmin]
    [Auth("update", "organization")]
    [Auth("update", "user")]
    public async Task<ActionResult> RemoveUserFromOrganization(
        long organizationId,
        [FromQuery] long userId)
    {
        var response = await _organizationBusiness.RemoveUserFromOrganization(organizationId, userId);
        return Ok(response);
    }

    /// <summary>
    ///     Upload a Organization Logo
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="file">The file to upload</param>
    /// <returns>File path for the logo</returns>
    [HttpPost("{organizationId}/logo", Name = "api_upload_organization_logo")]
    [OrgAdmin]
    [Sensitivity("upload file")]
    public async Task<IActionResult> UploadOrganizationLogo(
        long organizationId,
        IFormFile file)
    {
        try
        {
            var logoUri = await _organizationBusiness.UploadOrganizationLogo(organizationId, file);

            return Ok(logoUri);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to upload organization logo for organization {organizationId}: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    /// <summary>
    ///     Get an Organization Logo
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <returns>File stream of the logo bytes</returns>
    [HttpGet("{organizationId}/logo/image", Name = "api_get_organization_image")]
    public async Task<IActionResult> GetOrganizationLogoImage(
        long organizationId)
    {
        try
        {
            var result = await _organizationBusiness.GetOrganizationLogoStreamAsync(organizationId);
            if (result == null)
                return NotFound();

            var (logoStream, fullPath) = result.Value;


            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fullPath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return File(logoStream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving logo image for organization {organizationId}");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    ///     Delete a Organization Logo
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <returns>True if file was sucessfully deleted</returns>
    [HttpDelete("{organizationId}/logo/delete", Name = "api_delete_organization_logo")]
    [OrgAdmin]
    [Sensitivity("delete file")]
    public async Task<IActionResult> RemoveOrganizationLogo(
        long organizationId)
    {
        try
        {
            var success = await _organizationBusiness.RemoveLogoFileAsync(organizationId);

            return Ok(success);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to remove active logo file for organization {organizationId}: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    /// <summary>
    ///     Invite/Add User to Organization
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="userEmail"></param>
    /// <param name="userId"></param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpPost("{organizationId:long}/invite", Name = "api_invite_user_to_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [ProjectAdmin(unscoped: true)]
    public async Task<ActionResult> InviteUserToOrganization(
        long organizationId,
        [FromQuery] string? userEmail,
        [FromQuery] long? userId)
    {
        var response = await _invitationBusiness.InviteAndAddUserToHierarchy(organizationId, null, null, null, userId, userEmail);
        return Ok(response);
    }
}
