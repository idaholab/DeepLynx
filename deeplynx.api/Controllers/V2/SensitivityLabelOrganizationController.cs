using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using Asp.Versioning;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

[ApiController]

[ApiVersion(2)]
[Route("organizations/{organizationId:long}/labels")]
[Authorize]
[ForbidServiceAccounts] // service accounts can only act on the project level
[Tags("Organization - Sensitivity Label")]
public class SensitivityLabelOrganizationController : ControllerBase
{
    private readonly ILogger<SensitivityLabelOrganizationController> _logger;
    private readonly ISensitivityLabelBusiness _sensitivityLabelBusiness;
    private readonly ISensitivityLabelGrantBusiness _sensitivityLabelGrantBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SensitivityLabelOrganizationController" /> class
    /// </summary>
    /// <param name="sensitivityLabelBusiness">The business logic interface for handling Sensitivity Label operations.</param>
    /// <param name="sensitivityLabelGrantBusiness">The business logic interface for handling access grants to Sensitivity Labels.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public SensitivityLabelOrganizationController(ISensitivityLabelBusiness sensitivityLabelBusiness,
        ISensitivityLabelGrantBusiness sensitivityLabelGrantBusiness,
        ILogger<SensitivityLabelOrganizationController> logger)
    {
        _sensitivityLabelBusiness = sensitivityLabelBusiness;
        _sensitivityLabelGrantBusiness = sensitivityLabelGrantBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     List Sensitivity Labels 
    /// </summary>
    /// <param name="organizationId">ID of the organization across which to search</param>
    /// <param name="projectIds">(Optional)An array of project IDs within the organization to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived labels</param>
    /// <returns>A list of sensitivity labels matching the applied filters.</returns>
    [HttpGet(Name = "api_get_all_sensitivity_labels_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelResponseDto>>> GetAllSensitivityLabels(
        long organizationId,
        [FromQuery] long[]? projectIds,
        [FromQuery] bool hideArchived = true)
    {
            var currentUserId = UserContextStorage.UserId;
            var labels = await _sensitivityLabelBusiness
                .GetAllSensitivityLabels(currentUserId, projectIds, organizationId,
                    hideArchived);
            return Ok(labels);
    }

    /// <summary>
    ///     Fetch Sensitivity Label by ID 
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of sensitivity label</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived labels</param>
    /// <returns>The sensitivity label associated with the given ID.</returns>
    [HttpGet("{labelId:long}", Name = "api_get_sensitivity_label_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> GetSensitivityLabel(
        long organizationId,
        long labelId, [FromQuery] bool hideArchived = true)
    {
            var label = await _sensitivityLabelBusiness.GetSensitivityLabel(labelId, null, organizationId,
                hideArchived);
            return Ok(label);
    }

    /// <summary>
    ///     Create a Sensitivity Label 
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="dto">Data structure of sensitivity label to create</param>
    /// <returns>The created sensitivity label.</returns>
    [HttpPost(Name = "api_create_sensitivity_label_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> CreateSensitivityLabel(
        long organizationId,
        [FromBody] CreateSensitivityLabelRequestDto dto)
    {
            var currentUserId = UserContextStorage.UserId;
            var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(currentUserId, dto, null,
                organizationId);
            return Ok(label);
    }

    /// <summary>
    ///     Update a Sensitivity Label 
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="dto">Fields to update</param>
    /// <returns>The updated sensitivity label.</returns>
    [HttpPut("{labelId:long}", Name = "api_update_sensitivity_label_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> UpdateSensitivityLabel(
        long organizationId,
        long labelId,
        [FromBody] UpdateSensitivityLabelRequestDto dto)
    {
            var currentUserId = UserContextStorage.UserId;
            var label = await _sensitivityLabelBusiness.UpdateSensitivityLabel(currentUserId, labelId, null,
                organizationId, dto);
            return Ok(label);
    }

    /// <summary>
    ///     Delete a Sensitivity Label 
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label to hard delete</param>
    /// <returns>True if the sensitivity label was successfully deleted.</returns>
    [HttpDelete("{labelId:long}", Name = "api_delete_sensitivity_label_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "sensitivity_label")]
    public async Task<ActionResult<bool>> DeleteSensitivityLabel(
        long organizationId,
        long labelId)
    {
            var currentUserId = UserContextStorage.UserId;
            var response = await _sensitivityLabelBusiness.DeleteSensitivityLabel(currentUserId, labelId, null, organizationId);
            return Ok(response);
    }

    /// <summary>
    ///     Archive or Unarchive a Sensitivity Label 
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">The ID of the sensitivity label to archive or unarchive.</param>
    /// <param name="archive">True to archive the label, false to unarchive it.</param>
    /// <returns>True if the sensitivity label was successfully archived or unarchived.</returns>
    [HttpPatch("{labelId:long}", Name = "api_archive_sensitivity_label_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<bool>> ArchiveSensitivityLabel(
        long organizationId,
        long labelId,
        [FromQuery] bool archive)
    {
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                var responseA = await _sensitivityLabelBusiness.ArchiveSensitivityLabel(currentUserId, labelId, null,
                    organizationId);
                return Ok(responseA);
            }

            var responseB = await _sensitivityLabelBusiness.UnarchiveSensitivityLabel(currentUserId, labelId, null,
                organizationId);
            return Ok(responseB);
    }

    /// <summary>
    ///     List Sensitivity Label Permission Actions
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the labels belong</param>
    /// <returns>A list of all possible sensitivity label permission actions.</returns>
    [HttpGet("permission-actions", Name = "api_get_sensitivity_label_permission_actions_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<List<SensitivityLabelPermissionActionResponseDto>>> GetSensitivityLabelPermissionActions(
        long organizationId)
    {
        var permissionActions = await _sensitivityLabelBusiness.GetSensitivityLabelPermissionActions();
        return Ok(permissionActions);
    }

    /// <summary>
    ///     List Users and Groups with Access to a Sensitivity Label
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <returns>The list of users and groups granted access to the label.</returns>
    [HttpGet("{labelId:long}/members", Name = "api_get_sensitivity_label_organization_members")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelMemberAccessDto>>> GetMembersWithAccessToLabel(
        long organizationId,
        long labelId)
    {
        var members = await _sensitivityLabelGrantBusiness.GetMembersWithLabelAccess(labelId, organizationId, null);
        return Ok(members);
    }

    /// <summary>
    ///     List Permissions a given user has on the given label
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="userId">ID of the user.</param>
    /// <returns>The list of permissions granted this user on this label.</returns>
    [HttpGet("{labelId:long}/permissions/user/{userId:long}", Name = "api_get_organization_sensitivity_label_grants_user")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelPermissionResponseDto>>> GetUserPermissionsForLabel(
        long organizationId,
        long labelId,
        long userId)
    {
        var permissions = await _sensitivityLabelGrantBusiness.GetMemberPermissionsForLabel(
            labelId, organizationId, null, userId, null);
        return Ok(permissions);
    }

    /// <summary>
    ///     List Permissions the current user has on the given label
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <returns>The list of permissions granted the current user on this label.</returns>
    [HttpGet("{labelId:long}/permissions/user/current", Name = "api_get_organization_sensitivity_label_grants_current_user")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelPermissionResponseDto>>> GetCurrentUserPermissionsForLabel(
        long organizationId,
        long labelId)
    {
        var permissions = await _sensitivityLabelGrantBusiness.GetMemberPermissionsForLabel(
            labelId, organizationId, null, UserContextStorage.UserId, null);
        return Ok(permissions);
    }

    /// <summary>
    ///     List Permissions a given group has on the given label
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="groupId">(optional) ID of the group.</param>
    /// <returns>The list of permissions granted this group on this label.</returns>
    [HttpGet("{labelId:long}/permissions/group/{groupId:long}", Name = "api_get_organization_sensitivity_label_grants_group")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelPermissionResponseDto>>> GetGroupPermissionsForLabel(
        long organizationId,
        long labelId,
        long? groupId)
    {
        var permissions = await _sensitivityLabelGrantBusiness.GetMemberPermissionsForLabel(
            labelId, organizationId, null, null, groupId);
        return Ok(permissions);
    }

    /// <summary>
    ///     Grant a single User Access to a Sensitivity Label
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="userId">ID of the user to grant access to</param>
    /// <param name="dto">The permissions to give the user</param>
    /// <returns>The member's new access grants on the label.</returns>
    [HttpPost("{labelId:long}/users/{userId:long}", Name = "api_sensitivity_label_grant_user_access_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelMemberAccessDto>>> GrantUserLabelAccess(
        long organizationId,
        long labelId,
        long userId,
        [FromBody] GrantLabelAccessDto dto)
    {
        dto.UserIds = [userId];
        var currentUserId = UserContextStorage.UserId;
        var response = await _sensitivityLabelGrantBusiness.SetAccessForLabel(
            currentUserId, labelId, organizationId, null, dto);
        return Ok(response);
    }

    /// <summary>
    ///     Grant a Set of Users Access to a Sensitivity Label
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="dto">The users to grant and the permissions to give them</param>
    /// <returns>True if the access grants were successfully replaced.</returns>
    [HttpPost("{labelId:long}/users", Name = "api_sensitivity_label_grant_users_access_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<bool>> GrantUsersLabelAccess(
        long organizationId,
        long labelId,
        [FromBody] GrantLabelAccessDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _sensitivityLabelGrantBusiness.SetAccessForLabel(
            currentUserId, labelId, organizationId, null, dto);
        return Ok(response);
    }

    /// <summary>
    ///     Grant a single Group Access to a Sensitivity Label
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="groupId">ID of the group to grant access to</param>
    /// <param name="dto">The permissions to give the group</param>
    /// <returns>The member's new access grants on the label.</returns>
    [HttpPost("{labelId:long}/groups/{groupId:long}", Name = "api_sensitivity_label_grant_group_access_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelMemberAccessDto>>> GrantGroupLabelAccess(
        long organizationId,
        long labelId,
        long groupId,
        [FromBody] GrantLabelAccessDto dto)
    {
        dto.GroupIds = [groupId];
        var currentUserId = UserContextStorage.UserId;
        var response = await _sensitivityLabelGrantBusiness.SetAccessForLabel(
            currentUserId, labelId, organizationId, null, dto);
        return Ok(response);
    }

    /// <summary>
    ///     Grant a Set of Groups Access to a Sensitivity Label
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="dto">The groups to grant and the permissions to give them</param>
    /// <returns>True if the access grants were successfully replaced.</returns>
    [HttpPost("{labelId:long}/groups", Name = "api_sensitivity_label_grant_groups_access_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelMemberAccessDto>>> GrantGroupsLabelAccess(
        long organizationId,
        long labelId,
        [FromBody] GrantLabelAccessDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _sensitivityLabelGrantBusiness.SetAccessForLabel(
            currentUserId, labelId, organizationId, null, dto);
        return Ok(response);
    }

    /// <summary>
    ///     Grant a Mixed Set of Users and/or Groups Access to a Sensitivity Label
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="dto">The users/groups to grant and the permissions to give them</param>
    /// <returns>A list of members (users and groups) and their new permissions on the label.</returns>
    [HttpPost("{labelId:long}/grant", Name = "api_grant_sensitivity_label_access_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelMemberAccessDto>>> GrantLabelAccess(
        long organizationId,
        long labelId,
        [FromBody] GrantLabelAccessDto dto)
    {
        var currentUserId = UserContextStorage.UserId;
        var response = await _sensitivityLabelGrantBusiness.SetAccessForLabel(
            currentUserId, labelId, organizationId, null, dto);
        return Ok(response);
    }

    /// <summary>
    ///     Revoke a User's Access to a Sensitivity Label
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="userId">ID of the user to revoke access from</param>
    /// <returns>True if the access grant was successfully revoked.</returns>
    [HttpDelete("{labelId:long}/users/{userId:long}", Name = "api_revoke_sensitivity_label_organization_user_access")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<bool>> RevokeLabelAccessFromUser(
        long organizationId,
        long labelId,
        long userId)
    {
        var response = await _sensitivityLabelGrantBusiness.RevokeAccessForLabel(
            labelId, organizationId, null, [userId], null);
        return Ok(response);
    }

    /// <summary>
    ///     Revoke a Group's Access to a Sensitivity Label
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="groupId">ID of the group to revoke access from</param>
    /// <returns>True if the access grant was successfully revoked.</returns>
    [HttpDelete("{labelId:long}/groups/{groupId:long}", Name = "api_revoke_sensitivity_label_organization_group_access")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<bool>> RevokeLabelAccessFromGroup(
        long organizationId,
        long labelId,
        long groupId)
    {
        var response = await _sensitivityLabelGrantBusiness.RevokeAccessForLabel(
            labelId, organizationId, null, null, [groupId]);
        return Ok(response);
    }
}