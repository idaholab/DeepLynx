using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using Asp.Versioning;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers;

[Route("projects/{projectId:long}/tags")]
[ApiController]
[ApiVersion(1)]
[ApiVersion(2)]
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
    [MapToApiVersion(1)]
    [Auth("read", "tag")]
    public async Task<ActionResult<IEnumerable<TagResponseDto>>> GetAllTags(
        long projectId, [FromQuery] bool hideArchived = true)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var organizationId = UserContextStorage.OrganizationId;
            var tags = await _tagBusiness.GetAllTags(currentUserId, organizationId, [projectId], hideArchived);
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
    ///     Get all Tags 
    /// </summary>
    /// <param name="projectId">The ID of the project whose tags are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result (Default true)</param>
    /// <returns>A list of tags belonging to the project.</returns>
    [HttpGet(Name = "api_get_all_tags_project")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "tag")]
    public async Task<ActionResult<IEnumerable<TagResponseDto>>> GetAllTagsV2(
        long projectId, [FromQuery] bool hideArchived = true)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var currentUserId = UserContextStorage.UserId;
        var tags = await _tagBusiness.GetAllTags(currentUserId, organizationId, [projectId], hideArchived);
        return Ok(tags);
    }

    /// <summary>
    ///     Get Tags By Name
    /// </summary>
    /// <param name="projectId">The ID of the project whose tags are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result (Default true)</param>
    /// <returns>A list of tags belonging to the project.</returns>
    [HttpPost("by-name", Name = "api_get_tags_by_name_project")]
    [MapToApiVersion(1)]
    [Auth("read", "tag")]
    public async Task<ActionResult<IEnumerable<TagResponseDto>>> GetTagsByName(
        long projectId,
        [FromBody] List<string> tagNames,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var tags = await _tagBusiness.GetTagsByName(organizationId, projectId, tagNames, hideArchived);
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
    ///     Get Tags By Name
    /// </summary>
    /// <param name="projectId">The ID of the project whose tags are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result (Default true)</param>
    /// <returns>A list of tags belonging to the project.</returns>
    [HttpPost("by-name", Name = "api_get_tags_by_name_project")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "tag")]
    public async Task<ActionResult<IEnumerable<TagResponseDto>>> GetTagsByNameV2(
        long projectId,
        [FromBody] List<string> tagNames,
        [FromQuery] bool hideArchived = true)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var tags = await _tagBusiness.GetTagsByName(organizationId, projectId, tagNames, hideArchived);
        return Ok(tags);
    }

    /// <summary>
    ///     Get a tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to retrieve.</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result (Default true)</param>
    /// <returns>The tag with its details.</returns>
    [HttpGet("{tagId:long}", Name = "api_get_a_tag_project")]
    [MapToApiVersion(1)]
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
    ///     Get a tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to retrieve.</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived tags from the result (Default true)</param>
    /// <returns>The tag with its details.</returns>
    [HttpGet("{tagId:long}", Name = "api_get_a_tag_project")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "tag")]
    public async Task<ActionResult<TagResponseDto>> GetTagV2(
        long projectId,
        long tagId,
        [FromQuery] bool hideArchived = true)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var tag = await _tagBusiness.GetTag(organizationId, projectId, tagId, hideArchived);
        return Ok(tag);
    }

    /// <summary>
    ///     Creates a tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagRequestDto">The tag data transfer object containing tag details.</param>
    /// <returns>The created tag with its details.</returns>
    [HttpPost(Name = "api_create_a_tag_project")]
    [MapToApiVersion(1)]
    [Auth("write", "tag")]
    public async Task<ActionResult<TagResponseDto>> CreateTag(
        long projectId,
        [FromBody] CreateTagRequestDto tagRequestDto)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var createdTag = await _tagBusiness.CreateTag(organizationId, currentUserId, projectId, tagRequestDto);
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
    ///     Creates a tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagRequestDto">The tag data transfer object containing tag details.</param>
    /// <returns>The created tag with its details.</returns>
    [HttpPost(Name = "api_create_a_tag_project")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "tag")]
    public async Task<ActionResult<TagResponseDto>> CreateTagV2(
        long projectId,
        [FromBody] CreateTagRequestDto tagRequestDto)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var currentUserId = UserContextStorage.UserId;
        var createdTag = await _tagBusiness.CreateTag(organizationId, currentUserId, projectId, tagRequestDto);
        return Ok(createdTag);
    }

    /// <summary>
    ///     Create Many Tags 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagRequestDto">The tag data transfer object containing tag details.</param>
    /// <returns>The created tag with its details.</returns>
    [HttpPost("bulk", Name = "api_create_many_tags_project")]
    [MapToApiVersion(1)]
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
    ///     Create Many Tags 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagRequestDto">The tag data transfer object containing tag details.</param>
    /// <returns>The created tags with their details.</returns>
    [HttpPost("bulk", Name = "api_create_many_tags_project")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "tag")]
    public async Task<ActionResult<List<TagResponseDto>>> BulkCreateTagV2(
        long projectId,
        [FromBody] List<CreateTagRequestDto> tagRequestDto)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var currentUserId = UserContextStorage.UserId;
        var bulkTagResponseDto = await _tagBusiness.BulkCreateTags(organizationId, currentUserId, projectId, tagRequestDto);
        return Ok(bulkTagResponseDto);
    }

    /// <summary>
    /// Update a Tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to update.</param>
    /// <param name="tagRequestDto">The tag data transfer object containing updated tag details.</param>
    /// <returns>The updated tag with its details.</returns>
    [HttpPut("{tagId:long}", Name = "api_update_a_tag_project")]
    [MapToApiVersion(1)]
    [Auth("update", "tag")]
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
    /// Update a Tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to update.</param>
    /// <param name="tagRequestDto">The tag data transfer object containing updated tag details.</param>
    /// <returns>The updated tag with its details.</returns>
    [HttpPut("{tagId:long}", Name = "api_update_a_tag_project")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "tag")]
    public async Task<ActionResult<TagResponseDto>> UpdateTagV2(
        long projectId, long tagId,
        [FromBody] UpdateTagRequestDto tagRequestDto)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var currentUserId = UserContextStorage.UserId;
        var updatedTag = await _tagBusiness.UpdateTag(organizationId, currentUserId, projectId, tagId, tagRequestDto);
        return Ok(updatedTag);
    }

    /// <summary>
    ///     Delete a Tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to delete.</param>
    /// <returns> A message stating the tag was successfully deleted.</returns>
    [HttpDelete("{tagId:long}", Name = "api_delete_a_tag_project")]
    [MapToApiVersion(1)]
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
    ///     Delete a Tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to delete.</param>
    /// <returns>True if the tag was successfully deleted.</returns>
    [HttpDelete("{tagId:long}", Name = "api_delete_a_tag_project")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "tag")]
    public async Task<ActionResult<bool>> DeleteTagV2(
        long projectId, long tagId)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var response = await _tagBusiness.DeleteTag(organizationId, projectId, tagId);
        return Ok(response);
    }

    /// <summary>
    ///     Archive or Unarchive a Tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to archive or unarchive.</param>
    /// <param name="archive">True to archive the tag, false to unarchive it.</param>
    /// <returns>A message stating the tag was successfully archived or unarchived.</returns>
    [HttpPatch("{tagId:long}", Name = "api_archive_tag_project")]
    [MapToApiVersion(1)]
    [Auth("update", "tag")]
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

    /// <summary>
    ///     Archive or Unarchive a Tag 
    /// </summary>
    /// <param name="projectId">The ID of the project to which the tag belongs</param>
    /// <param name="tagId">The ID of the tag to archive or unarchive.</param>
    /// <param name="archive">True to archive the tag, false to unarchive it.</param>
    /// <returns>True if the tag was successfully archived or unarchived.</returns>
    [HttpPatch("{tagId:long}", Name = "api_archive_tag_project")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "tag")]
    public async Task<ActionResult<bool>> ArchiveTagV2(
        long projectId,
        long tagId,
        [FromQuery] bool archive)
    {
        var organizationId = UserContextStorage.OrganizationId;
        var userId = UserContextStorage.UserId;
        if (archive)
        {
            var responseA = await _tagBusiness.ArchiveTag(organizationId, userId, projectId, tagId);
            return Ok(responseA);
        }

        var responseB = await _tagBusiness.UnarchiveTag(organizationId, userId, projectId, tagId);
        return Ok(responseB);
    }
}