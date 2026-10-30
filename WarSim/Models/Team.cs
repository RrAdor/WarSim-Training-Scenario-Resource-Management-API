namespace WarSim.Models;

public class Team
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int MemberCount { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();
}

