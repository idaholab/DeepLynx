using System.Net;
using System.Text.Json.Nodes;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

[ApiController]
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
    public async Task<ActionResult<AirflowDagListResponseDto>> GetAllDags()
    {
        try
        {
            var dags = await _airflowClient.GetAllDags();
            return Ok(dags);
        }
        catch (HttpRequestException exc)
        {
            return HandleAirflowError(exc, "retrieving DAGs");
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving DAGs: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Check Airflow health
    /// </summary>
    /// <returns>Health details from the configured Airflow instance</returns>
    [HttpGet("health", Name = "api_get_airflow_health")]
    public async Task<ActionResult<JsonObject>> GetHealth()
    {
        try
        {
            var health = await _airflowClient.GetHealth();
            return Ok(health);
        }
        catch (HttpRequestException exc)
        {
            return HandleAirflowError(exc, "checking Airflow health");
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while checking Airflow health: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get details for a DAG
    /// </summary>
    /// <param name="dagId">ID of the DAG</param>
    /// <returns>Details for the requested DAG</returns>
    [HttpGet("dags/{dagId}/details", Name = "api_get_dag_details")]
    public async Task<ActionResult<AirflowDagDto>> GetDagDetails(string dagId)
    {
        try
        {
            var dag = await _airflowClient.GetDagDetails(dagId);
            return Ok(dag);
        }
        catch (HttpRequestException exc)
        {
            return HandleAirflowError(exc, $"retrieving DAG details for '{dagId}'");
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving DAG details for '{dagId}': {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Trigger a DAG Run
    /// </summary>
    /// <param name="dagId">ID of the DAG to trigger</param>
    /// <param name="dto">Optional run configuration (dag_run_id, logical_date, conf, note)</param>
    /// <returns>Details of the triggered DAG run</returns>
    [HttpPost("dags/{dagId}/trigger", Name = "api_trigger_dag_run")]
    public async Task<ActionResult<AirflowDagRunResponseDto>> TriggerDagRun(
        string dagId,
        [FromBody] TriggerDagRunRequestDto dto)
    {
        try
        {
            var dagRun = await _airflowClient.TriggerDagRun(dagId, dto);
            return Ok(dagRun);
        }
        catch (HttpRequestException exc)
        {
            return HandleAirflowError(exc, $"triggering DAG run for '{dagId}'");
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while triggering DAG run for '{dagId}': {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
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
        try
        {
            var dagRun = await _airflowClient.GetDagRun(dagId, dagRunId);
            return Ok(dagRun);
        }
        catch (HttpRequestException exc)
        {
            return HandleAirflowError(exc, $"retrieving DAG run '{dagRunId}' for '{dagId}'");
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving DAG run '{dagRunId}' for '{dagId}': {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

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
