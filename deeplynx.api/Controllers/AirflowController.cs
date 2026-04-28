using System.Net;
using deeplynx.models;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

[ApiController]
[Route("airflow")]
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
