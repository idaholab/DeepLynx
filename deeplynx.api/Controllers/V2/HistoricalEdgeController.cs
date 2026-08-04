using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using Asp.Versioning;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for managing historical edges.
/// </summary>
/// <remarks>
///     This controller provides endpoints to retrieve historical edge information and edge history.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/edges/historical")]
[Authorize]
[Tags("Historical Edge")]
public class HistoricalEdgeController : ControllerBase
{
    private readonly IHistoricalEdgeBusiness _historicalEdgeBusiness;
    private readonly ILogger<HistoricalEdgeController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HistoricalEdgeController" /> class
    /// </summary>
    /// <param name="historicalEdgeBusiness">The business logic interface for handling historical edge operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public HistoricalEdgeController(IHistoricalEdgeBusiness historicalEdgeBusiness,
        ILogger<HistoricalEdgeController> logger)
    {
        _historicalEdgeBusiness = historicalEdgeBusiness;
        _logger = logger;
    }



    /// <summary>
    ///     Get All Historical Edges
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose historical edges are to be retrieved</param>
    /// <param name="dataSourceId">(Optional) The ID of the datasource by which to filter edges</param>
    /// <param name="pointInTime">(Optional) Find the most current edges that existed before this point in time</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived edges from the result (Default true)</param>
    /// <returns>A list of historical edges based on the applied filters.</returns>
    [HttpGet(Name = "api_get_all_historical_edges")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "edge")]
    public async Task<ActionResult<IEnumerable<HistoricalEdgeResponseDto>>> GetAllHistoricalEdges(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId = null,
        [FromQuery] DateTime? pointInTime = null,
        [FromQuery] bool hideArchived = true)
    {
            var edges = await _historicalEdgeBusiness.GetAllHistoricalEdges(projectId, dataSourceId, pointInTime,
                hideArchived);
            return Ok(edges);
    }



    /// <summary>
    ///     Get a Historical Edge by ID
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the edge belongs</param>
    /// <param name="edgeId">The ID of the edge to retrieve</param>
    /// <param name="pointInTime">(Optional) Find the most current edge that existed before this point in time</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived edges from the result (Default true)</param>
    /// <returns>The historical edge at the specified point in time</returns>
    [HttpGet("{edgeId:long}", Name = "api_get_historical_edge_by_id")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "edge")]
    public async Task<ActionResult<HistoricalEdgeResponseDto>> GetHistoricalEdgeById(
        long organizationId,
        long projectId,
        long edgeId,
        [FromQuery] DateTime? pointInTime = null,
        [FromQuery] bool hideArchived = true)
    {
            var edge = await _historicalEdgeBusiness.GetHistoricalEdge(organizationId, edgeId, null, null, pointInTime,
                hideArchived);
            return Ok(edge);
    }



    /// <summary>
    ///     Get a Historical Edge by Origin and Destination
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the edge belongs</param>
    /// <param name="originId">The origin ID of the edge to retrieve</param>
    /// <param name="destinationId">The destination ID of the edge to retrieve</param>
    /// <param name="pointInTime">(Optional) Find the most current edge that existed before this point in time</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived edges from the result (Default true)</param>
    /// <returns>The historical edge at the specified point in time</returns>
    [HttpGet("by-relationship", Name = "api_get_historical_edge_by_relationship")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "edge")]
    public async Task<ActionResult<HistoricalEdgeResponseDto>> GetHistoricalEdgeByRelationship(
        long organizationId,
        long projectId,
        [FromQuery] long originId,
        [FromQuery] long destinationId,
        [FromQuery] DateTime? pointInTime = null,
        [FromQuery] bool hideArchived = true)
    {
            var edge = await _historicalEdgeBusiness.GetHistoricalEdge(organizationId, null, originId, destinationId,
                pointInTime,
                hideArchived);
            return Ok(edge);
    }



    /// <summary>
    ///     Get Edge History by ID
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the edge belongs</param>
    /// <param name="edgeId">The ID of the edge for which to retrieve history</param>
    /// <returns>A list of all previous versions of the edge</returns>
    [HttpGet("{edgeId:long}/history", Name = "api_get_edge_history_by_id")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "edge")]
    public async Task<ActionResult<IEnumerable<HistoricalEdgeResponseDto>>> GetEdgeHistoryById(
        long organizationId,
        long projectId,
        long edgeId)
    {
            var history = await _historicalEdgeBusiness.GetHistoryForEdge(organizationId, edgeId, null, null);
            return Ok(history);
    }



    /// <summary>
    ///     Get Edge History by Origin and Destination
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the edge belongs</param>
    /// <param name="originId">The origin ID of the edge for which to retrieve history</param>
    /// <param name="destinationId">The destination ID of the edge for which to retrieve history</param>
    /// <returns>A list of all previous versions of the edge</returns>
    [HttpGet("by-relationship/history", Name = "api_get_edge_history_by_relationship")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "edge")]
    public async Task<ActionResult<IEnumerable<HistoricalEdgeResponseDto>>> GetEdgeHistoryByRelationship(
        long organizationId,
        long projectId,
        [FromQuery] long originId,
        [FromQuery] long destinationId)
    {
            var history =
                await _historicalEdgeBusiness.GetHistoryForEdge(organizationId, null, originId, destinationId);
            return Ok(history);
    }
}