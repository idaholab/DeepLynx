using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
/// Controller for managing AiModelConfig at the Project Level
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve AI Model Configurations
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/ai-model-configs")]
[Authorize]
[Tags("Project - AI Model Config")]
// [InsightEnabled] // AI model configs are only consumed by Insight features; gate with HIDE_INSIGHT.
public class AiModelConfigProjectController : ControllerBase
{
    private readonly IAiModelConfigBusiness _aiModelConfigBusiness;
    private readonly ILogger<AiModelConfigProjectController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiModelConfigProjectController" /> AI Model Configuration
    /// </summary>
    /// <param name="aiModelConfigBusiness"></param>
    /// <param name="logger"></param>
    public AiModelConfigProjectController(
        IAiModelConfigBusiness aiModelConfigBusiness,
        ILogger<AiModelConfigProjectController> logger)
    {
        _aiModelConfigBusiness = aiModelConfigBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get all AI Model Configurations for a project.
    /// </summary>
    /// <param name="organizationId">The ID of the organization whose AI Model Configurations are being retrieved.</param>
    /// <param name="projectId">The ID of the project whose AI Model Configurations are being retrieved.</param>
    /// <param name="hideArchived">If true, archived configurations are excluded from the results. Defaults to true.</param>
    /// <returns>A list of AI Model Configuration DTOs belonging to the specified project.</returns>
    [HttpGet(Name = "api_get_all_ai_model_configs_project")]
    public async Task<ActionResult<IEnumerable<AiModelConfigResponseDto>>> GetAllAiModelConfigs(
        long organizationId,
        long projectId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var aiModelConfigs = await _aiModelConfigBusiness.GetAllAiModelConfigs(organizationId, projectId, hideArchived);
            return Ok(aiModelConfigs);
        }
        catch (Exception exc)
        {
            _logger.LogError(exc, "An unexpected error occurred while fetching all AI Model Configurations for project {ProjectId}", projectId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching AI Model Configurations.");
        }
    }

    /// <summary>
    ///     Get a single AI Model Configuration by ID for a project.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the AI Model Configuration belongs.</param>
    /// <param name="projectId">The ID of the project to which the AI Model Configuration belongs.</param>
    /// <param name="aiModelConfigId">The ID of the AI Model Configuration to retrieve.</param>
    /// <param name="hideArchived">If true, archived configurations will not be returned. Defaults to true.</param>
    /// <returns>The AI Model Configuration DTO matching the specified ID.</returns>
    [HttpGet("{aiModelConfigId:long}", Name = "api_get_an_ai_model_config_project")]
    public async Task<ActionResult<AiModelConfigResponseDto>> GetAiModelConfig(
        long organizationId,
        long projectId,
        long aiModelConfigId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var aiModelConfig = await _aiModelConfigBusiness.GetAiModelConfig(organizationId, projectId, aiModelConfigId, hideArchived);
            return Ok(aiModelConfig);
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            _logger.LogError(exc, "An unexpected error occurred while fetching AI Model Configuration {AiModelConfigId}", aiModelConfigId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching the AI Model Configuration.");
        }
    }

    /// <summary>
    ///     Get the default AI Model Configuration for a given model type at the project level,
    ///     falling back to the organization-level default if no project-level default is found.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs.</param>
    /// <param name="projectId">The ID of the project whose default AI Model Configuration is being retrieved.</param>
    /// <param name="modelType">The type of model to retrieve the default configuration for (e.g. "language" or "embedding").</param>
    /// <returns>The default AI Model Configuration DTO for the specified model type.</returns>
    [HttpGet("default", Name = "api_get_default_ai_model_config_project")]
    public async Task<ActionResult<AiModelConfigResponseDto>> GetDefaultAiModelConfig(
        long organizationId,
        long projectId,
        [FromQuery] string modelType)
    {
        try
        {
            var aiModelConfig = await _aiModelConfigBusiness.GetDefaultAiModelConfig(organizationId, projectId, modelType);
            return Ok(aiModelConfig);
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            _logger.LogError(exc, "An unexpected error occurred while fetching the default {ModelType} AI Model Configuration for project {ProjectId}", modelType, projectId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching the default AI Model Configuration.");
        }
    }

    /// <summary>
    ///     Create a new AI Model Configuration for a project.
    ///     Insight Features require a VLM and an Embedding Model (LLM is optional)
    /// </summary>
    /// <param name="organizationId">The ID of the organization under which the AI Model Configuration will be created.</param>
    /// <param name="projectId">The ID of the project under which the AI Model Configuration will be created.</param>
    /// <param name="dto">The data transfer object containing the details of the AI Model Configuration to create.</param>
    /// <returns>The newly created AI Model Configuration DTO.</returns>
    [HttpPost(Name = "api_create_ai_model_config_project")]
    public async Task<ActionResult<AiModelConfigResponseDto>> CreateAiModelConfig(
        long organizationId,
        long projectId,
        [FromBody] CreateAiModelConfigDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var newAiModelConfig = await _aiModelConfigBusiness.CreateAiModelConfig(currentUserId, organizationId, projectId, dto);
            return Ok(newAiModelConfig);
        }
        catch (ArgumentException exc)
        {
            return BadRequest(exc.Message);
        }
        catch (Exception exc)
        {
            _logger.LogError(exc, "An unexpected error occurred while creating an AI Model Configuration for project {ProjectId}", projectId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while creating the AI Model Configuration.");
        }
    }

    /// <summary>
    ///     Update an existing AI Model Configuration for a project.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the AI Model Configuration belongs.</param>
    /// <param name="projectId">The ID of the project to which the AI Model Configuration belongs.</param>
    /// <param name="aiModelConfigId">The ID of the AI Model Configuration to update.</param>
    /// <param name="dto">The data transfer object containing the updated details of the AI Model Configuration.</param>
    /// <returns>The updated AI Model Configuration DTO.</returns>
    [HttpPut("{aiModelConfigId:long}", Name = "api_update_ai_model_config_project")]
    public async Task<ActionResult<AiModelConfigResponseDto>> UpdateAiModelConfig(
        long organizationId,
        long projectId,
        long aiModelConfigId,
        [FromBody] UpdateAiModelConfigDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var updatedConfig = await _aiModelConfigBusiness.UpdateAiModelConfig(
                currentUserId, organizationId, projectId, aiModelConfigId, dto);
            return Ok(updatedConfig);
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (InvalidOperationException exc)
        {
            return UnprocessableEntity(exc.Message);
        }
        catch (Exception exc)
        {
            _logger.LogError(exc, "An unexpected error occurred while updating AI Model Configuration {AiModelConfigId}", aiModelConfigId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while updating the AI Model Configuration.");
        }
    }

    /// <summary>
    ///     Archive or unarchive an AI Model Configuration for a project.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the AI Model Configuration belongs.</param>
    /// <param name="projectId">The ID of the project to which the AI Model Configuration belongs.</param>
    /// <param name="aiModelConfigId">The ID of the AI Model Configuration to archive or unarchive.</param>
    /// <param name="archive">True to archive the AI Model Configuration, false to unarchive it.</param>
    /// <returns>A message confirming the AI Model Configuration was successfully archived or unarchived.</returns>
    [HttpPatch("{aiModelConfigId:long}/archive", Name = "api_archive_ai_model_config_project")]
    public async Task<IActionResult> ArchiveAiModelConfig(
        long organizationId,
        long projectId,
        long aiModelConfigId,
        [FromQuery] bool archive)
    {
        try
        {
            var userId = UserContextStorage.UserId;
            if (archive)
            {
                await _aiModelConfigBusiness.ArchiveAiModelConfig(userId, organizationId, projectId, aiModelConfigId);
                return Ok(new { message = $"Archived AI Model Configuration {aiModelConfigId}" });
            }

            await _aiModelConfigBusiness.UnarchiveAiModelConfig(userId, organizationId, projectId, aiModelConfigId);
            return Ok(new { message = $"Unarchived AI Model Configuration {aiModelConfigId}" });
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (InvalidOperationException exc)
        {
            return UnprocessableEntity(exc.Message);
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            _logger.LogError(exc, "An unexpected error occurred while {Action} AI Model Configuration {AiModelConfigId}", action, aiModelConfigId);
            return StatusCode(StatusCodes.Status500InternalServerError, $"An unexpected error occurred while {action} the AI Model Configuration.");
        }
    }

    /// <summary>
    ///     Permanently delete an AI Model Configuration for a project.
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the AI Model Configuration belongs.</param>
    /// <param name="projectId">The ID of the project to which the AI Model Configuration belongs.</param>
    /// <param name="aiModelConfigId">The ID of the AI Model Configuration to delete.</param>
    /// <returns>A message confirming the AI Model Configuration was successfully deleted.</returns>
    [HttpDelete("{aiModelConfigId:long}", Name = "api_delete_ai_model_configuration_project")]
    public async Task<IActionResult> DeleteAiModelConfig(long organizationId, long projectId, long aiModelConfigId)
    {
        try
        {
            await _aiModelConfigBusiness.DeleteAiModelConfig(organizationId, projectId, aiModelConfigId);
            return Ok(new { message = $"Deleted AI Model Configuration {aiModelConfigId}" });
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (InvalidOperationException exc)
        {
            return UnprocessableEntity(exc.Message);
        }
        catch (Exception exc)
        {
            _logger.LogError(exc, "An unexpected error occurred while deleting AI Model Configuration {AiModelConfigId}", aiModelConfigId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while deleting the AI Model Configuration.");
        }
    }
}