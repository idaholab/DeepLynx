using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

[ApiController]
[Route("projects/{projectId:long}/lattice")]
[Authorize]
[Tags("Lattice")]
public class LatticeController : ControllerBase
{
    private readonly ILogger<LatticeController> _logger;
    private readonly IClassBusiness _classBusiness;
    private readonly IRelationshipBusiness _relationshipBusiness;
    private readonly IRecordBusiness _recordBusiness;
    private readonly IEdgeBusiness _edgeBusiness;

    public LatticeController(
        IClassBusiness classBusiness,
        IRelationshipBusiness relationshipBusiness,
        IRecordBusiness recordBusiness,
        IEdgeBusiness edgeBusiness,
        ILogger<LatticeController> logger
    ) {
        _classBusiness = classBusiness;
        _relationshipBusiness = relationshipBusiness;
        _recordBusiness = recordBusiness;
        _edgeBusiness = edgeBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Textual Summary of Classes in the Ontology
    /// </summary>
    /// <param name="projectId">The ID of the project to which the classes belong</param>
    /// <returns>Table of descriptive class fields</returns>
    [HttpGet("classes", Name = "api_get_lattice_classes_project")]
    [Auth("read", "class")]
    public async Task<ActionResult<IEnumerable<LatticeClassDto>>> GetLatticeClasses(long projectId)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var classes = await _classBusiness.GetLatticeClasses(organizationId, projectId);
            return Ok(classes);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching Lattice class descriptions: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Textual Summary of Relationships in the Ontology
    /// </summary>
    /// <param name="projectId">The ID of the project to which the relationships belong</param>
    /// <returns>Table of descriptive relationship fields including class names</returns>
    [HttpGet("relationships", Name = "api_get_lattice_relationships")]
    [Auth("read", "relationship")]
    public async Task<ActionResult<IEnumerable<LatticeRelationshipDto>>> GetLatticeRelationships(long projectId)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var relationships = await _relationshipBusiness.GetLatticeRelationships(organizationId, projectId);
            return Ok(relationships);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching Lattice relationship descriptions: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Textual Summary of Records in the Knowledge Graph
    /// </summary>
    /// <param name="projectId">The ID of the project to which the records belong</param>
    /// <returns>Table of descriptive record fields including class name</returns>
    [HttpGet("records", Name = "api_get_lattice_records_project")]
    [Auth("read", "record")]
    public async Task<ActionResult<IEnumerable<LatticeRecordDto>>> GetLatticeRecords(long projectId)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var records = await _recordBusiness.GetLatticeRecords(organizationId, projectId);
            return Ok(records);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching Lattice record descriptions: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Textual Summary of Edges in the Knowledge Graph
    /// </summary>
    /// <param name="projectId">The ID of the project to which the edges belong</param>
    /// <returns>Table of descriptive edge fields including class, record, and relationship names</returns>
    [HttpGet("edges", Name = "api_get_lattice_dges_project")]
    [Auth("read", "edge")]
    [Auth("read", "record")]
    public async Task<ActionResult<IEnumerable<LatticeEdgeDto>>> GetLatticeEdges(long projectId)
    {
        try
        {
            var organizationId = UserContextStorage.OrganizationId;
            var edges = await _edgeBusiness.GetLatticeEdges(organizationId, projectId);
            return Ok(edges);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while fetching Lattice edge descriptions: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}