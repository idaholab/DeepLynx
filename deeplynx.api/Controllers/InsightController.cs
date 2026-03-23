using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/insight")]
[Authorize]
public class InsightController : ControllerBase
{
    private readonly ILogger<InsightController> _logger;
    private readonly IInsightBusiness _insightBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InsightController" /> class.
    /// </summary>
    /// <param name="insightBusiness">Business logic for proxying requests to the Insight service.</param>
    /// <param name="logger">Error/Info logging interface.</param>
    public InsightController(IInsightBusiness insightBusiness, ILogger<InsightController> logger)
    {
        _insightBusiness = insightBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Queue a document upload for embedding via Insight.
    ///     Insight manages ingestion internally via RabbitMQ — poll the ingestion
    ///     status endpoint to track progress after this returns 200.
    /// </summary>
    /// <param name="organizationId">ID of the organization.</param>
    /// <param name="projectId">ID of the project.</param>
    /// <param name="llmModelConfigId">Optional explicit LLM model config ID. Defaults to the project/org default.</param>
    /// <param name="embeddingModelConfigId">Optional explicit embedding model config ID. Defaults to the project/org default.</param>
    /// <param name="dto">Upload payload containing file info.</param>
    /// <returns>202 Accepted once Insight has acknowledged the request.</returns>
    [HttpPost("upload", Name = "api_insight_upload")]
    public async Task<IActionResult> Upload(
        long organizationId,
        long projectId,
        [FromQuery] long? llmModelConfigId,
        [FromQuery] long? embeddingModelConfigId,
        [FromBody] InsightUploadRequestDto dto)
    {
        try
        {
            var userId = UserContextStorage.UserId;
            await _insightBusiness.QueueInsightUpload(userId, organizationId, projectId, llmModelConfigId, embeddingModelConfigId, dto);
            return Accepted(new { message = "Upload queued. Poll /ingestion_status/{fileId} to track progress." });
        }
        catch (KeyNotFoundException exc)
        {
            _logger.LogError("Model config not found during Insight upload for project {ProjectId}: {Error}", projectId, exc.Message);
            return NotFound(exc.Message);
        }
        catch (InvalidOperationException exc)
        {
            _logger.LogError("Insight upload failed for project {ProjectId}: {Error}", projectId, exc.Message);
            return BadRequest(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while queuing Insight upload for project {projectId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Stream a RAG query response from Insight as plain text chunks.
    ///     The response body is streamed directly. consume it as a readable stream on the client.
    /// </summary>
    /// <param name="organizationId">ID of the organization.</param>
    /// <param name="projectId">ID of the project.</param>
    /// <param name="llmModelConfigId">Optional explicit LLM model config ID. Defaults to the project/org default.</param>
    /// <param name="embeddingModelConfigId">Optional explicit embedding model config ID. Defaults to the project/org default.</param>
    /// <param name="dto">Query payload containing the question, file IDs, and sampling parameters.</param>
    /// <param name="cancellationToken">Propagated from the HTTP request lifecycle.</param>
    [HttpPost("query", Name = "api_insight_query")]
    public async Task Query(
        long organizationId,
        long projectId,
        [FromQuery] long? llmModelConfigId,
        [FromQuery] long? embeddingModelConfigId,
        [FromBody] InsightQueryRequestDto dto,
        CancellationToken cancellationToken)
    {
        var userId = UserContextStorage.UserId;

        Response.ContentType = "text/plain; charset=utf-8";

        try
        {
            await foreach (var chunk in _insightBusiness.StreamInsightQuery(
                               userId, organizationId, projectId,
                               llmModelConfigId, embeddingModelConfigId,
                               dto, cancellationToken))
            {
                await Response.WriteAsync(chunk, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — nothing to do, response is already open.
        }
        catch (KeyNotFoundException exc)
        {
            _logger.LogError("Model config not found during Insight query for project {ProjectId}: {Error}", projectId, exc.Message);

            // Headers are already sent once we start streaming, so we can't change the
            // status code. Write the error into the stream instead so the client knows.
            if (!Response.HasStarted)
                Response.StatusCode = StatusCodes.Status404NotFound;
            else
                await Response.WriteAsync($"\n[error: {exc.Message}]", CancellationToken.None);
        }
        catch (InvalidOperationException exc)
        {
            _logger.LogError("Insight query failed for project {ProjectId}: {Error}", projectId, exc.Message);

            if (!Response.HasStarted)
                Response.StatusCode = StatusCodes.Status400BadRequest;
            else
                await Response.WriteAsync($"\n[error: {exc.Message}]", CancellationToken.None);
        }
        catch (Exception exc)
        {
            _logger.LogError("Unexpected error during Insight query for project {ProjectId}: {Error}", projectId, exc);

            if (!Response.HasStarted)
                Response.StatusCode = StatusCodes.Status500InternalServerError;
            else
                await Response.WriteAsync("\n[error: an unexpected error occurred]", CancellationToken.None);
        }
    }

    /// <summary>
    ///     Get the ingestion status for a previously uploaded file.
    /// </summary>
    /// <param name="organizationId">ID of the organization.</param>
    /// <param name="projectId">ID of the project.</param>
    /// <param name="fileId">The Insight file ID to check.</param>
    /// <returns>Ingestion status including chunk count and page count.</returns>
    [HttpGet("ingestion_status/{fileId:long}", Name = "api_insight_ingestion_status")]
    public async Task<ActionResult<InsightIngestionStatusResponseDto>> IngestionStatus(
        long organizationId,
        long projectId,
        long fileId)
    {
        if (fileId <= 0)
            return BadRequest("fileId must be a positive integer.");

        try
        {
            var status = await _insightBusiness.FetchInsightIngestionStatus(fileId);
            return Ok(status);
        }
        catch (InvalidOperationException exc)
        {
            _logger.LogError("Failed to check ingestion status for file {FileId} in project {ProjectId}: {Error}", fileId, projectId, exc.Message);
            return StatusCode(StatusCodes.Status502BadGateway, exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while checking ingestion status for file {fileId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}