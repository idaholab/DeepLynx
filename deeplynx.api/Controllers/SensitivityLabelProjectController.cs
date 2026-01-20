using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

[ApiController]
[Route("projects/{projectId:long}/labels")]
[Authorize]
[Tags("Project - Sensitivity Label")]
public class SensitivityLabelProjectController : ControllerBase
{
    private readonly ILogger<SensitivityLabelProjectController> _logger;
    private readonly ISensitivityLabelBusiness _sensitivityLabelBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SensitivityLabelProjectController" /> class
    /// </summary>
    /// <param name="sensitivityLabelBusiness">The business logic interface for handling Sensitivity Label operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public SensitivityLabelProjectController(ISensitivityLabelBusiness sensitivityLabelBusiness,
        ILogger<SensitivityLabelProjectController> logger)
    {
        _sensitivityLabelBusiness = sensitivityLabelBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Sensitivity Labels 
    /// </summary>
    /// <param name="projectId">ID of the project across which to search</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived labels</param>
    /// <returns></returns>
    [HttpGet(Name = "api_get_all_sensitivity_labels_project")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<IEnumerable<SensitivityLabelResponseDto>>> GetAllSensitivityLabels(
        long projectId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var labels = await _sensitivityLabelBusiness
                .GetAllSensitivityLabels([projectId], organizationId,
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
    ///     Get a Sensitivity Label 
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">ID of sensitivity label</param>
    /// <param name="hideArchived">Flag indicating whether to hide or show archived labels</param>
    /// <returns></returns>
    [HttpGet("{labelId:long}", Name = "api_get_sensitivity_label_project")]
    [Auth("read", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> GetSensitivityLabel(
        long projectId,
        long labelId, [FromQuery] bool hideArchived = true)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var label = await _sensitivityLabelBusiness.GetSensitivityLabel(labelId, projectId, organizationId,
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
    ///     Create a Sensitivity Label 
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="dto">Data structure of sensitivity label to create</param>
    /// <returns></returns>
    [HttpPost(Name = "api_create_sensitivity_label_project")]
    [Auth("write", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> CreateSensitivityLabel(
        long projectId,
        [FromBody] CreateSensitivityLabelRequestDto dto)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var label = await _sensitivityLabelBusiness.CreateSensitivityLabel(currentUserId, dto, projectId,
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
    ///     Update a Sensitivity Label 
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label</param>
    /// <param name="dto">Fields to update</param>
    /// <returns></returns>
    [HttpPut("{labelId:long}", Name = "api_update_sensitivity_label_project")]
    [Auth("write", "sensitivity_label")]
    public async Task<ActionResult<SensitivityLabelResponseDto>> UpdateSensitivityLabel(
        long projectId,
        long labelId,
        [FromBody] UpdateSensitivityLabelRequestDto dto)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            var label = await _sensitivityLabelBusiness.UpdateSensitivityLabel(currentUserId, labelId, projectId,
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
    ///     Delete a Sensitivity Label 
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">ID of the sensitivity label to hard delete</param>
    /// <returns></returns>
    [HttpDelete("{labelId:long}", Name = "api_delete_sensitivity_label_project")]
    [Auth("write", "sensitivity_label")]
    public async Task<ActionResult> DeleteSensitivityLabel(
        long projectId,
        long labelId)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            await _sensitivityLabelBusiness.DeleteSensitivityLabel(currentUserId, labelId, projectId, organizationId);
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
    ///     Archive or Unarchive a Sensitivity Label 
    /// </summary>
    /// <param name="projectId">ID of the project to which the label belongs</param>
    /// <param name="labelId">The ID of the sensitivity label to archive or unarchive.</param>
    /// <param name="archive">True to archive the label, false to unarchive it.</param>
    /// <returns>A message stating the label was successfully archived or unarchived.</returns>
    [HttpPatch("{labelId:long}", Name = "api_archive_sensitivity_label_project")]
    [Auth("write", "sensitivity_label")]
    public async Task<IActionResult> ArchiveSensitivityLabel(
        long projectId,
        long labelId,
        [FromQuery] bool archive)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                await _sensitivityLabelBusiness.ArchiveSensitivityLabel(currentUserId, labelId, projectId,
                    organizationId);
                return Ok(new { message = $"Archived sensitivity label {labelId}" });
            }

            await _sensitivityLabelBusiness.UnarchiveSensitivityLabel(currentUserId, labelId, projectId,
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
}