namespace WarSim.Models;

public class SimulationEvent
{
    public int Id { get; set; }

    public int ScenarioId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Scenario Scenario { get; set; } = null!;
}

