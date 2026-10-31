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
public class EquipmentController : ControllerBase
{
    private readonly IEquipmentService _equipmentService;

    public EquipmentController(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    [HttpPost]
    [EndpointSummary("Register generic training equipment")]
    [EndpointDescription("Registers new generic training equipment. AvailableQuantity is automatically set to TotalQuantity by the server.")]
    [ProducesResponseType(typeof(EquipmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EquipmentResponse>> Create(
        [FromBody] CreateEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _equipmentService.CreateAsync(request, cancellationToken);
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
    [EndpointSummary("List all equipment")]
    [EndpointDescription("Retrieves all registered training equipment items ordered by ID.")]
    [ProducesResponseType(typeof(IReadOnlyList<EquipmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EquipmentResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var equipment = await _equipmentService.GetAllAsync(cancellationToken);
        return Ok(equipment);
    }

    [HttpGet("{id:int}")]
    [EndpointSummary("Get equipment by ID")]
    [EndpointDescription("Retrieves the details of a specific equipment item by its ID.")]
    [ProducesResponseType(typeof(EquipmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EquipmentResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _equipmentService.GetByIdAsync(id, cancellationToken);
        if (item == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = 404,
                Title = "Not Found",
                Detail = $"Equipment with ID {id} was not found."
            });
        }

        return Ok(item);
    }
}
