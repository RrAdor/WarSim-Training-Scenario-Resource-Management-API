using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WarSim.DTOs;
using WarSim.DTOs.Allocations;
using WarSim.Services;

namespace WarSim.Controllers;

[ApiController]
public class AllocationsController : ControllerBase
{
    private readonly IAllocationService _allocationService;

    public AllocationsController(IAllocationService allocationService)
    {
        _allocationService = allocationService;
    }

    [HttpPost("api/scenarios/{scenarioId:int}/allocations")]
    public async Task<ActionResult<AllocationResponse>> Create(
        int scenarioId,
        [FromBody] CreateAllocationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _allocationService.CreateAsync(scenarioId, request, cancellationToken);
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

        return CreatedAtAction(
            nameof(GetByScenario),
            new { scenarioId = result.Value!.ScenarioId },
            result.Value);
    }

    [HttpGet("api/scenarios/{scenarioId:int}/allocations")]
    public async Task<ActionResult<IReadOnlyList<AllocationResponse>>> GetByScenario(
        int scenarioId,
        CancellationToken cancellationToken)
    {
        var result = await _allocationService.GetByScenarioIdAsync(scenarioId, cancellationToken);
        if (!result.Success)
        {
            return NotFound(new ProblemDetails
            {
                Status = 404,
                Title = "Not Found",
                Detail = result.ErrorMessage
            });
        }

        return Ok(result.Value);
    }

    [HttpDelete("api/allocations/{allocationId:int}")]
    public async Task<IActionResult> Delete(
        int allocationId,
        CancellationToken cancellationToken)
    {
        var result = await _allocationService.DeleteAsync(allocationId, cancellationToken);
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

        return NoContent();
    }
}

