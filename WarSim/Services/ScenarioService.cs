using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WarSim.Data;
using WarSim.DTOs;
using WarSim.Models;

namespace WarSim.Services;

public class ScenarioService : IScenarioService
{
    private readonly ApplicationDbContext _context;

    public ScenarioService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<ScenarioResponse>> CreateAsync(CreateScenarioRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ServiceResult<ScenarioResponse>.BadRequest("Scenario name cannot be empty.");
        }

        var scenario = new Scenario
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Status = ScenarioStatus.Draft,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _context.Scenarios.Add(scenario);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<ScenarioResponse>.Ok(ToResponse(scenario));
    }

    public async Task<IReadOnlyList<ScenarioResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Scenarios
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => ToResponse(s))
            .ToListAsync(cancellationToken);
    }

    public async Task<ScenarioResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var scenario = await _context.Scenarios
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return scenario == null ? null : ToResponse(scenario);
    }

    public async Task<ServiceResult<ScenarioResponse>> UpdateAsync(int id, UpdateScenarioRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ServiceResult<ScenarioResponse>.BadRequest("Scenario name cannot be empty.");
        }

        var scenario = await _context.Scenarios.FindAsync(new object[] { id }, cancellationToken);
        if (scenario == null)
        {
            return ServiceResult<ScenarioResponse>.NotFound($"Scenario with ID {id} was not found.");
        }

        if (scenario.Status != ScenarioStatus.Draft)
        {
            return ServiceResult<ScenarioResponse>.BadRequest("Only draft scenarios may be edited.");
        }

        scenario.Name = request.Name.Trim();
        scenario.Description = request.Description?.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<ScenarioResponse>.Ok(ToResponse(scenario));
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var scenario = await _context.Scenarios.FindAsync(new object[] { id }, cancellationToken);
        if (scenario == null)
        {
            return ServiceResult.NotFound($"Scenario with ID {id} was not found.");
        }

        bool hasAllocations = await _context.Allocations.AnyAsync(a => a.ScenarioId == id, cancellationToken);
        if (hasAllocations)
        {
            return ServiceResult.BadRequest("Cannot delete scenario with active allocations.");
        }

        bool hasEvents = await _context.SimulationEvents.AnyAsync(e => e.ScenarioId == id, cancellationToken);
        if (hasEvents)
        {
            return ServiceResult.BadRequest("Cannot delete scenario with recorded simulation events.");
        }

        _context.Scenarios.Remove(scenario);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.Ok();
    }

    private static ScenarioResponse ToResponse(Scenario scenario) => new()
    {
        Id = scenario.Id,
        Name = scenario.Name,
        Description = scenario.Description,
        Status = scenario.Status,
        CreatedAtUtc = scenario.CreatedAtUtc
    };
}

