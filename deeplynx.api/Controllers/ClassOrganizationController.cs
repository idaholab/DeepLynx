using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing classes at the organization level.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve class information.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/classes")]
[Authorize]
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
    /// <param name="projectIds">(Optional)An array of project IDs within the organization to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived classes from the result (Default true)</param>
    /// <returns>List of class response DTOs</returns>
    [HttpGet(Name = "api_get_all_classes_organization")]
    [Auth("read", "class")]
    public async Task<ActionResult<IEnumerable<ClassResponseDto>>> GetAllClasses(
        long organizationId,
        [FromQuery] long[]? projectIds,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var classes = await _classBusiness.GetAllClasses(
                organizationId, projectIds, hideArchived);
            return Ok(classes);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching all classes.: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get a Class 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// <param name="classId">The ID of the class to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived classes from the result (Default true)</param>
    /// <returns>Class response DTO</returns>
    [HttpGet("{classId:long}", Name = "api_get_a_class_organization")]
    [Auth("read", "class")]
    public async Task<ActionResult<ClassResponseDto>> GetClass(
        long organizationId,
        long classId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var classes = await _classBusiness.GetClass(
                organizationId, null, classId, hideArchived);
            return Ok(classes);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching this class {classId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create a Class 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// <param name="dto">The request DTO for classes</param>
    /// <returns>Class response DTOs</returns>
    [HttpPost(Name = "api_create_a_class_organization")]
    [Auth("write", "class")]
    public async Task<ActionResult<ClassResponseDto>> CreateClass(
        long organizationId,
        [FromBody] CreateClassRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var newClass = await _classBusiness.CreateClass(
                currentUserId, organizationId, null, dto);
            return Ok(newClass);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while creating this class.: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Bulk Create Classes 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// <param name="classes">List of request DTOs for classes</param>
    /// <returns>Bulk class response DTOs</returns>
    [HttpPost("bulk", Name = "api_create_many_classes_organization")]
    [Auth("write", "class")]
    public async Task<ActionResult<List<ClassResponseDto>>> BulkCreateClasses(
        long organizationId,
        [FromBody] List<CreateClassRequestDto> classes)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var newClasses = await _classBusiness.BulkCreateClasses(
                currentUserId, organizationId, null, classes);
            return Ok(newClasses);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while creating these classes: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
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
    [Auth("write", "class")]
    public async Task<ActionResult<ClassResponseDto>> UpdateClass(
        long organizationId,
        long classId,
        [FromBody] UpdateClassRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var updatedClass = await _classBusiness.UpdateClass(
                currentUserId, organizationId, null, classId, dto);
            return Ok(updatedClass);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while updating this class {classId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete a Class 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// <param name="classId">The ID of the class to delete.</param>
    /// <returns>A message stating the class was successfully deleted.</returns>
    [HttpDelete("{classId:long}", Name = "api_delete_a_class_organization")]
    [Auth("write", "class")]
    public async Task<IActionResult> DeleteClass(
        long organizationId,
        long classId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _classBusiness.DeleteClass(
                currentUserId, organizationId, null, classId);
            return Ok(new { message = $"Deleted class {classId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting class {classId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive a Class 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the class's project belongs</param>
    /// <param name="classId">The ID of the class to archive or unarchive.</param>
    /// <param name="archive">True to archive the class, false to unarchive it.</param>
    /// <returns>A message stating the class was successfully archived or unarchived.</returns>
    [HttpPatch("{classId:long}", Name = "api_archive_class_organization")]
    [Auth("write", "class")]
    public async Task<IActionResult> ArchiveClass(
        long organizationId,
        long classId,
        [FromQuery] bool archive)
    {
        try
        {
            var userId = UserContextStorage.UserId;
            if (archive)
            {
                await _classBusiness.ArchiveClass(userId, organizationId, null, classId);
                return Ok(new { message = $"Archived class {classId}" });
            }

            await _classBusiness.UnarchiveClass(userId, organizationId, null, classId);
            return Ok(new { message = $"Unarchived class {classId}" });
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} class {classId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}