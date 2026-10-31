using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WarSim.DTOs;
using WarSim.Services;

namespace WarSim.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScenariosController : ControllerBase
{
    private readonly IScenarioService _scenarioService;

    public ScenariosController(IScenarioService scenarioService)
    {
        _scenarioService = scenarioService;
    }

    [HttpPost]
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
    public async Task<ActionResult<IReadOnlyList<ScenarioResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var scenarios = await _scenarioService.GetAllAsync(cancellationToken);
        return Ok(scenarios);
    }

    [HttpGet("{id:int}")]
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

