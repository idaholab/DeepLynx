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
[Tags("Extraction")]
public class ExtractionController : ControllerBase
{
    private readonly IExtractionBusiness _extractionBusiness;
    private readonly ILogger<ExtractionController> _logger;

    public ExtractionController(IExtractionBusiness extractionBusiness, ILogger<ExtractionController> logger)
    {
        _extractionBusiness = extractionBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Create an extraction job
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
    [HttpPost(Name = "api_create_an_extraction")]
    public async Task<ActionResult<ExtractionResponseDto>> CreateExtraction(
        long organizationId,
        long projectId,
        [FromQuery] long dataSourceId,
        [FromBody] CreateExtractionRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var result = await _extractionBusiness.CreateExtraction(
                currentUserId, organizationId, projectId, dataSourceId, dto);
            return Ok(result);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating extraction: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}