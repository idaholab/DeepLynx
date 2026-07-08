using deeplynx.interfaces;
using deeplynx.models.ResponseDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using deeplynx.models;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for retrieving provenance records.
/// </summary>
/// <remarks>
///     This controller provides endpoints to retrieve individual provenance records and provenance history for a record.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/records/provenance")]
[Authorize]
[Tags("Provenance")]
public class ProvenanceController : ControllerBase
{
    private readonly IProvenanceBusiness _provenanceBusiness;
    private readonly ILogger<ProvenanceController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProvenanceController" /> class
    /// </summary>
    /// <param name="provenanceBusiness">The business logic interface for handling provenance operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public ProvenanceController(IProvenanceBusiness provenanceBusiness,
        ILogger<ProvenanceController> logger)
    {
        _provenanceBusiness = provenanceBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get a Provenance Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="provenanceRecordId">The database ID of the provenance record to retrieve</param>
    /// <returns>The matching provenance record</returns>
    [HttpGet("{provenanceRecordId:long}", Name = "api_get_a_provenance_record")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<ProvenanceRecordResponseDto>> GetProvenanceRecord(
        long organizationId,
        long projectId,
        long provenanceRecordId)
    {
        try
        {
            var provenanceRecord = await _provenanceBusiness.GetProvenanceRecord(provenanceRecordId);
            return Ok(provenanceRecord);
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving provenance record {provenanceRecordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Provenance History
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record for which to retrieve provenance history</param>
    /// <returns>A list of all provenance records for the given record, most recent first</returns>
    [HttpGet("{recordId:long}/history", Name = "api_get_provenance_history")]
    [Auth("read", "record")]
    [Sensitivity("read record")]
    public async Task<ActionResult<ProvenanceHistoryResponseDto>> GetProvenanceHistory(
        long organizationId,
        long projectId,
        long recordId)
    {
        try
        {
            var history = await _provenanceBusiness.GetProvenanceHistory(recordId);
            return Ok(history);
        }
        catch (KeyNotFoundException exc)
        {
            return NotFound(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving provenance history for record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}