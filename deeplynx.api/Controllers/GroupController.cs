using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers;

[ApiController]
[ApiVersion(1)]
[ApiVersion(2)]
[Route("organizations/{organizationId:long}/groups")]
[Authorize]
[ForbidServiceAccounts] // Service accounts can only act on the project level
public class GroupController : ControllerBase
{
    private readonly IGroupBusiness _groupBusiness;
    private readonly ILogger<GroupController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GroupController" /> class
    /// </summary>
    /// <param name="groupBusiness">The business logic interface for handling Group operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public GroupController(IGroupBusiness groupBusiness, ILogger<GroupController> logger)
    {
        _groupBusiness = groupBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Groups Within an Organization
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the groups belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived groups</param>
    /// <returns>A list of groups in the organization.</returns>
    [HttpGet(Name = "api_get_all_groups")]
    [MapToApiVersion(1)]
    [Auth("read", "group")]
    public async Task<ActionResult<IEnumerable<GroupResponseDto>>> GetAllGroups(
        long organizationId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var groups = await _groupBusiness.GetAllGroups(organizationId, hideArchived);
            return Ok(groups);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing groups: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get All Groups Within an Organization
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the groups belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived groups</param>
    /// <returns>A list of groups in the organization.</returns>
    [HttpGet(Name = "api_get_all_groups")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "group")]
    public async Task<ActionResult<IEnumerable<GroupResponseDto>>> GetAllGroupsV2(
        long organizationId,
        [FromQuery] bool hideArchived = true)
    {
        var groups = await _groupBusiness.GetAllGroups(organizationId, hideArchived);
        return Ok(groups);
    }

    /// <summary>
    ///     Fetch Group by ID
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of group</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived groups</param>
    /// <returns>The requested group.</returns>
    [HttpGet("{groupId:long}", Name = "api_get_group")]
    [MapToApiVersion(1)]
    [Auth("read", "group")]
    public async Task<ActionResult<GroupResponseDto>> GetGroup(
        long organizationId,
        long groupId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var group = await _groupBusiness.GetGroup(organizationId, groupId, hideArchived);
            return Ok(group);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving group {groupId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Fetch Group by ID
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of group</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived groups</param>
    /// <returns>The requested group.</returns>
    [HttpGet("{groupId:long}", Name = "api_get_group")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "group")]
    public async Task<ActionResult<GroupResponseDto>> GetGroupV2(
        long organizationId,
        long groupId,
        [FromQuery] bool hideArchived = true)
    {
        var group = await _groupBusiness.GetGroup(organizationId, groupId, hideArchived);
        return Ok(group);
    }

    /// <summary>
    ///     Get All Members of a Group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of the group</param>
    /// <returns>List of users in the group</returns>
    [HttpGet("{groupId:long}/users", Name = "api_get_group_members")]
    [MapToApiVersion(1)]
    [Auth("read", "group")]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetGroupMembers(
        long organizationId,
        long groupId)
    {
        try
        {
            var members = await _groupBusiness.GetGroupMembers(organizationId, groupId);
            return Ok(members);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving members for group {groupId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get All Members of a Group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of the group</param>
    /// <returns>A list of users in the group.</returns>
    [HttpGet("{groupId:long}/users", Name = "api_get_group_members")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "group")]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetGroupMembersV2(
        long organizationId,
        long groupId)
    {
        var members = await _groupBusiness.GetGroupMembers(organizationId, groupId);
        return Ok(members);
    }

    /// <summary>
    ///     Create a Group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="dto">Data structure of group to create</param>
    /// <returns>The newly created group.</returns>
    [HttpPost(Name = "api_create_group")]
    [MapToApiVersion(1)]
    [Auth("write", "group")]
    public async Task<ActionResult<GroupResponseDto>> CreateGroup(
        long organizationId,
        [FromBody] CreateGroupRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var group = await _groupBusiness.CreateGroup(currentUserId, organizationId, dto);
            return Ok(group);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating group: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create a Group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="dto">Data structure of group to create</param>
    /// <returns>The newly created group.</returns>
    [HttpPost(Name = "api_create_group")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "group")]
    public async Task<ActionResult<GroupResponseDto>> CreateGroupV2(
        long organizationId,
        [FromBody] CreateGroupRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var group = await _groupBusiness.CreateGroup(currentUserId, organizationId, dto);
        return Ok(group);
    }

    /// <summary>
    ///     Update a Group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of the group</param>
    /// <param name="dto">Fields to update</param>
    /// <returns>The updated group.</returns>
    [HttpPut("{groupId:long}", Name = "api_update_group")]
    [MapToApiVersion(1)]
    [Auth("update", "group")]
    public async Task<ActionResult<GroupResponseDto>> UpdateGroup(
        long organizationId,
        long groupId,
        [FromBody] UpdateGroupRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var group = await _groupBusiness.UpdateGroup(currentUserId, organizationId, groupId, dto);
            return Ok(group);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating group {groupId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Update a Group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of the group</param>
    /// <param name="dto">Fields to update</param>
    /// <returns>The updated group.</returns>
    [HttpPut("{groupId:long}", Name = "api_update_group")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "group")]
    public async Task<ActionResult<GroupResponseDto>> UpdateGroupV2(
        long organizationId,
        long groupId,
        [FromBody] UpdateGroupRequestDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var group = await _groupBusiness.UpdateGroup(currentUserId, organizationId, groupId, dto);
        return Ok(group);
    }

    /// <summary>
    ///     Delete a group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of the group to hard delete</param>
    /// <returns>A message confirming the group was deleted.</returns>
    [HttpDelete("{groupId:long}", Name = "api_delete_group")]
    [MapToApiVersion(1)]
    [Auth("write", "group")]
    public async Task<ActionResult> DeleteGroup(
        long organizationId,
        long groupId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _groupBusiness.DeleteGroup(currentUserId, organizationId, groupId);
            return Ok(new { message = $"Deleted group {groupId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting group {groupId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete a Group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of the group to hard delete</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpDelete("{groupId:long}", Name = "api_delete_group")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "group")]
    public async Task<ActionResult> DeleteGroupV2(
        long organizationId,
        long groupId)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _groupBusiness.DeleteGroup(currentUserId, organizationId, groupId);
        return Ok(response);
    }

    /// <summary>
    ///     Archive or Unarchive a Group
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the group belongs</param>
    /// <param name="groupId">The ID of the group to archive or unarchive.</param>
    /// <param name="archive">True to archive the group, false to unarchive it.</param>
    /// <returns>A message stating the group was successfully archived or unarchived.</returns>
    [HttpPatch("{groupId:long}", Name = "api_archive_group")]
    [MapToApiVersion(1)]
    [Auth("update", "group")]
    public async Task<IActionResult> ArchiveGroup(
        long organizationId,
        long groupId,
        [FromQuery] bool archive)
    {
        try
        {
            var userId = UserContextStorage.UserId;
            if (archive)
            {
                await _groupBusiness.ArchiveGroup(userId, organizationId, groupId);
                return Ok(new { message = $"Archived group {groupId}" });
            }

            await _groupBusiness.UnarchiveGroup(userId, organizationId, groupId);
            return Ok(new { message = $"Unarchived group {groupId}" });
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} group {groupId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive a Group
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the group belongs</param>
    /// <param name="groupId">The ID of the group to archive or unarchive.</param>
    /// <param name="archive">True to archive the group, false to unarchive it.</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpPatch("{groupId:long}", Name = "api_archive_group")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "group")]
    public async Task<IActionResult> ArchiveGroupV2(
        long organizationId,
        long groupId,
        [FromQuery] bool archive)
    {
        var userId = UserContextStorage.UserId;
        if (archive)
        {
            var archiveResponse = await _groupBusiness.ArchiveGroup(userId, organizationId, groupId);
            return Ok(archiveResponse);
        }

        var response = await _groupBusiness.UnarchiveGroup(userId, organizationId, groupId);
        return Ok(response);
    }

    /// <summary>
    ///     Add User to Group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of the group</param>
    /// <param name="userId">ID of the user to be added</param>
    /// <returns>A message confirming the user was added to the group.</returns>
    [HttpPost("{groupId:long}/users", Name = "api_add_user_to_group")]
    [MapToApiVersion(1)]
    [Auth("update", "group")]
    public async Task<ActionResult> AddUserToGroup(
        long organizationId,
        long groupId,
        [FromQuery] long userId)
    {
        try
        {
            await _groupBusiness.AddUserToGroup(userId, organizationId, groupId);
            return Ok(new { message = $"Added user {userId} to group {groupId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while adding user {userId} to group {groupId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Add User to Group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of the group</param>
    /// <param name="userId">ID of the user to be added</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpPost("{groupId:long}/users", Name = "api_add_user_to_group")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "group")]
    public async Task<ActionResult> AddUserToGroupV2(
        long organizationId,
        long groupId,
        [FromQuery] long userId)
    {
        var response = await _groupBusiness.AddUserToGroup(userId, organizationId, groupId);
        return Ok(response);
    }


    /// <summary>
    ///     Remove User from Group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of the group to remove from</param>
    /// <param name="userId">ID of user to be removed</param>
    /// <returns>A message confirming the user was removed from the group.</returns>
    [HttpDelete("{groupId:long}/users/{userId:long}", Name = "api_remove_user_from_group")]
    [MapToApiVersion(1)]
    [Auth("update", "group")]
    public async Task<ActionResult> RemoveUserFromGroup(
        long organizationId,
        long groupId,
        long userId)
    {
        try
        {
            await _groupBusiness.RemoveUserFromGroup(userId, organizationId, groupId);
            return Ok(new { message = $"Removed user {userId} from group {groupId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while removing user {userId} from group {groupId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Remove User from Group
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the group belongs</param>
    /// <param name="groupId">ID of the group to remove from</param>
    /// <param name="userId">ID of user to be removed</param>
    /// <returns>A 200 OK response with an empty body.</returns>
    [HttpDelete("{groupId:long}/users/{userId:long}", Name = "api_remove_user_from_group")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "group")]
    public async Task<ActionResult> RemoveUserFromGroupV2(
        long organizationId,
        long groupId,
        long userId)
    {
        var response = await _groupBusiness.RemoveUserFromGroup(userId, organizationId, groupId);
        return Ok(response);
    }
}
