using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

[ApiController]

[ApiVersion(2)]
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
    ///     Poll the ingestion status endpoint to track progress after this returns 202.
    /// </summary>
    /// <param name="organizationId">ID of the organization.</param>
    /// <param name="projectId">ID of the project.</param>
    /// <param name="vlmModelConfigId">Optional explicit VLM model config ID. Defaults to the project/org default.</param>
    /// <param name="embeddingModelConfigId">Optional explicit embedding model config ID. Defaults to the project/org default.</param>
    /// <param name="dto">Upload payload containing file info.</param>
    /// <returns>202 Accepted with an empty response body once Insight has acknowledged the request.</returns>
    [HttpPost("upload", Name = "api_insight_upload")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("write", "insight")]
    [InsightEnabled]
    public async Task<IActionResult> Upload(
        long organizationId,
        long projectId,
        [FromQuery] long? vlmModelConfigId,
        [FromQuery] long? embeddingModelConfigId,
        [FromBody] InsightUploadApiRequestDto dto)
    {
        var userId = UserContextStorage.UserId;
        var userJwt = UserContextStorage.Token;
        var isAdmin = UserContextStorage.IsSysAdmin || UserContextStorage.IsOrgAdmin || UserContextStorage.IsProjectAdmin;
        await _insightBusiness.QueueInsightUpload(
            userId,
            organizationId,
            projectId,
            vlmModelConfigId,
            embeddingModelConfigId,
            dto,
            userJwt,
            isAdmin);
        return Accepted();
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
    /// <returns>200 OK with a streamed plain-text response containing the Insight query result.</returns>
    [HttpPost("query", Name = "api_insight_query")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
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

        await foreach (var chunk in _insightBusiness.StreamInsightQuery(
                           userId,
                           organizationId,
                           projectId,
                           languageModelConfigId,
                           embeddingModelConfigId,
                           dto,
                           cancellationToken))
        {
            await Response.WriteAsync(chunk, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }



    /// <summary>
    ///     Get the ingestion status for a previously uploaded file.
    /// </summary>
    /// <param name="organizationId">ID of the organization.</param>
    /// <param name="projectId">ID of the project.</param>
    /// <param name="fileId">The Insight file ID to check.</param>
    /// <returns>200 OK with the ingestion status, including chunk count and page count.</returns>
    [HttpGet("ingestion_status/{fileId:long}", Name = "api_insight_ingestion_status")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [InsightEnabled]
    public async Task<ActionResult<InsightIngestionStatusResponseDto>> IngestionStatus(
        long organizationId,
        long projectId,
        long fileId)
    {
        var status = await _insightBusiness.FetchInsightIngestionStatus(fileId);
        return Ok(status);
    }



    /// <summary>
    ///     Get the persistent upload pipeline status for a record.
    /// </summary>
    /// <param name="organizationId">ID of the organization.</param>
    /// <param name="projectId">ID of the project.</param>
    /// <param name="recordId">The record ID whose Insight pipeline status should be checked.</param>
    /// <returns>200 OK with the persistent pipeline status, including stage, worker, progress, and error details.</returns>
    [HttpGet("pipeline_status/{recordId:long}", Name = "api_insight_pipeline_status")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "insight")]
    [Sensitivity("read record")]
    [InsightEnabled]
    public async Task<ActionResult<InsightPipelineStatusResponseDto>> PipelineStatus(
        long organizationId,
        long projectId,
        long recordId)
    {
        var userId = UserContextStorage.UserId;
        var status = await _insightBusiness.FetchInsightPipelineStatus(
            userId,
            organizationId,
            projectId,
            recordId);
        return Ok(status);
    }



    /// <summary>
    ///     Check the health of a configured model endpoint through the Insight service.
    /// </summary>
    /// <param name="organizationId">ID of the organization.</param>
    /// <param name="projectId">ID of the project.</param>
    /// <param name="dto">Endpoint health request containing the model configuration ID and model type.</param>
    /// <returns>200 OK with endpoint health information for the requested model endpoint.</returns>
    [HttpPost("endpoint_health", Name = "api_insight_endpoint_health")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "insight")]
    [InsightEnabled]
    public async Task<ActionResult<InsightEndpointHealthResponseDto>> EndpointHealth(
        long organizationId,
        long projectId,
        [FromBody] InsightEndpointHealthApiRequestDto dto)
    {
        var userId = UserContextStorage.UserId;
        var result = await _insightBusiness.CheckEndpointHealth(
            userId,
            organizationId,
            projectId,
            dto.ModelConfigId,
            dto.ModelType);
        return Ok(result);
    }



    /// <summary>
    ///     Queue embedding jobs for all class and relationship descriptions in the project.
    /// </summary>
    /// <param name="organizationId">ID of the organization.</param>
    /// <param name="projectId">ID of the project whose ontology strings will be embedded.</param>
    /// <param name="embeddingModelConfigId">Optional explicit embedding model config ID. Defaults to the project/org default. If no default is configured, Insight falls back to its own environment defaults.</param>
    /// <returns>202 Accepted with an empty response body once all items have been queued.</returns>
    [HttpPost("embed_strings", Name = "api_insight_embed_strings")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [InsightEnabled]
    public async Task<IActionResult> EmbedStrings(
        long organizationId,
        long projectId,
        [FromQuery] long? embeddingModelConfigId)
    {
        var userId = UserContextStorage.UserId;
        await _insightBusiness.QueueInsightEmbedStrings(
            userId,
            organizationId,
            projectId,
            embeddingModelConfigId);
        return Accepted();
    }
}
