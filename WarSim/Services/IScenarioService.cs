using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WarSim.DTOs;

namespace WarSim.Services;

public interface IScenarioService
{
    Task<ServiceResult<ScenarioResponse>> CreateAsync(CreateScenarioRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScenarioResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ScenarioResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ServiceResult<ScenarioResponse>> UpdateAsync(int id, UpdateScenarioRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

