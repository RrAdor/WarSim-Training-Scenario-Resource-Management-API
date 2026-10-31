using System.ComponentModel.DataAnnotations;

namespace WarSim.DTOs;

public class CreateTeamRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Team name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Team name must be between 1 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Member count cannot be negative.")]
    public int MemberCount { get; set; }
}

public class TeamResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public bool IsActive { get; set; }
}

