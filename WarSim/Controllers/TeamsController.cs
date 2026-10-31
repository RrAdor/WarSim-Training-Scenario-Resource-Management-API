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
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamsController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpPost]
    [EndpointSummary("Register a new training team")]
    [EndpointDescription("Registers a new fictional team with specified name and non-negative member count. IsActive defaults to true.")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TeamResponse>> Create(
        [FromBody] CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _teamService.CreateAsync(request, cancellationToken);
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
    [EndpointSummary("List all teams")]
    [EndpointDescription("Retrieves all registered training teams ordered by ID.")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var teams = await _teamService.GetAllAsync(cancellationToken);
        return Ok(teams);
    }

    [HttpGet("{id:int}")]
    [EndpointSummary("Get team by ID")]
    [EndpointDescription("Retrieves the details of a specific team by its ID.")]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var team = await _teamService.GetByIdAsync(id, cancellationToken);
        if (team == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = 404,
                Title = "Not Found",
                Detail = $"Team with ID {id} was not found."
            });
        }

        return Ok(team);
    }
}
