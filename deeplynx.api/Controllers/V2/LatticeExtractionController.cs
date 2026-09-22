using System.Text.Json;
using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.helpers.json;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for managing AI extractions into the staging schema.
/// </summary>
/// <remarks>
///     Extractions hold staged records, classes, relationships, and edges awaiting human approval.
///     Once approved, they are promoted into the deeplynx schema.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/extractions")]
[Authorize]
[Tags("Lattice")]
public class LatticeExtractionController : ControllerBase
{
    private readonly IInsightBusiness _insightBusiness;
    private readonly ILatticeExtractionBusiness _latticeExtractionBusiness;
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

    private static IActionResult StructuredTriggerFailure(ControllerBase controller, InvalidOperationException exc)
    {
        if (exc.Message.Contains("Embeddings are being generated", StringComparison.OrdinalIgnoreCase))
        {
            return controller.Conflict(new
            {
                error = "embeddings_not_ready",
                message = exc.Message
            });
        }

        if (exc.Message.Contains("sufficient ontology", StringComparison.OrdinalIgnoreCase))
        {
            return controller.BadRequest(new
            {
                error = "ontology_not_ready",
                message = exc.Message
            });
        }

        if (exc.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return controller.NotFound(new
            {
                error = "record_not_found",
                message = exc.Message
            });
        }

        return controller.BadRequest(new
        {
            error = "lattice_trigger_invalid",
            message = exc.Message
        });
    }

