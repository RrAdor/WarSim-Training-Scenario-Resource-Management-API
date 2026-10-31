using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WarSim.Data;
using WarSim.DTOs;
using WarSim.DTOs.Simulation;
using WarSim.Models;

namespace WarSim.Services;

public class SimulationService : ISimulationService
{
    private readonly ApplicationDbContext _context;

    public SimulationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<SimulationResultResponse>> RunSimulationAsync(
        int scenarioId,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify scenario exists
        var scenario = await _context.Scenarios
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);

        if (scenario == null)
        {
            return ServiceResult<SimulationResultResponse>.NotFound(
                $"Scenario with ID {scenarioId} was not found.");
        }

        // 2. Query allocations for this scenario including team and equipment details
        var allocations = await _context.Allocations
            .AsNoTracking()
            .Include(a => a.Team)
            .Include(a => a.Equipment)
            .Where(a => a.ScenarioId == scenarioId)
            .ToListAsync(cancellationToken);

        // 3. Verify scenario has at least one allocation
        if (allocations.Count == 0)
        {
            return ServiceResult<SimulationResultResponse>.BadRequest(
                "Scenario must have at least one allocation to run a simulation.");
        }

        // 4. Calculate administrative summary metrics
        // Total active teams in the system (denominator)
        var totalActiveTeams = await _context.Teams
            .AsNoTracking()
            .CountAsync(t => t.IsActive, cancellationToken);

        // Active teams associated with this scenario's allocations (numerator)
        var teamsWithAllocations = allocations
            .Where(a => a.Team != null && a.Team.IsActive)
            .Select(a => a.TeamId)
            .Distinct()
            .Count();

        var totalAllocations = allocations.Count;
        var totalAllocatedEquipmentUnits = allocations.Sum(a => a.Quantity);

        // Calculate coverage percentage with safe division-by-zero handling
        var coveragePercent = totalActiveTeams > 0
            ? Math.Round(((decimal)teamsWithAllocations / totalActiveTeams) * 100m, 2)
            : 0m;

        var executedAtUtc = DateTimeOffset.UtcNow;
        var summary = $"{teamsWithAllocations} of {totalActiveTeams} active teams have at least one equipment allocation. Administrative summary only.";

        // 5. Create and persist SimulationEvent record
        var simulationEvent = new SimulationEvent
        {
            ScenarioId = scenario.Id,
            EventType = "SimulationExecuted",
            Description = summary,
            OccurredAtUtc = executedAtUtc
        };

        _context.SimulationEvents.Add(simulationEvent);
        await _context.SaveChangesAsync(cancellationToken);

        var result = new SimulationResultResponse
        {
            ScenarioId = scenario.Id,
            ScenarioName = scenario.Name,
            TotalActiveTeams = totalActiveTeams,
            TeamsWithAllocations = teamsWithAllocations,
            TotalAllocations = totalAllocations,
            TotalAllocatedEquipmentUnits = totalAllocatedEquipmentUnits,
            TeamAllocationCoveragePercent = coveragePercent,
            ExecutedAtUtc = executedAtUtc,
            Summary = summary
        };

        return ServiceResult<SimulationResultResponse>.Ok(result);
    }

    public async Task<ServiceResult<IReadOnlyList<SimulationEventResponse>>> GetScenarioEventsAsync(
        int scenarioId,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify scenario exists
        var scenarioExists = await _context.Scenarios
            .AnyAsync(s => s.Id == scenarioId, cancellationToken);

        if (!scenarioExists)
        {
            return ServiceResult<IReadOnlyList<SimulationEventResponse>>.NotFound(
                $"Scenario with ID {scenarioId} was not found.");
        }

        // 2. Query events for this scenario, then order by OccurredAtUtc descending
        // Note: EF Core SQLite does not support DateTimeOffset ordering directly in SQL,
        // so we order by Id in SQL and finalize chronological ordering in LINQ to Objects.
        var events = await _context.SimulationEvents
            .AsNoTracking()
            .Where(e => e.ScenarioId == scenarioId)
            .OrderByDescending(e => e.Id)
            .Select(e => new SimulationEventResponse
            {
                Id = e.Id,
                ScenarioId = e.ScenarioId,
                EventType = e.EventType,
                Description = e.Description,
                OccurredAtUtc = e.OccurredAtUtc
            })
            .ToListAsync(cancellationToken);

        var sortedEvents = events
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .ToList();

        return ServiceResult<IReadOnlyList<SimulationEventResponse>>.Ok(sortedEvents);
    }
}
