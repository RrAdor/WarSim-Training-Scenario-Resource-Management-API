using System;

namespace WarSim.DTOs.Simulation;

public class SimulationResultResponse
{
    public int ScenarioId { get; set; }

    public string ScenarioName { get; set; } = string.Empty;

    public int TotalActiveTeams { get; set; }

    public int TeamCount => TotalActiveTeams;

    public int TeamsWithAllocations { get; set; }

    public int TotalAllocations { get; set; }

    public int AllocationCount => TotalAllocations;

    public int TotalAllocatedEquipmentUnits { get; set; }

    public int AllocatedEquipmentUnits => TotalAllocatedEquipmentUnits;

    public decimal TeamAllocationCoveragePercent { get; set; }

    public DateTimeOffset ExecutedAtUtc { get; set; }

    public DateTimeOffset RunAtUtc => ExecutedAtUtc;

    public string Summary { get; set; } = string.Empty;
}

