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
[Route("projects/{projectId:long}/labels")]
[Authorize]
[Tags("Project - Sensitivity Label")]
public class SensitivityLabelProjectController : ControllerBase
{
    private readonly ILogger<SensitivityLabelProjectController> _logger;
    private readonly ISensitivityLabelBusiness _sensitivityLabelBusiness;
    private readonly IUserSensitivityLabelBusiness _userSensitivityLabelBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SensitivityLabelProjectController" /> class
    /// </summary>
    /// <param name="sensitivityLabelBusiness">The business logic interface for handling Sensitivity Label operations.</param>
    /// <param name="userSensitivityLabelBusiness">The business logic interface for handling user access grants to Sensitivity Labels.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public SensitivityLabelProjectController(ISensitivityLabelBusiness sensitivityLabelBusiness,
        IUserSensitivityLabelBusiness userSensitivityLabelBusiness,
        ILogger<SensitivityLabelProjectController> logger)
    {
        _sensitivityLabelBusiness = sensitivityLabelBusiness;
        _userSensitivityLabelBusiness = userSensitivityLabelBusiness;
        _logger = logger;
    }



    /// <summary>
    ///     Get All Sensitivity Labels 
    /// </summary>
    /// <param name="projectId">ID of the project across which to search</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived labels</param>
    /// <returns>A list of sensitivity labels matching the applied filters.</returns>
    [HttpGet(Name = "api_get_all_sensitivity_labels_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelResponseDto>>> GetAllSensitivityLabels(
        long projectId,
        [FromQuery] bool hideArchived = true)
    {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var labels = await _sensitivityLabelBusiness
                .GetAllSensitivityLabels(currentUserId, [projectId], organizationId,
                    hideArchived); //setting project ID null for now to circumvent xor logic
            return Ok(labels);
    }



    /// <summary>
    ///     Get a Sensitivity Label 
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">ID of sensitivity label</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived labels</param>
    /// <returns>The sensitivity label associated with the given ID.</returns>
    [HttpGet("{labelId:long}", Name = "api_get_sensitivity_label_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> GetSensitivityLabel(
        long projectId,
        long labelId, [FromQuery] bool hideArchived = true)
    {
            var organizationId = UserContextStorage.OrganizationId;
            var label = await _sensitivityLabelBusiness.GetSensitivityLabel(labelId, projectId, organizationId,
                hideArchived);
            return Ok(label);
    }



    /// <summary>
    ///     Create a Sensitivity Label 
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="dto">Data structure of sensitivity label to create</param>
    /// <returns>The created sensitivity label.</returns>
    [HttpPost(Name = "api_create_sensitivity_label_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> CreateSensitivityLabel(
        long projectId,
        [FromBody] CreateSensitivityLabelRequestDto dto)
    {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(currentUserId, dto, projectId,
                organizationId);
            return Ok(label);
    }



    /// <summary>
    ///     Update a Sensitivity Label 
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="dto">Fields to update</param>
    /// <returns>The updated sensitivity label.</returns>
    [HttpPut("{labelId:long}", Name = "api_update_sensitivity_label_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> UpdateSensitivityLabel(
        long projectId,
        long labelId,
        [FromBody] UpdateSensitivityLabelRequestDto dto)
    {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var label = await _sensitivityLabelBusiness.UpdateSensitivityLabel(currentUserId, labelId, projectId,
                organizationId, dto);
            return Ok(label);
    }



    /// <summary>
    ///     Delete a Sensitivity Label 
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label to hard delete</param>
    /// <returns>True if the sensitivity label was successfully deleted.</returns>
    [HttpDelete("{labelId:long}", Name = "api_delete_sensitivity_label_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "sensitivity_label")]
    public async Task<ActionResult<bool>> DeleteSensitivityLabel(
        long projectId,
        long labelId)
    {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var response = await _sensitivityLabelBusiness.DeleteSensitivityLabel(currentUserId, labelId, projectId, organizationId);
            return Ok(response);
    }



    /// <summary>
    ///     Archive or Unarchive a Sensitivity Label 
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">The ID of the sensitivity label to archive or unarchive.</param>
    /// <param name="archive">True to archive the label, false to unarchive it.</param>
    /// <returns>True if the sensitivity label was successfully archived or unarchived.</returns>
    [HttpPatch("{labelId:long}", Name = "api_archive_sensitivity_label_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<bool>> ArchiveSensitivityLabel(
        long projectId,
        long labelId,
        [FromQuery] bool archive)
    {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                var responseA = await _sensitivityLabelBusiness.ArchiveSensitivityLabel(currentUserId, labelId, projectId,
                    organizationId);
                return Ok(responseA);
            }

            var responseB = await _sensitivityLabelBusiness.UnarchiveSensitivityLabel(currentUserId, labelId, projectId,
                organizationId);
            return Ok(responseB);
    }



    /// <summary>
    ///     List Users with Access to a Sensitivity Label
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <returns>The list of users granted access to the label.</returns>
    [HttpGet("{labelId:long}/users", Name = "api_get_sensitivity_label_project_users")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<UserSensitivityLabelResponseDto>>> GetUsersWithAccessToLabel(
        long projectId,
        long labelId)
    {
            var organizationId = UserContextStorage.OrganizationId;
            var users = await _userSensitivityLabelBusiness.GetUsersWithAccessToLabel(labelId, organizationId, projectId);
            return Ok(users);
    }



    /// <summary>
    ///     Grant a User Access to a Sensitivity Label
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="userId">ID of the user to grant access to</param>
    /// <returns>The created access grant.</returns>
    [HttpPost("{labelId:long}/users/{userId:long}", Name = "api_grant_sensitivity_label_project_user_access")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<UserSensitivityLabelResponseDto>> GrantLabelAccess(
        long projectId,
        long labelId,
        long userId)
    {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var grant = await _userSensitivityLabelBusiness.GrantLabelAccess(currentUserId, labelId, userId, organizationId, projectId);
            return Ok(grant);
    }



    /// <summary>
    ///     Revoke a User's Access to a Sensitivity Label
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="userId">ID of the user to revoke access from</param>
    /// <returns>True if the access grant was successfully revoked.</returns>
    [HttpDelete("{labelId:long}/users/{userId:long}", Name = "api_revoke_sensitivity_label_project_user_access")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<bool>> RevokeLabelAccess(
        long projectId,
        long labelId,
        long userId)
    {
            var organizationId = UserContextStorage.OrganizationId;
            var response = await _userSensitivityLabelBusiness.RevokeLabelAccess(labelId, userId, organizationId, projectId);
            return Ok(response);
    }



    /// <summary>
    ///     Replace the Set of Users with Access to a Sensitivity Label
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="userIds">The complete set of user IDs that should have access after this call</param>
    /// <returns>True if the access grants were successfully replaced.</returns>
    [HttpPut("{labelId:long}/users", Name = "api_set_sensitivity_label_project_users")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<bool>> SetUsersForLabel(
        long projectId,
        long labelId,
        [FromBody] long[] userIds)
    {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var response = await _userSensitivityLabelBusiness.SetUsersForLabel(currentUserId, labelId, userIds, organizationId, projectId);
            return Ok(response);
    }



    /// <summary>
    ///     List Permissions Governed by a Sensitivity Label
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <returns>The list of actions governed by the label.</returns>
    [HttpGet("{labelId:long}/permissions", Name = "api_get_sensitivity_label_project_permissions")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelPermissionResponseDto>>> GetPermissionsForLabel(
        long projectId,
        long labelId)
    {
            var organizationId = UserContextStorage.OrganizationId;
            var permissions = await _userSensitivityLabelBusiness.GetPermissionsForLabel(labelId, organizationId, projectId);
            return Ok(permissions);
    }
}