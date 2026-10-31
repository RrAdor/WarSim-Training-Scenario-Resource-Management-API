using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WarSim.DTOs;

namespace WarSim.Services;

public interface IEquipmentService
{
    Task<ServiceResult<EquipmentResponse>> CreateAsync(CreateEquipmentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EquipmentResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<EquipmentResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

