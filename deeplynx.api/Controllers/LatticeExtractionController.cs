using deeplynx.helpers.Context;
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
    ///     Stage extractions from Lattice
    /// </summary>
    /// <param name="organizationId">
    ///     The ID of the organization to which the staged classes, records, edges and relationships
    ///     belong
    /// </param>
    /// <param name="projectId">The ID of the project to which the staged classes, records, edges and relationships belong</param>
    /// <param name="dataSourceId">The ID of the datasource to which the staged records and edges belong</param>
    /// <param name="extractionId">
    ///     When supplied, ties this payload to an existing Extraction created during trigger.
    ///     Lattice passes this as a query param on its success callback.
    /// </param>
    /// <param name="dto">
    ///     CreateExtractionRequestDTO that contains the CreateDTOs for Classes, Records, Edges, and
    ///     Relationships as well as extraction configurations
    /// </param>
    /// <returns>ExtractionResponseDto which contains counts of staged entities</returns>
    /// <exception cref="Exception">Returned if error occurs during extraction transaction</exception>
    /// <remarks>
    ///     All entities are written to the staging schema and associated with a single Extraction record.
    ///     Cross-references within the same payload are resolved automatically:
    ///     - Records can reference classes by class_name (resolved to staging or deeplynx classes)
    ///     - Relationships can reference classes by origin_name / destination_name
    ///     - Edges can reference records by origin_original_id / destination_original_id
    ///     - Edges can reference relationships by relationship_name
    /// </remarks>
    [HttpPost(Name = "api_stage_extractions")]
    public async Task<ActionResult<ExtractionResponseDto>> LatticeExtractionStaging(
        long organizationId,
        long projectId,
        [FromQuery] long dataSourceId,
        [FromQuery] long? extractionId,
        [FromBody] CreateStagingRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var result = await _latticeExtractionBusiness.LatticeEntityStaging(
                currentUserId, organizationId, projectId, dataSourceId, dto, extractionId);
            return Ok(result);
        }
        catch (InvalidOperationException exc)
        {
            _logger.LogWarning(exc.Message);
            return BadRequest(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while staging extractions: {exc}";
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
