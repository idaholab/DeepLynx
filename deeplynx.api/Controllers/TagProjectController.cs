using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

[Route("projects/{projectId:long}/tags")]
[ApiController]
[Authorize]
[Tags("Project - Tag")]
public class TagProjectController : ControllerBase
{
    private readonly ILogger<TagProjectController> _logger;
    private readonly ITagBusiness _tagBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TagProjectController" /> class.
    /// </summary>
    /// <param name="tagBusiness">The business logic interface for handling tag operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public TagProjectController(ITagBusiness tagBusiness, ILogger<TagProjectController> logger)
    {
        _tagBusiness = tagBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get all Tags 
    /// </summary>
    /// <param name="projectId">The ID of the project whose tags are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result (Default true)</param>
    /// <returns>A list of tags belonging to the project.</returns>
    [HttpGet(Name = "api_get_all_tags_project")]
    [Auth("read", "tag")]
    public async Task<ActionResult<IEnumerable<TagResponseDto>>> GetAllTags(
        long projectId, [FromQuery] bool hideArchived = true)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var tags = await _tagBusiness.GetAllTags(organizationId, [projectId], hideArchived);
            return Ok(tags);
        }
        catch (Exception exception)
        {
            var message = $"An error occurred while listing all tags: {exception}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get a tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to retrieve.</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result (Default true)</param>
    /// <returns>The tag with its details.</returns>
    [HttpGet("{tagId:long}", Name = "api_get_a_tag_project")]
    [Auth("read", "tag")]
    public async Task<ActionResult<TagResponseDto>> GetTag(
        long projectId,
        long tagId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var tag = await _tagBusiness.GetTag(organizationId, projectId, tagId, hideArchived);
            return Ok(tag);
        }
        catch (Exception exception)
        {
            var message = $"An error occurred while retrieving tag {tagId}: {exception}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Creates a tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagRequestDto">The tag data transfer object containing tag details.</param>
    /// <returns>The created tag with its details.</returns>
    [HttpPost(Name = "api_create_a_tag_project")]
    [Auth("write", "tag")]
    public async Task<ActionResult<TagResponseDto>> CreateTag(
        long projectId,
        [FromBody] CreateTagRequestDto tagRequestDto)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var createdTag = await _tagBusiness.CreateTag(organizationId,currentUserId, projectId, tagRequestDto);
            return Ok(createdTag);
        }
        catch (Exception exception)
        {
            var message = $"An error occurred while creating tag: {exception}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create Many Tags 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagRequestDto">The tag data transfer object containing tag details.</param>
    /// <returns>The created tag with its details.</returns>
    [HttpPost("bulk", Name = "api_create_many_tags_project")]
    [Auth("write", "tag")]
    public async Task<ActionResult<List<TagResponseDto>>> BulkCreateTag(
        long projectId,
        [FromBody] List<CreateTagRequestDto> tagRequestDto)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var bulkTagResponseDto = await _tagBusiness.BulkCreateTags(organizationId, currentUserId, projectId, tagRequestDto);
            return Ok(bulkTagResponseDto);
        }
        catch (Exception exception)
        {
            var message = $"An unexpected error occurred while creating these tags: {exception}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    /// Update a Tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to update.</param>
    /// <param name="tagRequestDto">The tag data transfer object containing updated tag details.</param>
    /// <returns>The updated tag with its details.</returns>
    [HttpPut("{tagId:long}", Name = "api_update_a_tag_project")]
    [Auth("write", "tag")]
    public async Task<ActionResult<TagResponseDto>> UpdateTag(
        long projectId, long tagId,
        [FromBody] UpdateTagRequestDto tagRequestDto)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var updatedTag = await _tagBusiness.UpdateTag(organizationId, currentUserId, projectId, tagId, tagRequestDto);
            return Ok(updatedTag);
        }
        catch (Exception exception)
        {
            var message = $"An error occurred while updating tag {tagId}: {exception}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete a Tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to delete.</param>
    /// <returns> A message stating the tag was successfully deleted.</returns>
    [HttpDelete("{tagId:long}", Name = "api_delete_a_tag_project")]
    [Auth("write", "tag")]
    public async Task<IActionResult> DeleteTag(
        long projectId, long tagId)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            await _tagBusiness.DeleteTag(organizationId, projectId, tagId);
            return Ok(new { message = "Tag deleted successfully" });
        }
        catch (Exception exception)
        {
            var message = $"An error occurred while deleting tag {tagId}: {exception}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive a Tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to archive or unarchive.</param>
    /// <param name="archive">True to archive the tag, false to unarchive it.</param>
    /// <returns>A message stating the tag was successfully archived or unarchived.</returns>
    [HttpPatch("{tagId:long}", Name = "api_archive_tag_project")]
    [Auth("write", "tag")]
    public async Task<IActionResult> ArchiveTag(
        long projectId,
        long tagId,
        [FromQuery] bool archive)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var userId = UserContextStorage.UserId;
            if (archive)
            {
                await _tagBusiness.ArchiveTag(organizationId, userId, projectId, tagId);
                return Ok(new { message = $"Archived tag {tagId}" });
            }

            await _tagBusiness.UnarchiveTag(organizationId, userId, projectId, tagId);
            return Ok(new { message = $"Unarchived tag {tagId}" });
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} tag {tagId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}