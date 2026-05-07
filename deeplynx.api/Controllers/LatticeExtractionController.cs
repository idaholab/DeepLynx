using System.Text.Json;
using deeplynx.helpers.Context;
using deeplynx.helpers.json;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing AI extractions into the staging schema.
/// </summary>
/// <remarks>
///     Extractions hold staged records, classes, relationships, and edges awaiting human approval.
///     Once approved, they are promoted into the deeplynx schema.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/extractions")]
[Authorize]
[Tags("Lattice")]
public class LatticeExtractionController : ControllerBase
{
    private readonly ILatticeExtractionBusiness _latticeExtractionBusiness;
    private readonly IInsightBusiness _insightBusiness;
    private readonly ILogger<LatticeExtractionController> _logger;

    public LatticeExtractionController(
        ILatticeExtractionBusiness latticeExtractionBusiness,
        IInsightBusiness insightBusiness,
        ILogger<LatticeExtractionController> logger)
    {
        _latticeExtractionBusiness = latticeExtractionBusiness;
        _insightBusiness = insightBusiness;
        _logger = logger;
    }
    
    /// <summary>
    ///     Returns all extractions created by the current user, ordered newest first.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    [HttpGet(Name = "api_list_extractions")]
    public async Task<IActionResult> ListExtractions(long organizationId, long projectId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var result = await _latticeExtractionBusiness.ListExtractionsByUser(currentUserId, projectId);
            return Ok(result);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing extractions: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Returns the ontology embedding status for a project — how many classes and relationships
    ///     exist and how many have been embedded. Use this to check readiness before triggering extraction.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    [HttpGet("embedding-status", Name = "api_get_embedding_status")]
    public async Task<IActionResult> GetEmbeddingStatus(long organizationId, long projectId)
    {
        try
        {
            var result = await _latticeExtractionBusiness.GetEmbeddingStatus(projectId);
            return Ok(result);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving embedding status: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Trigger ontology embedding for all classes and relationships in the project.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project whose ontology will be embedded.</param>
    /// <param name="embeddingModelConfigId">
    ///     Optional embedding model config ID. If omitted, the project/org default is used.
    /// </param>
    [HttpPost("embed-ontology", Name = "api_embed_ontology")]
    public async Task<IActionResult> EmbedOntology(
        long organizationId,
        long projectId,
        [FromQuery] long? embeddingModelConfigId = null)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _insightBusiness.QueueInsightEmbedStrings(
                currentUserId, organizationId, projectId, embeddingModelConfigId);
            return Accepted();
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while queuing ontology embedding: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Mark an extraction as failed. Called by Insight when async extraction cannot complete.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="extractionId">The extraction ID returned by the trigger endpoint.</param>
    /// <param name="errorMessage">Optional error message from Insight describing the failure.</param>
    [AllowAnonymous]
    [HttpPost("{extractionId:long}/failure", Name = "api_insight_extraction_failure")]
    public async Task<IActionResult> InsightExtractionFailure(
        long organizationId,
        long projectId,
        long extractionId,
        [FromQuery] string? errorMessage = null)
    {
        try
        {
            await _latticeExtractionBusiness.MarkExtractionFailed(extractionId, errorMessage);
            return Ok();
        }
        catch (InvalidOperationException exc)
        {
            _logger.LogWarning(exc.Message);
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while marking extraction {extractionId} as failed: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Receive the LLM extraction result from Insight and stage it.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="extractionId">The extraction ID returned by the trigger endpoint.</param>
    /// <param name="dataSourceId">The data source the staged records and edges will belong to.</param>
    /// <param name="dto">LLM response payload from Insight.</param>
    [AllowAnonymous]
    [HttpPost("{extractionId:long}/callback", Name = "api_insight_extraction_callback")]
    public async Task<IActionResult> InsightExtractionCallback(
        long organizationId,
        long projectId,
        long extractionId,
        [FromQuery] long dataSourceId)
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body))
            rawBody = await reader.ReadToEndAsync();

        InsightExtractionCallbackDto dto;
        try
        {
            dto = LlmJsonParser.Deserialize<InsightExtractionCallbackDto>(rawBody);
        }
        catch (JsonException exc)
        {
            _logger.LogError(
                "Failed to parse Insight callback for extraction {ExtractionId}: {Error}. Raw body: {RawBody}",
                extractionId, exc.Message, rawBody);
            try
            {
                await _latticeExtractionBusiness.MarkExtractionFailed(
                    extractionId, $"LLM response could not be parsed: {exc.Message}");
            }
            catch (Exception markFailedExc)
            {
                _logger.LogError(markFailedExc,
                    "Failed to mark extraction {ExtractionId} as failed after JSON parse error", extractionId);
            }
            return BadRequest($"Invalid JSON in callback body: {exc.Message}");
        }

        try
        {
            var result = await _latticeExtractionBusiness.ProcessInsightExtractionCallback(
                organizationId, projectId, dataSourceId, extractionId, dto);
            return Ok(result);
        }
        catch (InvalidOperationException exc)
        {
            _logger.LogWarning(exc.Message);
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while staging Insight extraction results: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Returns all staged items for an extraction — classes, records, relationships, and edges —
    ///     with their validation statuses and scores, for human review before approve/reject.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="extractionId">The extraction to retrieve staging data for.</param>
    [HttpGet("{extractionId:long}/staging", Name = "api_get_extraction_staging")]
    public async Task<IActionResult> GetExtractionStaging(
        long organizationId,
        long projectId,
        long extractionId)
    {
        try
        {
            var result = await _latticeExtractionBusiness.GetExtractionStaging(extractionId);
            return Ok(result);
        }
        catch (InvalidOperationException exc)
        {
            _logger.LogWarning(exc.Message);
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving staging data for extraction {extractionId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Approve or reject a completed extraction.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="extractionId">The extraction to promote.</param>
    /// <param name="approve">True to promote all staged items; false to reject.</param>
    [HttpPost("{extractionId:long}/promote", Name = "api_promote_extraction")]
    public async Task<IActionResult> PromoteExtraction(
        long organizationId,
        long projectId,
        long extractionId,
        [FromQuery] bool approve)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var result = await _latticeExtractionBusiness.PromoteExtraction(
                currentUserId, organizationId, projectId, extractionId, approve);
            return Ok(result);
        }
        catch (InvalidOperationException exc)
        {
            _logger.LogWarning(exc.Message);
            return BadRequest(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while promoting extraction {extractionId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Trigger asynchronous Lattice extraction
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="recordId">The ID of the document record to extract from.</param>
    /// <param name="mode">Extraction mode: strict or discovery</param>
    /// <returns>202 Accepted with the extraction_id.</returns>
    [HttpPost("/organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/trigger", Name = "api_trigger_extraction")]
    public async Task<IActionResult> TriggerExtraction(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] string mode)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var extractionId = await _latticeExtractionBusiness.TriggerLatticeExtraction(
                currentUserId, organizationId, projectId, recordId, mode);
            return Accepted(new { extraction_id = extractionId });
        }
        catch (InvalidOperationException exc)
        {
            _logger.LogWarning(exc.Message);
            return BadRequest(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while triggering Lattice extraction: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}
