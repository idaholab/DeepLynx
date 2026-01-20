using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

[Route("organizations/{organizationId:long}/tags")]
[ApiController]
[Authorize]
[Tags("Organization - Tag")]
public class TagOrganizationController : ControllerBase
{
    private readonly ITagBusiness _tagBusiness;
    private readonly ILogger<TagProjectController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TagOrganizationController" /> class.
    /// </summary>
    /// <param name="tagBusiness">The business logic interface for handling tag operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public TagOrganizationController(ITagBusiness tagBusiness, ILogger<TagProjectController> logger)
    {
        _tagBusiness = tagBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Tags 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectIds">(Optional)An array of project IDs within the organization to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result (Default true)</param>
    /// <returns>A list of tags belonging to the project.</returns>
    [HttpGet(Name = "api_get_all_tags_organization")]
    [Auth("read", "tag")]
    public async Task<ActionResult<IEnumerable<TagResponseDto>>> GetAllTags(
        long organizationId, [FromQuery] long[]? projectIds, [FromQuery] bool hideArchived = true)
    {
        try
        {
            var tags = await _tagBusiness.GetAllTags(organizationId, projectIds, hideArchived);
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
    ///     Get a Tag 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="tagId">The ID of the tag to retrieve.</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result (Default true)</param>
    /// <returns>The tag with its details.</returns>
    [HttpGet("{tagId:long}", Name = "api_get_a_tag_organization")]
    [Auth("read", "tag")]
    public async Task<ActionResult<TagResponseDto>> GetTag(
        long organizationId,
        long tagId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var tag = await _tagBusiness.GetTag(organizationId, null, tagId, hideArchived);
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
    ///     Create a Tag 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="tagRequestDto">The tag data transfer object containing tag details.</param>
    /// <returns>The created tag with its details.</returns>
    [HttpPost(Name = "api_create_a_tag_organization")]
    [Auth("write", "tag")]
    public async Task<ActionResult<TagResponseDto>> CreateTag(
        long organizationId,
        [FromBody] CreateTagRequestDto tagRequestDto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var createdTag = await _tagBusiness.CreateTag(organizationId, currentUserId, null, tagRequestDto);
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
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="tagRequestDto">The tag data transfer object containing tag details.</param>
    /// <returns>The created tag with its details.</returns>
    [HttpPost("bulk", Name = "api_create_many_tags_organization")]
    [Auth("write", "tag")]
    public async Task<ActionResult<List<TagResponseDto>>> BulkCreateTag(
        long organizationId,
        [FromBody] List<CreateTagRequestDto> tagRequestDto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var bulkTagResponseDto = await _tagBusiness.BulkCreateTags(organizationId, currentUserId, null, tagRequestDto);
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
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="tagId">The ID of the tag to update.</param>
    /// <param name="tagRequestDto">The tag data transfer object containing updated tag details.</param>
    /// <returns>The updated tag with its details.</returns>
    [HttpPut("{tagId:long}", Name = "api_update_a_tag_organization")]
    [Auth("write", "tag")]
    public async Task<ActionResult<TagResponseDto>> UpdateTag(
        long organizationId, long tagId,
        [FromBody] UpdateTagRequestDto tagRequestDto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var updatedTag = await _tagBusiness.UpdateTag(organizationId, currentUserId, null, tagId, tagRequestDto);
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
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="tagId">The ID of the tag to delete.</param>
    /// <returns> A message stating the tag was successfully deleted.</returns>
    [HttpDelete("{tagId:long}", Name = "api_delete_a_tag_organization")]
    [Auth("write", "tag")]
    public async Task<IActionResult> DeleteTag(
        long organizationId, long tagId)
    {
        try
        {
            await _tagBusiness.DeleteTag(organizationId, null, tagId);
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
    /// <param name="organizationId">The ID of the organization to which the tag's project belongs</param>
    /// <param name="tagId">The ID of the tag to archive or unarchive.</param>
    /// <param name="archive">True to archive the tag, false to unarchive it.</param>
    /// <returns>A message stating the tag was successfully archived or unarchived.</returns>
    [HttpPatch("{tagId:long}", Name = "api_archive_tag_organization")]
    [Auth("write", "tag")]
    public async Task<IActionResult> ArchiveTag(
        long organizationId,
        long tagId,
        [FromQuery] bool archive)
    {
        try
        {
            var userId = UserContextStorage.UserId;
            if (archive)
            {
                await _tagBusiness.ArchiveTag(organizationId, userId, null, tagId);
                return Ok(new { message = $"Archived tag {tagId}" });
            }

            await _tagBusiness.UnarchiveTag(organizationId, userId, null, tagId);
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