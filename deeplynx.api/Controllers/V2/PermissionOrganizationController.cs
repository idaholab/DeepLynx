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
///     Controller for managing organization and default permissions.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve organization permission information.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("organizations/{organizationId:long}/permissions")]
[ForbidServiceAccounts] // service accounts can only act on the project level
[Authorize]
[Tags("Organization - Permission")]
public class PermissionOrganizationController : ControllerBase
{
    private readonly ILogger<PermissionOrganizationController> _logger;
    private readonly IPermissionBusiness _permissionBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PermissionOrganizationController" /> class
    /// </summary>
    /// <param name="permissionBusiness">The business logic interface for handling organization permission operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public PermissionOrganizationController(IPermissionBusiness permissionBusiness, ILogger<PermissionOrganizationController> logger)
    {
        _permissionBusiness = permissionBusiness;
        _logger = logger;
    }



    /// <summary>
    ///     Get All Permissions 
    /// </summary>
    /// <param name="organizationId">(Optional)The ID of the organization to which the project belongs. If not supplied, will get all defaults.</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived permissions from the result (Default true)</param>
    /// <returns>A list of permissions for the given organization/project.</returns>
    [HttpGet(Name = "api_get_all_organization_permissions")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "permission")]
    public async Task<ActionResult<IEnumerable<PermissionResponseDto>>> GetAllPermissions(
        long organizationId,
        [FromQuery] bool hideArchived = true)
    {
            var permissions =
                await _permissionBusiness.GetAllPermissions(
                    null, null, organizationId, hideArchived);
            return Ok(permissions);
    }



    /// <summary>
    ///     Get a Permission 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the permission belongs</param>
    /// <param name="permissionId">The ID of the permission to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived permissions from the result (Default true)</param>
    /// <returns>The permission associated with the given ID</returns>
    [HttpGet("{permissionId:long}", Name = "api_get_organization_permission")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "permission")]
    public async Task<ActionResult<PermissionResponseDto>> GetPermission(
        long organizationId,
        long permissionId,
        [FromQuery] bool hideArchived = true)
    {
            var permission = await _permissionBusiness.GetPermission(organizationId, null, permissionId, hideArchived);
            return Ok(permission);
    }



    /// <summary>
    ///     Create a Permission 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the permission belongs</param>
    /// <param name="dto">The data transfer object containing permission details</param>
    /// <returns>The created permission</returns>
    [HttpPost(Name = "api_create_organization_permission")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "permission")]
    public async Task<ActionResult<PermissionResponseDto>> CreatePermission(
        long organizationId,
        [FromBody] CreatePermissionRequestDto dto)
    {
            var currentUserId = UserContextStorage.UserId;
            var permission =
                await _permissionBusiness.CreatePermission(currentUserId, dto, null,
                    organizationId);
            return Ok(permission);
    }



    /// <summary>
    ///     Update a Permission 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the permission belongs</param>
    /// <param name="permissionId">The ID of the permission to update</param>
    /// <param name="dto">The data transfer object containing updated permission details</param>
    /// <returns>The updated permission</returns>
    [HttpPut("{permissionId:long}", Name = "api_update_organization_permission")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "permission")]
    public async Task<ActionResult<PermissionResponseDto>> UpdatePermission(
        long organizationId,
        long permissionId,
        [FromBody] UpdatePermissionRequestDto dto)
    {
            var currentUserId = UserContextStorage.UserId;
            var permission = await _permissionBusiness.UpdatePermission(organizationId, null, currentUserId, permissionId, dto);
            return Ok(permission);
    }



    /// <summary>
    ///     Delete a Permission 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the permission belongs</param>
    /// <param name="permissionId">The ID of the permission to delete</param>
    /// <returns>True if the permission was successfully deleted.</returns>
    [HttpDelete("{permissionId:long}", Name = "api_delete_organization_permission")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "permission")]
    public async Task<ActionResult<bool>> DeletePermission(
        long organizationId,
        long permissionId)
    {
            var currentUserId = UserContextStorage.UserId;
            var response = await _permissionBusiness.DeletePermission(organizationId, null, currentUserId, permissionId);
            return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive a Permission 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the permission belongs</param>
    /// <param name="permissionId">The ID of the permission to archive or unarchive</param>
    /// <param name="archive">True to archive the permission, false to unarchive it.</param>
    /// <returns>True if the permission was successfully archived or unarchived.</returns>
    [HttpPatch("{permissionId:long}", Name = "api_archive_organization_permission")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "permission")]
    public async Task<ActionResult<bool>> ArchivePermission(
        long organizationId,
        long permissionId,
        [FromQuery] bool archive)
    {
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                var responseA = await _permissionBusiness.ArchivePermission(organizationId, null, currentUserId, permissionId);
                return Ok(responseA);
            }

            var responseB = await _permissionBusiness.UnarchivePermission(organizationId, null, currentUserId, permissionId);
            return Ok(responseB);
    }
}