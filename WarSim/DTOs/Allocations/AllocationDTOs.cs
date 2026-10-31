using System;
using System.ComponentModel.DataAnnotations;

namespace WarSim.DTOs.Allocations;

public class CreateAllocationRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "TeamId must be a valid positive integer.")]
    public int TeamId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "EquipmentId must be a valid positive integer.")]
    public int EquipmentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public int Quantity { get; set; }
}

public class AllocationResponse
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

