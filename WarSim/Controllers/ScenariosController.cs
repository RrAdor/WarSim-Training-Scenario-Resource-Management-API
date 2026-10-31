using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WarSim.DTOs;
using WarSim.Services;

namespace WarSim.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ScenariosController : ControllerBase
{
    private readonly IScenarioService _scenarioService;

    public ScenariosController(IScenarioService scenarioService)
    {
        _scenarioService = scenarioService;
    }

    [HttpPost]
    [EndpointSummary("Create a new training scenario")]
    [EndpointDescription("Creates a new fictional scenario with status 'Draft'. Name must be between 1 and 120 characters.")]
    [ProducesResponseType(typeof(ScenarioResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScenarioResponse>> Create(
        [FromBody] CreateScenarioRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _scenarioService.CreateAsync(request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Bad Request",
                Detail = result.ErrorMessage
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet]
    [EndpointSummary("List all scenarios")]
    [EndpointDescription("Retrieves all training scenarios ordered by ID.")]
    [ProducesResponseType(typeof(IReadOnlyList<ScenarioResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScenarioResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var scenarios = await _scenarioService.GetAllAsync(cancellationToken);
        return Ok(scenarios);
    }

    [HttpGet("{id:int}")]
    [EndpointSummary("Get scenario by ID")]
    [EndpointDescription("Retrieves the details of a specific scenario by its ID.")]
    [ProducesResponseType(typeof(ScenarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScenarioResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var scenario = await _scenarioService.GetByIdAsync(id, cancellationToken);
        if (scenario == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = 404,
                Title = "Not Found",
                Detail = $"Scenario with ID {id} was not found."
            });
        }

        return Ok(scenario);
    }

    [HttpPut("{id:int}")]
    [EndpointSummary("Update a draft scenario")]
    [EndpointDescription("Updates the name and description of an existing draft scenario. Only scenarios in 'Draft' status can be edited.")]
    [ProducesResponseType(typeof(ScenarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScenarioResponse>> Update(
        int id,
        [FromBody] UpdateScenarioRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _scenarioService.UpdateAsync(id, request, cancellationToken);
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

    [HttpDelete("{id:int}")]
    [EndpointSummary("Delete a scenario")]
    [EndpointDescription("Deletes a scenario. Rejects with 400 Bad Request if dependent allocations or simulation events exist.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _scenarioService.DeleteAsync(id, cancellationToken);
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

        return NoContent();
    }
}
