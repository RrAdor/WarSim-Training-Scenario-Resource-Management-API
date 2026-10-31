using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WarSim.DTOs;
using WarSim.DTOs.Simulation;

namespace WarSim.Services;

public interface ISimulationService
{
    Task<ServiceResult<SimulationResultResponse>> RunSimulationAsync(
        int scenarioId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<SimulationEventResponse>>> GetScenarioEventsAsync(
        int scenarioId,
        CancellationToken cancellationToken = default);
}

