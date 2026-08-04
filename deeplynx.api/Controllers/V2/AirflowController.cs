using System.Net;
using System.Text.Json.Nodes;
using Asp.Versioning;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

[ApiController]

[ApiVersion(2)]
[Route("airflow")]
[Authorize]
[ForbidServiceAccounts] // service accounts can only act on the project level
public class AirflowController : ControllerBase
{
    private readonly AirflowServiceClient _airflowClient;
    private readonly ILogger<AirflowController> _logger;

    public AirflowController(AirflowServiceClient airflowClient, ILogger<AirflowController> logger)
    {
        _airflowClient = airflowClient;
        _logger = logger;
    }


    
    /// <summary>
    ///     Get All Available DAGs
    /// </summary>
    /// <returns>List of all DAGs available in the Airflow instance</returns>
    [HttpGet("dags", Name = "api_get_all_dags")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<AirflowDagListResponseDto>> GetAllDags()
    {
        var dags = await _airflowClient.GetAllDags();
        return Ok(dags);
    }


    
    /// <summary>
    ///     Check Airflow health
    /// </summary>
    /// <returns>Health details from the configured Airflow instance</returns>
    [HttpGet("health", Name = "api_get_airflow_health")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<JsonObject>> GetHealth()
    {
        var health = await _airflowClient.GetHealth();
        return Ok(health);
    }


    
    /// <summary>
    ///     Get details for a DAG
    /// </summary>
    /// <param name="dagId">ID of the DAG</param>
    /// <returns>Details for the requested DAG</returns>
    [HttpGet("dags/{dagId}/details", Name = "api_get_dag_details")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<AirflowDagDto>> GetDagDetails(string dagId)
    {
        var dag = await _airflowClient.GetDagDetails(dagId);
        return Ok(dag);
    }


    
    /// <summary>
    ///     Trigger a DAG Run
    /// </summary>
    /// <param name="dagId">ID of the DAG to trigger</param>
    /// <param name="dto">Optional run configuration (dag_run_id, logical_date, conf, note)</param>
    /// <returns>Details of the triggered DAG run</returns>
    [HttpPost("dags/{dagId}/trigger", Name = "api_trigger_dag_run")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<AirflowDagRunResponseDto>> TriggerDagRun(
        string dagId,
        [FromBody] TriggerDagRunRequestDto dto)
    {
        var dagRun = await _airflowClient.TriggerDagRun(dagId, dto);
        return Ok(dagRun);
    }


    
    /// <summary>
    ///     Get a DAG run
    /// </summary>
    /// <param name="dagId">ID of the DAG</param>
    /// <param name="dagRunId">ID of the DAG run</param>
    /// <returns>Details of the requested DAG run</returns>
    [HttpGet("dags/{dagId}/runs/{dagRunId}", Name = "api_get_dag_run")]
    public async Task<ActionResult<AirflowDagRunResponseDto>> GetDagRun(
        string dagId,
        string dagRunId)
    {
        var dagRun = await _airflowClient.GetDagRun(dagId, dagRunId);
        return Ok(dagRun);
    }

    
    //TODO: Remove when V1 api is deprecated https://nstinl.atlassian-us-gov-mod.net/browse/DL-2642
    private ObjectResult HandleAirflowError(HttpRequestException exc, string context)
    {
        var statusCode = exc.StatusCode switch
        {
            HttpStatusCode.Unauthorized => StatusCodes.Status401Unauthorized,
            HttpStatusCode.Forbidden => StatusCodes.Status403Forbidden,
            HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status502BadGateway
        };

        var message = $"Airflow error while {context}: {exc.Message}";
        _logger.LogError(message);
        return StatusCode(statusCode, message);
    }
}
