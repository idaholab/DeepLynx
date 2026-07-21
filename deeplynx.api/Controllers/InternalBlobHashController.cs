using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Internal callbacks for Azure-side blob hashing.
/// </summary>
[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/internal/blob-hashes")]
[Authorize]
public class InternalBlobHashController : ControllerBase
{
    private readonly ILogger<InternalBlobHashController> _logger;
    private readonly IRecordBusiness _recordBusiness;

    public InternalBlobHashController(
        IRecordBusiness recordBusiness,
        ILogger<InternalBlobHashController> logger)
    {
        _recordBusiness = recordBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Stores a whole-blob SHA-256 hash for a Nexus-managed Azure blob.
    /// </summary>
    [HttpPost(Name = "api_internal_blob_hash_callback")]
    [Auth("update", "record")]
    public async Task<ActionResult<BlobHashCallbackResponseDto>> StoreBlobHash(
        long organizationId,
        long projectId,
        [FromBody] BlobHashCallbackRequestDto request)
    {
        try
        {
            var response = await _recordBusiness.UpdateFileContentHashFromBlob(
                UserContextStorage.UserId,
                organizationId,
                projectId,
                request);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to store blob hash for organization {OrganizationId}, project {ProjectId}, blob {BlobName}",
                organizationId,
                projectId,
                request.BlobName);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while storing the blob hash" });
        }
    }
}
