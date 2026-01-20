using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using Microsoft.AspNetCore.Authorization;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing roles.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve role information.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/roles")]
[Authorize]
[Tags("Organization - Role")]
public class RoleOrganizationController : ControllerBase
{
    private readonly ILogger<RoleProjectController> _logger;
    private readonly IRoleBusiness _roleBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RoleProjectController" /> class
    /// </summary>
    /// <param name="roleBusiness">The business logic interface for handling Role operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public RoleOrganizationController(IRoleBusiness roleBusiness, ILogger<RoleProjectController> logger)
    {
        _roleBusiness = roleBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Roles 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived roles from the result (Default true)</param>
    /// <returns>A list of roles for the given organization.</returns>
    [HttpGet(Name = "api_get_all_roles_organization")]
    [Auth("read", "role")]
    public async Task<ActionResult<IEnumerable<RoleResponseDto>>> GetAllRoles(
        long organizationId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var roles = await _roleBusiness.GetAllRoles(organizationId, null, hideArchived);
            return Ok(roles);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing roles: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived roles from the result (Default true)</param>
    /// <returns>The role associated with the given ID</returns>
    [HttpGet("{roleId:long}", Name = "api_get_role_organization")]
    [Auth("read", "role")]
    public async Task<ActionResult<RoleResponseDto>> GetRole(
        long organizationId,
        long roleId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var role = await _roleBusiness.GetRole(roleId, organizationId, null, hideArchived);
            return Ok(role);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving role {roleId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="dto">The data transfer object containing role details</param>
    /// <returns>The created role</returns>
    [HttpPost(Name = "api_create_role_organization")]
    [Auth("write", "role")]
    public async Task<ActionResult<RoleResponseDto>> CreateRole(
        long organizationId,
        [FromBody] CreateRoleRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var role = await _roleBusiness.CreateRole(currentUserId, dto, organizationId, null);
            return Ok(role);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating role: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Update a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role to update</param>
    /// <param name="dto">The data transfer object containing updated role details</param>
    /// <returns>The updated role</returns>
    [HttpPut("{roleId:long}", Name = "api_update_role_organization")]
    [Auth("write", "role")]
    public async Task<ActionResult<RoleResponseDto>> UpdateRole(
        long organizationId,
        long roleId,
        [FromBody] UpdateRoleRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var role = await _roleBusiness.UpdateRole(currentUserId, roleId, organizationId, null, dto);
            return Ok(role);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating role {roleId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role to delete</param>
    /// <returns>A message stating the role was successfully deleted.</returns>
    [HttpDelete("{roleId:long}", Name = "api_delete_role_organization")]
    [Auth("write", "role")]
    public async Task<ActionResult> DeleteRole(
        long organizationId,
        long roleId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _roleBusiness.DeleteRole(currentUserId, roleId, organizationId, null);
            return Ok(new { message = $"Deleted role {roleId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting role {roleId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role to archive or unarchive</param>
    /// <param name="archive">True to archive the role, false to unarchive it.</param>
    /// <returns>A message stating the role was successfully archived or unarchived.</returns>
    [HttpPatch("{roleId:long}", Name = "api_archive_role_organization")]
    [Auth("write", "role")]
    public async Task<IActionResult> ArchiveRole(
        long organizationId,
        long roleId,
        [FromQuery] bool archive)
    {
        try
        {
            var userId = UserContextStorage.UserId;
            if (archive)
            {
                await _roleBusiness.ArchiveRole(userId, roleId, organizationId, null);
                return Ok(new { message = $"Archived role {roleId}" });
            }

            await _roleBusiness.UnarchiveRole(userId, roleId, organizationId, null);
            return Ok(new { message = $"Unarchived role {roleId}" });
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} role {roleId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Permissions for a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role whose permissions to retrieve</param>
    /// <returns>A list of permissions associated with the role</returns>
    [HttpGet("{roleId:long}/permissions", Name = "api_get_permissions_by_role_organization")]
    [Auth("read", "role")]
    public async Task<ActionResult<IEnumerable<PermissionResponseDto>>> GetPermissionsByRole(
        long organizationId,
        long roleId)
    {
        try
        {
            var permissions = await _roleBusiness.GetPermissionsByRole(roleId, organizationId, null);
            return Ok(permissions);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving permissions for role {roleId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Add Permission to Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role</param>
    /// <param name="permissionId">The ID of the permission to add</param>
    /// <returns>A message stating the permission was successfully added to the role.</returns>
    [HttpPost("{roleId:long}/permissions/{permissionId:long}", Name = "api_add_permission_to_role_organization")]
    [Auth("write", "role")]
    public async Task<ActionResult> AddPermissionToRole(
        long organizationId,
        long roleId,
        long permissionId)
    {
        try
        {
            await _roleBusiness.AddPermissionToRole(roleId, permissionId, organizationId, null);
            return Ok(new { message = $"Added permission {permissionId} to role {roleId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while adding permission {permissionId} to role {roleId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Remove Permission from Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role</param>
    /// <param name="permissionId">The ID of the permission to remove</param>
    /// <returns>A message stating the permission was successfully removed from the role.</returns>
    [HttpDelete("{roleId:long}/permissions/{permissionId:long}", Name = "api_remove_permission_from_role_organization")]
    [Auth("write", "role")]
    public async Task<ActionResult> RemovePermissionFromRole(
        long organizationId,
        long roleId,
        long permissionId)
    {
        try
        {
            await _roleBusiness.RemovePermissionFromRole(roleId, permissionId, organizationId, null);
            return Ok(new { message = $"Removed permission {permissionId} from role {roleId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while removing permission {permissionId} from role {roleId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Set All Permissions for a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role</param>
    /// <param name="permissionIds">Array of permission IDs to assign to the role (replaces existing permissions)</param>
    /// <returns>A message stating the permissions were successfully set for the role.</returns>
    [HttpPut("{roleId:long}/permissions", Name = "api_set_permissions_for_role_organization")]
    [Auth("write", "role")]
    public async Task<ActionResult> SetPermissionsForRole(
        long organizationId,
        long roleId,
        [FromBody] long[] permissionIds)
    {
        try
        {
            await _roleBusiness.SetPermissionsForRole(roleId, permissionIds, organizationId, null);
            return Ok(new { message = $"Set permissions for role {roleId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while setting permissions for role {roleId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}