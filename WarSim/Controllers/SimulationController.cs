using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WarSim.DTOs;
using WarSim.DTOs.Simulation;
using WarSim.Services;

namespace WarSim.Controllers;

[ApiController]
[Produces("application/json")]
public class SimulationController : ControllerBase
{
    private readonly ISimulationService _simulationService;

    public SimulationController(ISimulationService simulationService)
    {
        _simulationService = simulationService;
    }

    [HttpPost("api/scenarios/{scenarioId:int}/run")]
    [EndpointSummary("Execute an administrative scenario simulation")]
    [EndpointDescription("Performs a deterministic administrative coverage calculation for a scenario with allocations. Persists a SimulationExecuted event audit log. Does not alter equipment inventory or scenario status.")]
    [ProducesResponseType(typeof(SimulationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationResultResponse>> Run(
        int scenarioId,
        CancellationToken cancellationToken)
    {
        var result = await _simulationService.RunSimulationAsync(scenarioId, cancellationToken);
        if (!result.Success)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = result.ErrorMessage
                }),
                ServiceErrorType.Conflict => Conflict(new ProblemDetails
                {
                    Status = 409,
                    Title = "Conflict",
                    Detail = result.ErrorMessage
                }),
                _ => BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Bad Request",
                    Detail = result.ErrorMessage
                })
            };
        }

        return Ok(result.Value);
    }

    [HttpGet("api/scenarios/{scenarioId:int}/events")]
    [EndpointSummary("Retrieve simulation audit events for a scenario")]
    [EndpointDescription("Retrieves the historical audit events recorded for a scenario, sorted in descending chronological order (newest first).")]
    [ProducesResponseType(typeof(IReadOnlyList<SimulationEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SimulationEventResponse>>> GetEvents(
        int scenarioId,
        CancellationToken cancellationToken)
    {
        var result = await _simulationService.GetScenarioEventsAsync(scenarioId, cancellationToken);
        if (!result.Success)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = result.ErrorMessage
                }),
                _ => BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Bad Request",
                    Detail = result.ErrorMessage
                })
            };
        }

        return Ok(result.Value);
    }
}
