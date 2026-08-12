using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for managing classes at the organization level.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve class information.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("organizations/{organizationId:long}/classes")]
[Authorize]
[ForbidServiceAccounts] // service accounts can only act on the project level
[Tags("Organization - Class")]
public class ClassOrganizationController : ControllerBase
{
    private readonly IClassBusiness _classBusiness;
    private readonly ILogger<ClassOrganizationController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClassOrganizationController" /> class
    /// </summary>
    /// <param name="classBusiness">The business logic interface for handling class operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public ClassOrganizationController(IClassBusiness classBusiness, ILogger<ClassOrganizationController> logger)
    {
        _classBusiness = classBusiness;
        _logger = logger;
    }



    /// <summary>
    ///     Get All Classes
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// <param name="projects">(Optional)An array of project IDs within the organization to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived classes from the result (Default true)</param>
    /// <param name="paginatedRequestDto"> Pagination parameters</param>
    /// <returns>Paginated list of class response DTOs</returns>
    [HttpGet(Name = "api_get_all_classes_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "class")]
    public async Task<ActionResult<PaginatedResponse<ClassResponseDto>>> GetAllClasses(
        long organizationId,
        [FromQuery] long[]? projects,
        [FromQuery] bool hideArchived = true,
        [FromQuery] PaginatedRequestDto? paginatedRequestDto = null)
    {
        paginatedRequestDto ??= new PaginatedRequestDto();
        var currentUserId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var isOrgAdmin = UserContextStorage.IsOrgAdmin;
        var classes = await _classBusiness.GetAllClassesPaginated(
            currentUserId, organizationId, projects, paginatedRequestDto, hideArchived, isSysAdmin, isOrgAdmin);
        return Ok(classes);
    }



    /// <summary>
    ///     Get a Class
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// <param name="classId">The ID of the class to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived classes from the result (Default true)</param>
    /// <returns>Class response DTO</returns>
    [HttpGet("{classId:long}", Name = "api_get_a_class_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "class")]
    public async Task<ActionResult<ClassResponseDto>> GetClass(
        long organizationId,
        long classId,
        [FromQuery] bool hideArchived = true)
    {
        var classes = await _classBusiness.GetClass(
            organizationId, null, classId, hideArchived);
        return Ok(classes);
    }



    /// <summary>
    ///     Create a Class
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// <param name="dto">The request DTO for classes</param>
    /// <returns>Class response DTOs</returns>
    [HttpPost(Name = "api_create_a_class_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "class")]
    public async Task<ActionResult<ClassResponseDto>> CreateClass(
        long organizationId,
        [FromBody] CreateClassRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var newClass = await _classBusiness.CreateClass(
            currentUserId, organizationId, null, dto);
        return Ok(newClass);
    }



    /// <summary>
    ///     Bulk Create Classes
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// <param name="classes">List of request DTOs for classes</param>
    /// <returns>Bulk class response DTOs</returns>
    [HttpPost("bulk", Name = "api_create_many_classes_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "class")]
    public async Task<ActionResult<List<ClassResponseDto>>> BulkCreateClasses(
        long organizationId,
        [FromBody] List<CreateClassRequestDto> classes)
    {
        var currentUserId = UserContextStorage.UserId;
        var newClasses = await _classBusiness.BulkCreateClasses(
            currentUserId, organizationId, null, classes);
        return Ok(newClasses);
    }



    /// <summary>
    ///     Update a Class
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// ///
    /// <param name="classId">The ID of the class to update</param>
    /// <param name="dto">The request DTO for the class</param>
    /// <returns>Class response DTO</returns>
    [HttpPut("{classId:long}", Name = "api_update_a_class_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "class")]
    public async Task<ActionResult<ClassResponseDto>> UpdateClass(
        long organizationId,
        long classId,
        [FromBody] UpdateClassRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var updatedClass = await _classBusiness.UpdateClass(
            currentUserId, organizationId, null, classId, dto);
        return Ok(updatedClass);
    }



    /// <summary>
    ///     Delete a Class
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// <param name="classId">The ID of the class to delete.</param>
    /// <returns>True if the class was successfully deleted.</returns>
    [HttpDelete("{classId:long}", Name = "api_delete_a_class_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "class")]
    public async Task<IActionResult> DeleteClass(
        long organizationId,
        long classId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _classBusiness.DeleteClass(
            currentUserId, organizationId, null, classId);
        return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive a Class
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// <param name="classId">The ID of the class to archive or unarchive.</param>
    /// <param name="archive">True to archive the class, false to unarchive it.</param>
    /// <returns>True if the class was successfully archived or unarchived.</returns>
    [HttpPatch("{classId:long}", Name = "api_archive_class_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "class")]
    public async Task<IActionResult> ArchiveClass(
        long organizationId,
        long classId,
        [FromQuery] bool archive)
    {
        var userId = UserContextStorage.UserId;
        if (archive)
        {
            var responseA = await _classBusiness.ArchiveClass(userId, organizationId, null, classId);
            return Ok(responseA);
        }

        var responseB = await _classBusiness.UnarchiveClass(userId, organizationId, null, classId);
        return Ok(responseB);
    }
}