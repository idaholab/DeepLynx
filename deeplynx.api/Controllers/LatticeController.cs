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
public class LatticeController : ControllerBase
{
    private readonly IExtractionBusiness _extractionBusiness;
    private readonly IInsightBusiness _insightBusiness;
    private readonly ILatticeOrchestrationBusiness _latticeOrchestration;
    private readonly ILogger<LatticeController> _logger;

    public LatticeController(
        IExtractionBusiness extractionBusiness,
        IInsightBusiness insightBusiness,
        ILatticeOrchestrationBusiness latticeOrchestration,
        ILogger<LatticeController> logger)
    {
        _extractionBusiness = extractionBusiness;
        _insightBusiness = insightBusiness;
        _latticeOrchestration = latticeOrchestration;
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
        [FromBody] CreateStagingRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var result = await _extractionBusiness.LatticeEntityStaging(
                currentUserId, organizationId, projectId, dataSourceId, dto);
            return Ok(result);
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
    ///     Queues descriptions for embedding via the Insight service. Must be called before
    ///     using the ontology similarity search endpoint.
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
    ///     Search for the most similar ontology classes and/or relationships in the project
    ///     using cosine similarity against a record's stored embeddings.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project to scope the ontology search to.</param>
    /// <param name="recordId">The ID of the record whose embeddings are used as the query.</param>
    /// <param name="limit">Maximum number of results to return. Defaults to 5.</param>
    /// <param name="type">Optional filter: "class" or "relationship". Omit to return both.</param>
    /// <returns>
    ///     A list of ontology matches ordered by similarity score descending.
    ///     Each result includes: name, technical_id, type, description, score.
    /// </returns>
    [HttpGet("records/{recordId:long}/ontology-similarity", Name = "api_ontology_similarity")]
    public async Task<ActionResult<List<OntologySimilarityResultDto>>> OntologySimilarity(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] int limit = 5,
        [FromQuery] string? type = null)
    {
        try
        {
            var results = await _extractionBusiness.SearchOntologySimilarity(
                recordId, projectId, limit, type);
            return Ok(results);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred during ontology similarity search: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Trigger asynchronous entity extraction on a document record via the Lattice service.
    ///     Creates an Extraction record (status: pending → running) and returns immediately.
    ///     Lattice processes the extraction and calls back via the staging endpoint when complete.
    ///     NEXUS_BASE_URL and NEXUS_SERVICE_TOKEN environment variables must be set so Lattice
    ///     can authenticate its callback.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="recordId">The ID of the document record to extract from.</param>
    /// <param name="dto">Extraction configuration: data_source_id, mode, similarity_limit.</param>
    /// <returns>202 Accepted with the extraction_id to poll for status.</returns>
    [HttpPost("records/{recordId:long}/trigger", Name = "api_trigger_extraction")]
    public async Task<IActionResult> TriggerExtraction(
        long organizationId,
        long projectId,
        long recordId,
        [FromBody] TriggerExtractionRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var extractionId = await _latticeOrchestration.TriggerLatticeExtraction(
                currentUserId, organizationId, projectId, dto.DataSourceId, recordId, dto.Mode, dto.SimilarityLimit);
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
