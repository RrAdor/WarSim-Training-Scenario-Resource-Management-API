namespace WarSim.Models;

public class Equipment
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TotalQuantity { get; set; }

    public int AvailableQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();
}

