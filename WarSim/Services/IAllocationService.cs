using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WarSim.DTOs;
using WarSim.DTOs.Allocations;

namespace WarSim.Services;

public interface IAllocationService
{
    Task<ServiceResult<AllocationResponse>> CreateAsync(
        int scenarioId,
        CreateAllocationRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<AllocationResponse>>> GetByScenarioIdAsync(
        int scenarioId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(
        int allocationId,
        CancellationToken cancellationToken = default);
}

