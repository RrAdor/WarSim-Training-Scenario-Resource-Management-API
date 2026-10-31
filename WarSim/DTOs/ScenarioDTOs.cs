using System;
using System.ComponentModel.DataAnnotations;
using WarSim.Models;

namespace WarSim.DTOs;

public class CreateScenarioRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Scenario name is required.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Scenario name must be between 1 and 120 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }
}

public class UpdateScenarioRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Scenario name is required.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Scenario name must be between 1 and 120 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }
}

public class ScenarioResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ScenarioStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

