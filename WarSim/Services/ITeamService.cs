using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WarSim.DTOs;

namespace WarSim.Services;

public interface ITeamService
{
    Task<ServiceResult<TeamResponse>> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TeamResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

