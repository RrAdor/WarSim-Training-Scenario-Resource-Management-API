namespace WarSim.Models;

public class Allocation
{
    public int Id { get; set; }

    public int ScenarioId { get; set; }

    public int TeamId { get; set; }

    public int EquipmentId { get; set; }

    public int Quantity { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Scenario Scenario { get; set; } = null!;

    public Team Team { get; set; } = null!;

    public Equipment Equipment { get; set; } = null!;
}

