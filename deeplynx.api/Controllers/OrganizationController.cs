using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using Microsoft.AspNetCore.Authorization;

namespace deeplynx.api.Controllers;

[ApiController]
[Route("organizations")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly ILogger<OrganizationController> _logger;
    private readonly IOrganizationBusiness _organizationBusiness;
    private readonly IInvitationBusiness _invitationBusiness;

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
    ///  Get All Organizations
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived orgs</param>
    /// <returns></returns>
    [HttpGet(Name = "api_get_all_organizations")]
    [Auth("read", "organization")]
    public async Task<ActionResult<IEnumerable<OrganizationResponseDto>>> GetAllOrganizations(
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var organizations = await _organizationBusiness
                .GetAllOrganizations(hideArchived);
            return Ok(organizations);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing organizations: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///  Get Organizations for User
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived orgs</param>
    /// <returns></returns>
    [HttpGet("user", Name = "api_get_organizations_for_user")]
    public async Task<ActionResult<IEnumerable<OrganizationResponseDto>>> GetAllOrganizationsForUser(
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var organizations = await _organizationBusiness
                .GetAllOrganizationsForUser(currentUserId, hideArchived);
            return Ok(organizations);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing organizations: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }


    /// <summary>
    ///     Fetch Organization by ID
    /// </summary>
    /// <param name="organizationId">ID of organization</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived orgs</param>
    /// <returns></returns>
    [HttpGet("{organizationId:long}", Name = "api_get_organization")]
    [Auth("read", "organization")]
    public async Task<ActionResult<OrganizationResponseDto>> GetOrganization(
        long organizationId, [FromQuery] bool hideArchived = true)
    {
        try
        {
            var organization = await _organizationBusiness.GetOrganization(organizationId, hideArchived);
            return Ok(organization);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving organization {organizationId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create an Organization
    /// </summary>
    /// <param name="dto">Data structure of organization to create</param>
    /// <returns></returns>
    [HttpPost(Name = "api_create_organization")]
    [Auth("write", "organization")]
    public async Task<ActionResult<OrganizationResponseDto>> CreateOrganization(
        [FromBody] CreateOrganizationRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var organization = await _organizationBusiness.CreateOrganization(currentUserId, dto);
            return Ok(organization);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating organization: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Update an Organization
    /// </summary>
    /// <param name="organizationId">ID of the organization</param>
    /// <param name="dto">Fields to update</param>
    /// <returns></returns>
    [HttpPut("{organizationId:long}", Name = "api_update_organization")]
    [Auth("write", "organization")]
    public async Task<ActionResult<OrganizationResponseDto>> UpdateOrganization(
        long organizationId,
        [FromBody] UpdateOrganizationRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var organization = await _organizationBusiness.UpdateOrganization(currentUserId, organizationId, dto);
            return Ok(organization);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating organization {organizationId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete an Organization
    /// </summary>
    /// <param name="organizationId">ID of the organization to hard delete</param>
    /// <returns></returns>
    [HttpDelete("{organizationId:long}", Name = "api_delete_organization")]
    [Auth("write", "organization")]
    public async Task<ActionResult> DeleteOrganization(long organizationId)
    {
        try
        {
            await _organizationBusiness.DeleteOrganization(organizationId);
            return Ok(new { message = $"Deleted organization {organizationId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting organization {organizationId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive an Organization
    /// </summary>
    /// <param name="organizationId">The ID of the organization</param>
    /// <param name="archive">True to archive the organization, false to unarchive it.</param>
    /// <returns>A message stating the organization was successfully archived or unarchived.</returns>
    [HttpPatch("{organizationId:long}", Name = "api_archive_organization")]
    [Auth("write", "organization", includeArchived: true)]
    public async Task<IActionResult> ArchiveOrganization(
        long organizationId,
        [FromQuery] bool archive)
    {
        try
        {
            var userId = UserContextStorage.UserId;
            if (archive)
            {
                await _organizationBusiness.ArchiveOrganization(userId, organizationId);
                return Ok(new { message = $"Archived organization {organizationId}" });
            }

            await _organizationBusiness.UnarchiveOrganization(userId, organizationId);
            return Ok(new { message = $"Unarchived organization {organizationId}" });
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} organization {organizationId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Add User to Organization
    /// </summary>
    /// <param name="organizationId">ID of the organization</param>
    /// <param name="userId">ID of the user to be added</param>
    /// <param name="isAdmin"></param>
    /// <returns></returns>
    [HttpPost("{organizationId:long}/user", Name = "api_add_user_to_organization")]
    [Auth("write", "organization")]
    public async Task<ActionResult> AddUserToOrganization(
        long organizationId,
        [FromQuery] long userId,
        [FromQuery] bool isAdmin = false)
    {
        try
        {
            await _organizationBusiness.AddUserToOrganization(organizationId, userId, isAdmin);
            return Ok(new { message = $"Added user {userId} to organization {organizationId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while adding user {userId} to organization {organizationId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Set Admin Status for Organization User
    /// </summary>
    /// <param name="organizationId">ID of the organization</param>
    /// <param name="userId">ID of the user</param>
    /// <param name="isAdmin">isAdmin status</param>
    /// <returns></returns>
    [HttpPut("{organizationId:long}/admin", Name = "api_update_organization_admin_status")]
    [Auth("write", "organization")]
    public async Task<ActionResult> SetOrganizationAdminStatus(
        long organizationId,
        [FromQuery] long userId,
        [FromQuery] bool isAdmin)
    {
        try
        {
            await _organizationBusiness.SetOrganizationAdminStatus(organizationId, userId, isAdmin);
            return Ok(new { message = $"Adjusted admin status for user {userId} in organization {organizationId}" });
        }
        catch (Exception exc)
        {
            var message =
                $"An error occurred while setting admin status for user {userId} in organization {organizationId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Remove User from Organization
    /// </summary>
    /// <param name="organizationId">ID of the organization to remove from</param>
    /// <param name="userId">ID of user to be removed</param>
    /// <returns></returns>
    [HttpDelete("{organizationId:long}/user", Name = "api_remove_user_from_organization")]
    [Auth("write", "organization")]
    public async Task<ActionResult> RemoveUserFromOrganization(
        long organizationId,
        [FromQuery] long userId)
    {
        try
        {
            await _organizationBusiness.RemoveUserFromOrganization(organizationId, userId);
            return Ok(new { message = $"Removed user {userId} from organization {organizationId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while removing user {userId} from organization {organizationId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    /// Invite/Add User to Organization
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="userEmail"></param>
    /// <param name="userName"></param>
    /// <returns></returns>
    [HttpPost("{organizationId:long}/invite", Name = "api_invite_user_to_organization")]
    [Auth("write", "user")]
    public async Task<ActionResult> InviteUserToOrganization(
        long organizationId,
        [FromQuery] string userEmail,
        [FromQuery] string? userName)
    {
        try
        {
            await _invitationBusiness.InviteAndAddUserToHierarchy(organizationId, null, null, userEmail, userName);
            return Ok(new { message = $"Invited and added inactive user with email {userEmail} to organization {organizationId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while adding user with email {userEmail} to organization {organizationId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}