    private static string? ExtractFailureMessage(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return null;

        var trimmedBody = rawBody.Trim();
        if (trimmedBody.StartsWith('{'))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<LatticeExtractionErrorDto>(trimmedBody);
                if (!string.IsNullOrWhiteSpace(dto?.Detail)) return dto.Detail.Trim();
                if (!string.IsNullOrWhiteSpace(dto?.Error)) return dto.Error.Trim();
            }
            catch (JsonException)
            {
                return trimmedBody;
            }
        }

        return trimmedBody;
    }

    private async Task<string?> ReadFailureMessage(string? queryMessage)
    {
        if (!string.IsNullOrWhiteSpace(queryMessage)) return queryMessage.Trim();

        var request = ControllerContext.HttpContext?.Request;
        if (request?.Body == null) return null;

        if (request.Body.CanSeek) request.Body.Position = 0;

        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();

        if (request.Body.CanSeek) request.Body.Position = 0;

        return ExtractFailureMessage(rawBody);
    }



    /// <summary>
    ///     Returns all extractions for the specified project.
    /// </summary>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="paginatedRequestDto"> Pagination parameters</param>
    /// <returns>200 OK with a list of extractions belonging to the project.</returns>
    [HttpGet(Name = "api_list_extractions")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [InsightEnabled]
    public async Task<IActionResult> ListExtractions(
        long projectId,
        [FromQuery] PaginatedRequestDto? paginatedRequestDto = null)
    {
        paginatedRequestDto ??= new PaginatedRequestDto();
        var result = await _latticeExtractionBusiness.ListExtractionsByProjectPaginated(projectId, paginatedRequestDto);
        return Ok(result);
    }



    /// <summary>
    ///     Return the ontology embedding status.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <returns>200 OK with the ontology embedding status for the project.</returns>
    [HttpGet("embedding-status", Name = "api_get_embedding_status")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [InsightEnabled]
    public async Task<IActionResult> GetEmbeddingStatus(long organizationId, long projectId)
    {
        var result = await _latticeExtractionBusiness.GetEmbeddingStatus(projectId);
        return Ok(result);
    }



    /// <summary>
    ///     Trigger ontology embedding.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project whose ontology will be embedded.</param>
    /// <param name="embeddingModelConfigId">
    ///     Optional embedding model config ID. If omitted, the project/org default is used.
    /// </param>
    /// <returns>202 Accepted with an empty response body once ontology embedding has been queued.</returns>
    [HttpPost("embed-ontology", Name = "api_embed_ontology")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [InsightEnabled]
    public async Task<IActionResult> EmbedOntology(
        long organizationId,
        long projectId,
        [FromQuery] long? embeddingModelConfigId = null)
    {
        var currentUserId = UserContextStorage.UserId;
        await _insightBusiness.QueueInsightEmbedStrings(
            currentUserId,
            organizationId,
            projectId,
            embeddingModelConfigId);
        return Accepted();
    }



    /// <summary>
    ///     Mark an extraction as failed.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="extractionId">The extraction ID returned by the trigger endpoint.</param>
    /// <param name="errorMessage">Optional error message from Insight describing the failure.</param>
    /// <returns>202 Accepted with an empty response body once the extraction has been marked as failed.</returns>
    [AllowAnonymous]
    [HttpPost("{extractionId:long}/failure", Name = "api_insight_extraction_failure")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [InsightEnabled]
    public async Task<IActionResult> InsightExtractionFailure(
        long organizationId,
        long projectId,
        long extractionId,
        [FromQuery] string? errorMessage = null)
    {
        var failureMessage = await ReadFailureMessage(errorMessage);
        await _latticeExtractionBusiness.MarkExtractionFailed(
            extractionId,
            organizationId,
            projectId,
            failureMessage);
        return Accepted();
    }



    /// <summary>
    ///     Receive the LLM extraction result from Insight and stage it.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="extractionId">The extraction ID returned by the trigger endpoint.</param>
    /// <param name="dataSourceId">The data source the staged records and edges will belong to.</param>
    /// <returns>200 OK with the staged extraction result.</returns>
    [AllowAnonymous]
    [HttpPost("{extractionId:long}/callback", Name = "api_insight_extraction_callback")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [InsightEnabled]
    public async Task<IActionResult> InsightExtractionCallback(
        long organizationId,
        long projectId,
        long extractionId,
        [FromQuery] long dataSourceId)
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        var dto = LlmJsonParser.Deserialize<InsightExtractionCallbackDto>(rawBody);
        var result = await _latticeExtractionBusiness.ProcessInsightCallback(
            organizationId,
            projectId,
            dataSourceId,
            extractionId,
            dto);
        return Ok(result);
    }

    /// <summary>
    ///     Receive progress updates from Insight during the extraction process.
    /// </summary>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="extractionId">The ID of the extraction.</param>
    /// <param name="progressDto">The progress of the extraction.</param>
    /// <returns>200 OK when progress update is successfully recorded.</returns>
    [AllowAnonymous]
    [HttpPost("{extractionId:long}/progress", Name = "api_insight_extraction_progress")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [InsightEnabled]
    public async Task<IActionResult> InsightExtractionProgress(
        long projectId,
        long extractionId,
        [FromBody] InsightExtractionProgressCombinedDto progressDto)
    {

        var result = await _latticeExtractionBusiness.ProcessExtractionProgress(
            projectId,
            extractionId,
            progressDto
        );

        return Ok(result);
    }



    /// <summary>
    ///     Returns all staged items for an extraction.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="extractionId">The extraction to retrieve staging data for.</param>
    /// <returns>200 OK with all staged classes, records, relationships, and edges for the extraction.</returns>
    [HttpGet("{extractionId:long}/staging", Name = "api_get_extraction_staging")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [InsightEnabled]
    public async Task<IActionResult> GetExtractionStaging(
        long organizationId,
        long projectId,
        long extractionId)
    {
        var result = await _latticeExtractionBusiness.GetExtractionStaging(
            extractionId,
            organizationId,
            projectId);
        return Ok(result);
    }



    /// <summary>
    ///     Promote a selected subset of an extraction's staged items.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="extractionId">The extraction to promote.</param>
    /// <param name="request">The selection of staged items to promote.</param>
    /// <returns>200 OK with the updated extraction after the selected items are promoted.</returns>
    [HttpPost("{extractionId:long}/promote", Name = "api_promote_extraction")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [InsightEnabled]
    public async Task<IActionResult> PromoteExtraction(
        long organizationId,
        long projectId,
        long extractionId,
        [FromBody] PromoteExtractionRequestDto request)
    {
        var currentUserId = UserContextStorage.UserId;
        var result = await _latticeExtractionBusiness.PromoteExtraction(
            currentUserId,
            organizationId,
            projectId,
            extractionId,
            request);
        return Ok(result);
    }



    /// <summary>
    ///     Reject a selected subset of an extraction's staged items, or every remaining item.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="extractionId">The extraction to reject items from.</param>
    /// <param name="request">The selection of staged items to reject.</param>
    /// <returns>200 OK with the updated extraction after the selected items are rejected.</returns>
    [HttpPost("{extractionId:long}/reject", Name = "api_reject_extraction")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<IActionResult> RejectExtraction(
        long organizationId,
        long projectId,
        long extractionId,
        [FromBody] RejectExtractionRequestDto request)
    {
        var result = await _latticeExtractionBusiness.RejectExtraction(extractionId, request);
        return Ok(result);
    }



    /// <summary>
    ///     Trigger asynchronous Lattice extraction.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="recordId">The ID of the document record to extract from.</param>
    /// <param name="mode">Extraction mode: strict or discovery.</param>
    /// <returns>202 Accepted with the extraction ID in the response body.</returns>
    [HttpPost("/organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/trigger",
        Name = "api_trigger_extraction")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [InsightEnabled]
    public async Task<IActionResult> TriggerExtraction(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] string mode)
    {
        var currentUserId = UserContextStorage.UserId;
        var extractionId = await _latticeExtractionBusiness.TriggerLatticeExtraction(
            currentUserId,
            organizationId,
            projectId,
            recordId,
            mode);
        return Accepted(extractionId);
    }
}
