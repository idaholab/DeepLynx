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
///     Controller for managing permissions.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve permission information.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/permissions")]
[Authorize]
[Tags("Project - Permission")]
public class PermissionProjectController : ControllerBase
{
    private readonly ILogger<PermissionProjectController> _logger;
    private readonly IPermissionBusiness _permissionBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PermissionProjectController" /> class
    /// </summary>
    /// <param name="permissionBusiness">The business logic interface for handling Permission operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public PermissionProjectController(IPermissionBusiness permissionBusiness, ILogger<PermissionProjectController> logger)
    {
        _permissionBusiness = permissionBusiness;
        _logger = logger;
    }



    /// <summary>
    ///     Get All Permissions 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose permissions are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived permissions from the result (Default true)</param>
    /// <returns>A list of permissions for the given organization/project.</returns>
    [HttpGet(Name = "api_get_all_project_permissions")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "permission")]
    public async Task<ActionResult<IEnumerable<PermissionResponseDto>>> GetAllPermissions(
        long organizationId,
        long projectId,
        [FromQuery] bool hideArchived = true)
    {
            var permissions =
                await _permissionBusiness.GetAllPermissions(null, projectId, organizationId,
                    hideArchived);
            return Ok(permissions);
    }



    /// <summary>
    ///     Get a Permission 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the permission belongs</param>
    /// <param name="projectId">The ID of the project to which the permission belongs</param>
    /// <param name="permissionId">The ID of the permission to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived permissions from the result (Default true)</param>
    /// <returns>The permission associated with the given ID</returns>
    [HttpGet("{permissionId:long}", Name = "api_get_project_permission")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "permission")]
    public async Task<ActionResult<PermissionResponseDto>> GetPermission(
        long organizationId,
        long projectId,
        long permissionId,
        [FromQuery] bool hideArchived = true)
    {
            var permission = await _permissionBusiness.GetPermission(organizationId, projectId, permissionId, hideArchived);
            return Ok(permission);
    }
}