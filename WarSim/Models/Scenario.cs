namespace WarSim.Models;

public class Scenario
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ScenarioStatus Status { get; set; } = ScenarioStatus.Draft;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();

    public ICollection<SimulationEvent> SimulationEvents { get; set; } = new List<SimulationEvent>();
}

