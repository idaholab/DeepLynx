using deeplynx.helpers;
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
    private readonly IInsightBusiness _insightBusiness;
    private readonly ILogger<InsightController> _logger;

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
    ///     Insight manages ingestion internally via RabbitMQ.
    ///     Poll the ingestion status endpoint to track progress after this returns 200.
    /// </summary>
    /// <param name="organizationId">ID of the organization.</param>
    /// <param name="projectId">ID of the project.</param>
    /// <param name="vlmModelConfigId">Optional explicit VLM model config ID. Defaults to the project/org default.</param>
    /// <param name="embeddingModelConfigId">Optional explicit embedding model config ID. Defaults to the project/org default.</param>
    /// <param name="dto">Upload payload containing file info.</param>
    /// <returns>202 Accepted once Insight has acknowledged the request.</returns>
    [HttpPost("upload", Name = "api_insight_upload")]
    [Auth("write", "insight")]
    [InsightEnabled]
    public async Task<IActionResult> Upload(
        long organizationId,
        long projectId,
        [FromQuery] long? vlmModelConfigId,
        [FromQuery] long? embeddingModelConfigId,
        [FromBody] InsightUploadApiRequestDto dto)
    {
        try
        {
            var userId = UserContextStorage.UserId;
            var userJwt = UserContextStorage.Token;
            await _insightBusiness.QueueInsightUpload(userId, organizationId, projectId, vlmModelConfigId, embeddingModelConfigId, dto, userJwt);
            return Accepted(new { message = "Upload queued. Poll /ingestion_status/{fileId} to track progress." });
        }
        catch (KeyNotFoundException exc)
        {
            _logger.LogError("Model config not found during Insight upload for project {ProjectId}: {Error}", projectId,
                exc.Message);
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
    ///     The response body is streamed directly. Consume it as a readable stream on the client.
    /// </summary>
    /// <param name="organizationId">ID of the organization.</param>
    /// <param name="projectId">ID of the project.</param>
    /// <param name="languageModelConfigId">
    ///     Optional explicit language model config ID. Language Model Type can be LLM or VLM.
    ///     Defaults to the project/org LLM default, or VLM default if no LLM configured.
    /// </param>
    /// <param name="embeddingModelConfigId">Optional explicit embedding model config ID. Defaults to the project/org default.</param>
    /// <param name="dto">Query payload containing the question, file IDs, and sampling parameters.</param>
    /// <param name="cancellationToken">Propagated from the HTTP request lifecycle.</param>
    [HttpPost("query", Name = "api_insight_query")]
    [Auth("read", "insight")]
    [Sensitivity("read record")]
    [InsightEnabled]
    public async Task Query(
        long organizationId,
        long projectId,
        [FromQuery] long? languageModelConfigId,
        [FromQuery] long? embeddingModelConfigId,
        [FromBody] InsightQueryApiRequestDto dto,
        CancellationToken cancellationToken)
    {
        var userId = UserContextStorage.UserId;

        Response.ContentType = "text/plain; charset=utf-8";

        try
        {
            await foreach (var chunk in _insightBusiness.StreamInsightQuery(
                               userId, organizationId, projectId,
                               languageModelConfigId, embeddingModelConfigId,
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
            _logger.LogError("Model config not found during Insight query for project {ProjectId}: {Error}", projectId,
                exc.Message);

            // Headers are already sent once streaming is started, can't change the status code.
            // Write the error into the stream instead so the client knows.
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
    [InsightEnabled]
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
            _logger.LogError("Failed to check ingestion status for file {FileId} in project {ProjectId}: {Error}",
                fileId, projectId, exc.Message);
            return StatusCode(StatusCodes.Status502BadGateway, exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while checking ingestion status for file {fileId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Queue embedding jobs for all class and relationship descriptions in the project.
    /// </summary>
    /// <param name="organizationId">ID of the organization.</param>
    /// <param name="projectId">ID of the project whose ontology strings will be embedded.</param>
    /// <param name="embeddingModelConfigId">Optional explicit embedding model config ID. Defaults to the project/org default. If no default is configured, Insight falls back to its own environment defaults.</param>
    /// <returns>202 Accepted once all items have been queued.</returns>
    [HttpPost("embed_strings", Name = "api_insight_embed_strings")]
    [InsightEnabled]
    public async Task<IActionResult> EmbedStrings(
        long organizationId,
        long projectId,
        [FromQuery] long? embeddingModelConfigId)
    {
        try
        {
            var userId = UserContextStorage.UserId;
            await _insightBusiness.QueueInsightEmbedStrings(userId, organizationId, projectId,
                embeddingModelConfigId);
            return Accepted(new { message = "Ontology embedding queued." });
        }
        catch (KeyNotFoundException exc)
        {
            _logger.LogError("Model config not found during Insight upload for project {ProjectId}: {Error}", projectId,
                exc.Message);
            return NotFound(exc.Message);
        }
        catch (InvalidOperationException exc)
        {
            _logger.LogError("Ontology embeddings failed for project {ProjectId}: {Error}", projectId, exc.Message);
            return BadRequest(exc.Message);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while queuing Insight ontology embeddings for project {projectId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}