using System.ComponentModel.DataAnnotations;

namespace WarSim.DTOs;

public class CreateEquipmentRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Equipment name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Equipment name must be between 1 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Total quantity cannot be negative.")]
    public int TotalQuantity { get; set; }
}

public class EquipmentResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsActive { get; set; }
}

