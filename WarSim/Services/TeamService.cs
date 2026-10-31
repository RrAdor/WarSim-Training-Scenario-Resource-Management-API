using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WarSim.Data;
using WarSim.DTOs;
using WarSim.Models;

namespace WarSim.Services;

public class TeamService : ITeamService
{
    private readonly ApplicationDbContext _context;

    public TeamService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<TeamResponse>> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ServiceResult<TeamResponse>.BadRequest("Team name cannot be empty.");
        }

        if (request.MemberCount < 0)
        {
            return ServiceResult<TeamResponse>.BadRequest("Member count cannot be negative.");
        }

        var team = new Team
        {
            Name = request.Name.Trim(),
            MemberCount = request.MemberCount,
            IsActive = true
        };

        _context.Teams.Add(team);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<TeamResponse>.Ok(ToResponse(team));
    }

    public async Task<IReadOnlyList<TeamResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => ToResponse(t))
            .ToListAsync(cancellationToken);
    }

    public async Task<TeamResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var team = await _context.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return team == null ? null : ToResponse(team);
    }

    private static TeamResponse ToResponse(Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        MemberCount = team.MemberCount,
        IsActive = team.IsActive
    };
}

