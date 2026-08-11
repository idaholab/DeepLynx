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
///     Controller for managing classes at a project level.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve class information.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("projects/{projectId:long}/classes")]
[Authorize]
[Tags("Project - Class")]
public class ClassProjectController : ControllerBase
{
    private readonly IClassBusiness _classBusiness;
    private readonly ILogger<ClassProjectController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClassProjectController" /> class
    /// </summary>
    /// <param name="classBusiness">The business logic interface for handling class operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public ClassProjectController(IClassBusiness classBusiness, ILogger<ClassProjectController> logger)
    {
        _classBusiness = classBusiness;
        _logger = logger;
    }



    /// <summary>
    ///     Get All Classes
    /// </summary>
    /// <param name="projectId">The ID of the project to which the class belongs</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived classes from the result (Default true)</param>
    /// /// <param name="paginatedRequestDto"> Pagination parameters</param>
    /// <returns>Paginated list of class response DTOs</returns>
    [HttpGet(Name = "api_get_all_classes_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "class")]
    public async Task<ActionResult<PaginatedResponse<ClassResponseDto>>> GetAllClasses(
        long projectId,
        [FromQuery] bool hideArchived = true,
        PaginatedRequestDto? paginatedRequestDto = null)
    {
        paginatedRequestDto ??= new PaginatedRequestDto();
        var currentUserId = UserContextStorage.UserId;
        var organizationId = UserContextStorage.OrganizationId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var isOrgAdmin = UserContextStorage.IsOrgAdmin;
        var classes = await _classBusiness.GetAllClassesPaginated(
            currentUserId, organizationId, [projectId], paginatedRequestDto, hideArchived, isSysAdmin, isOrgAdmin);
        return Ok(classes);
    }



    /// <summary>
    ///     Get a Class
    /// </summary>
    /// <param name="projectId">The ID of the project to which the class belongs</param>
    /// <param name="classId">The ID of the class to retrieve</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived classes from the result (Default true)</param>
    /// <returns>Class response DTO</returns>
    [HttpGet("{classId:long}", Name = "api_get_a_class_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "class")]
    public async Task<ActionResult<ClassResponseDto>> GetClass(
        long projectId,
        long classId,
        [FromQuery] bool hideArchived = true)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var classes = await _classBusiness.GetClass(
            organizationId, projectId, classId, hideArchived);
        return Ok(classes);
    }



    /// <summary>
    ///     Create a Class
    /// </summary>
    /// <param name="projectId">The ID of the project to which the class belongs</param>
    /// <param name="dto">The request DTO for classes</param>
    /// <returns>Class response DTOs</returns>
    [HttpPost(Name = "api_create_a_class_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "class")]
    public async Task<ActionResult<ClassResponseDto>> CreateClass(
        long projectId,
        [FromBody] CreateClassRequestDto dto)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var currentUserId = UserContextStorage.UserId;
        var newClass = await _classBusiness.CreateClass(
            currentUserId, organizationId, projectId, dto);
        return Ok(newClass);
    }



    /// <summary>
    ///     Bulk Create Classes
    /// </summary>
    /// <param name="projectId">The ID of the project to which the class belongs</param>
    /// <param name="classes">List of request DTOs for classes</param>
    /// <returns>Bulk class response DTOs</returns>
    [HttpPost("bulk", Name = "api_create_many_classes_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "class")]
    public async Task<ActionResult<List<ClassResponseDto>>> BulkCreateClasses(
        long projectId,
        [FromBody] List<CreateClassRequestDto> classes)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var currentUserId = UserContextStorage.UserId;
        var newClasses = await _classBusiness.BulkCreateClasses(
            currentUserId, organizationId, projectId, classes);
        return Ok(newClasses);
    }



    /// <summary>
    ///     Update a Class
    /// </summary>
    /// <param name="projectId">The ID of the project to which the class belongs</param>
    /// ///
    /// <param name="classId">The ID of the class to update</param>
    /// <param name="dto">The request DTO for the class</param>
    /// <returns>Class response DTO</returns>
    [HttpPut("{classId:long}", Name = "api_update_a_class_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "class")]
    public async Task<ActionResult<ClassResponseDto>> UpdateClass(
        long projectId,
        long classId,
        [FromBody] UpdateClassRequestDto dto)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var currentUserId = UserContextStorage.UserId;
        var updatedClass = await _classBusiness.UpdateClass(
            currentUserId, organizationId, projectId, classId, dto);
        return Ok(updatedClass);
    }



    /// <summary>
    ///     Delete a Class
    /// </summary>
    /// <param name="projectId">The ID of the project to which the class belongs</param>
    /// <param name="classId">The ID of the class to delete.</param>
    /// <returns>True if the class was successfully deleted.</returns>
    [HttpDelete("{classId:long}", Name = "api_delete_a_class_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "class")]
    public async Task<IActionResult> DeleteClass(
        long projectId,
        long classId)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var currentUserId = UserContextStorage.UserId;
        var response = await _classBusiness.DeleteClass(
            currentUserId, organizationId, projectId, classId);
        return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive a Class
    /// </summary>
    /// <param name="projectId">The ID of the project to which the class belongs</param>
    /// <param name="classId">The ID of the class to archive or unarchive.</param>
    /// <param name="archive">True to archive the class, false to unarchive it.</param>
    /// <returns>True if the class was successfully archived or unarchived.</returns>
    [HttpPatch("{classId:long}", Name = "api_archive_class_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "class")]
    public async Task<IActionResult> ArchiveClass(
        long projectId,
        long classId,
        [FromQuery] bool archive)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var userId = UserContextStorage.UserId;
        if (archive)
        {
            var responseA = await _classBusiness.ArchiveClass(userId, organizationId, projectId, classId);
            return Ok(responseA);
        }

        var responseB = await _classBusiness.UnarchiveClass(userId, organizationId, projectId, classId);
        return Ok(responseB);
    }
}