using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for managing roles.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve role information.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("organizations/{organizationId:long}/roles")]
[Authorize]
[ForbidServiceAccounts] // service accounts can only act on the project level
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
    /// <param name="paginatedRequestDto">Pagination parameters</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived roles from the result (Default true)</param>
    /// <returns>A paginated list of roles for the given organization.</returns>
    [HttpGet(Name = "api_get_all_roles_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "role")]
    public async Task<ActionResult<IEnumerable<RoleResponseDto>>> GetAllRoles(
        long organizationId,
        [FromQuery] PaginatedRequestDto? paginatedRequestDto = null,
        [FromQuery] bool hideArchived = true)
    {
        paginatedRequestDto ??= new PaginatedRequestDto();
        var roles = await _roleBusiness.GetAllRolesPaginated(organizationId, null, paginatedRequestDto, hideArchived);
        return Ok(roles);
    }



    /// <summary>
    ///     Get a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived roles from the result (Default true)</param>
    /// <returns>The role associated with the given ID</returns>
    [HttpGet("{roleId:long}", Name = "api_get_role_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "role")]
    public async Task<ActionResult<RoleResponseDto>> GetRole(
        long organizationId,
        long roleId,
        [FromQuery] bool hideArchived = true)
    {
        var role = await _roleBusiness.GetRole(roleId, organizationId, null, hideArchived);
        return Ok(role);
    }



    /// <summary>
    ///     Create a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="dto">The data transfer object containing role details</param>
    /// <returns>The created role</returns>
    [HttpPost(Name = "api_create_role_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "role")]
    public async Task<ActionResult<RoleResponseDto>> CreateRole(
        long organizationId,
        [FromBody] CreateRoleRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var role = await _roleBusiness.CreateRole(currentUserId, dto, organizationId, null);
        return Ok(role);
    }



    /// <summary>
    ///     Update a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role to update</param>
    /// <param name="dto">The data transfer object containing updated role details</param>
    /// <returns>The updated role</returns>
    [HttpPut("{roleId:long}", Name = "api_update_role_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "role")]
    public async Task<ActionResult<RoleResponseDto>> UpdateRole(
        long organizationId,
        long roleId,
        [FromBody] UpdateRoleRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var role = await _roleBusiness.UpdateRole(currentUserId, roleId, organizationId, null, dto);
        return Ok(role);
    }



    /// <summary>
    ///     Delete a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role to delete</param>
    /// <returns>True if the role was successfully deleted.</returns>
    [HttpDelete("{roleId:long}", Name = "api_delete_role_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "role")]
    public async Task<ActionResult<bool>> DeleteRole(
        long organizationId,
        long roleId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _roleBusiness.DeleteRole(currentUserId, roleId, organizationId, null);
        return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role to archive or unarchive</param>
    /// <param name="archive">True to archive the role, false to unarchive it.</param>
    /// <returns>True if the role was successfully archived or unarchived.</returns>
    [HttpPatch("{roleId:long}", Name = "api_archive_role_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "role")]
    public async Task<ActionResult<bool>> ArchiveRole(
        long organizationId,
        long roleId,
        [FromQuery] bool archive)
    {
        var userId = UserContextStorage.UserId;
        if (archive)
        {
            var responseA = await _roleBusiness.ArchiveRole(userId, roleId, organizationId, null);
            return Ok(responseA);
        }

        var responseB = await _roleBusiness.UnarchiveRole(userId, roleId, organizationId, null);
        return Ok(responseB);
    }



    /// <summary>
    ///     Get Permissions for a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role whose permissions to retrieve</param>
    /// <returns>A list of permissions associated with the role</returns>
    [HttpGet("{roleId:long}/permissions", Name = "api_get_permissions_by_role_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "role")]
    [Auth("read", "permission")]
    public async Task<ActionResult<IEnumerable<PermissionResponseDto>>> GetPermissionsByRole(
        long organizationId,
        long roleId)
    {
        var permissions = await _roleBusiness.GetPermissionsByRole(roleId, organizationId, null);
        return Ok(permissions);
    }



    /// <summary>
    ///     Add Permission to Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role</param>
    /// <param name="permissionId">The ID of the permission to add</param>
    /// <returns>True if the permission was successfully added to the role.</returns>
    [HttpPost("{roleId:long}/permissions/{permissionId:long}", Name = "api_add_permission_to_role_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "role")]
    [Auth("read", "permission")]
    [Auth("update", "user")]
    public async Task<ActionResult<bool>> AddPermissionToRole(
        long organizationId,
        long roleId,
        long permissionId)
    {
        var response = await _roleBusiness.AddPermissionToRole(roleId, permissionId, organizationId, null);
        return Ok(response);
    }



    /// <summary>
    ///     Remove Permission from Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role</param>
    /// <param name="permissionId">The ID of the permission to remove</param>
    /// <returns>True if the permission was successfully removed from the role.</returns>
    [HttpDelete("{roleId:long}/permissions/{permissionId:long}", Name = "api_remove_permission_from_role_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "role")]
    [Auth("read", "permission")]
    [Auth("update", "user")]
    public async Task<ActionResult<bool>> RemovePermissionFromRole(
        long organizationId,
        long roleId,
        long permissionId)
    {
        var response = await _roleBusiness.RemovePermissionFromRole(roleId, permissionId, organizationId, null);
        return Ok(response);
    }



    /// <summary>
    ///     Set All Permissions for a Role 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the role belongs</param>
    /// <param name="roleId">The ID of the role</param>
    /// <param name="permissionIds">Array of permission IDs to assign to the role (replaces existing permissions)</param>
    /// <returns>True if the permissions were successfully set for the role.</returns>
    [HttpPut("{roleId:long}/permissions", Name = "api_set_permissions_for_role_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "role")]
    [Auth("read", "permission")]
    public async Task<ActionResult<bool>> SetPermissionsForRole(
        long organizationId,
        long roleId,
        [FromBody] long[] permissionIds)
    {
        var response = await _roleBusiness.SetPermissionsForRole(roleId, permissionIds, organizationId, null);
        return Ok(response);
    }
}