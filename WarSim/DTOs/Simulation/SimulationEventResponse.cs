using System;

namespace WarSim.DTOs.Simulation;

public class SimulationEventResponse
{
    public int Id { get; set; }

    public int ScenarioId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }
}

