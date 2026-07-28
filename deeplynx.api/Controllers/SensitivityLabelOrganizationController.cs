using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using Asp.Versioning;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers;

[ApiController]
[ApiVersion(1, Deprecated = true)]
[ApiVersion(2)]
[Route("organizations/{organizationId:long}/labels")]
[Authorize]
[ForbidServiceAccounts] // service accounts can only act on the project level
[Tags("Organization - Sensitivity Label")]
public class SensitivityLabelOrganizationController : ControllerBase
{
    private readonly ILogger<SensitivityLabelOrganizationController> _logger;
    private readonly ISensitivityLabelBusiness _sensitivityLabelBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SensitivityLabelOrganizationController" /> class
    /// </summary>
    /// <param name="sensitivityLabelBusiness">The business logic interface for handling Sensitivity Label operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public SensitivityLabelOrganizationController(ISensitivityLabelBusiness sensitivityLabelBusiness,
        ILogger<SensitivityLabelOrganizationController> logger)
    {
        _sensitivityLabelBusiness = sensitivityLabelBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     List Sensitivity Labels 
    /// </summary>
    /// <param name="organizationId">ID of the organization across which to search</param>
    /// <param name="projectIds">(Optional)An array of project IDs within the organization to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived labels</param>
    /// <returns></returns>
    [HttpGet(Name = "api_get_all_sensitivity_labels_organization")]
    [MapToApiVersion(1)]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelResponseDto>>> GetAllSensitivityLabels(
        long organizationId,
        [FromQuery] long[]? projectIds,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var labels = await _sensitivityLabelBusiness
                .GetAllSensitivityLabels(currentUserId, projectIds, organizationId,
                    hideArchived); //setting project ID null for now to circumvent xor logic
            return Ok(labels);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing sensitivity labels: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     List Sensitivity Labels 
    /// </summary>
    /// <param name="organizationId">ID of the organization across which to search</param>
    /// <param name="projectIds">(Optional)An array of project IDs within the organization to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived labels</param>
    /// <returns>A list of sensitivity labels matching the applied filters.</returns>
    [HttpGet(Name = "api_get_all_sensitivity_labels_organization")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelResponseDto>>> GetAllSensitivityLabelsV2(
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
    /// <returns></returns>
    [HttpGet("{labelId:long}", Name = "api_get_sensitivity_label_organization")]
    [MapToApiVersion(1)]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> GetSensitivityLabel(
        long organizationId,
        long labelId, [FromQuery] bool hideArchived = true)
    {
        try
        {
            var label = await _sensitivityLabelBusiness.GetSensitivityLabel(labelId, null, organizationId,
                hideArchived);
            return Ok(label);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving sensitivity label {labelId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Fetch Sensitivity Label by ID 
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of sensitivity label</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived labels</param>
    /// <returns>The sensitivity label associated with the given ID.</returns>
    [HttpGet("{labelId:long}", Name = "api_get_sensitivity_label_organization")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> GetSensitivityLabelV2(
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
    /// <returns></returns>
    [HttpPost(Name = "api_create_sensitivity_label_organization")]
    [MapToApiVersion(1)]
    [Auth("write", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> CreateSensitivityLabel(
        long organizationId,
        [FromBody] CreateSensitivityLabelRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(currentUserId, dto, null,
                organizationId);
            return Ok(label);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating sensitivity label: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create a Sensitivity Label 
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="dto">Data structure of sensitivity label to create</param>
    /// <returns>The created sensitivity label.</returns>
    [HttpPost(Name = "api_create_sensitivity_label_organization")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> CreateSensitivityLabelV2(
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
    /// <returns></returns>
    [HttpPut("{labelId:long}", Name = "api_update_sensitivity_label_organization")]
    [MapToApiVersion(1)]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> UpdateSensitivityLabel(
        long organizationId,
        long labelId,
        [FromBody] UpdateSensitivityLabelRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var label = await _sensitivityLabelBusiness.UpdateSensitivityLabel(currentUserId, labelId, null,
                organizationId, dto);
            return Ok(label);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating sensitivity label {labelId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Update a Sensitivity Label 
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="dto">Fields to update</param>
    /// <returns>The updated sensitivity label.</returns>
    [HttpPut("{labelId:long}", Name = "api_update_sensitivity_label_organization")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> UpdateSensitivityLabelV2(
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
    /// <returns></returns>
    [HttpDelete("{labelId:long}", Name = "api_delete_sensitivity_label_organization")]
    [MapToApiVersion(1)]
    [Auth("write", "sensitivity_label")]
    public async Task<ActionResult> DeleteSensitivityLabel(
        long organizationId,
        long labelId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _sensitivityLabelBusiness.DeleteSensitivityLabel(currentUserId, labelId, null, organizationId);
            return Ok(new { message = $"Deleted sensitivity label {labelId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting sensitivity label {labelId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete a Sensitivity Label 
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label to hard delete</param>
    /// <returns>True if the sensitivity label was successfully deleted.</returns>
    [HttpDelete("{labelId:long}", Name = "api_delete_sensitivity_label_organization")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "sensitivity_label")]
    public async Task<ActionResult<bool>> DeleteSensitivityLabelV2(
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
    /// <returns>A message stating the label was successfully archived or unarchived.</returns>
    [HttpPatch("{labelId:long}", Name = "api_archive_sensitivity_label_organization")]
    [MapToApiVersion(1)]
    [Auth("update", "sensitivity_label")]
    public async Task<IActionResult> ArchiveSensitivityLabel(
        long organizationId,
        long labelId,
        [FromQuery] bool archive)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                await _sensitivityLabelBusiness.ArchiveSensitivityLabel(currentUserId, labelId, null,
                    organizationId);
                return Ok(new { message = $"Archived sensitivity label {labelId}" });
            }

            await _sensitivityLabelBusiness.UnarchiveSensitivityLabel(currentUserId, labelId, null,
                organizationId);
            return Ok(new { message = $"Unarchived sensitivity label {labelId}" });
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} sensitivity label {labelId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive a Sensitivity Label 
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the label belongs</param>
    /// <param name="labelId">The ID of the sensitivity label to archive or unarchive.</param>
    /// <param name="archive">True to archive the label, false to unarchive it.</param>
    /// <returns>True if the sensitivity label was successfully archived or unarchived.</returns>
    [HttpPatch("{labelId:long}", Name = "api_archive_sensitivity_label_organization")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("update", "sensitivity_label")]
    public async Task<ActionResult<bool>> ArchiveSensitivityLabelV2(
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
}
