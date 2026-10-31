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

public class EquipmentService : IEquipmentService
{
    private readonly ApplicationDbContext _context;

    public EquipmentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<EquipmentResponse>> CreateAsync(CreateEquipmentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ServiceResult<EquipmentResponse>.BadRequest("Equipment name cannot be empty.");
        }

        if (request.TotalQuantity < 0)
        {
            return ServiceResult<EquipmentResponse>.BadRequest("Total quantity cannot be negative.");
        }

        var equipment = new Equipment
        {
            Name = request.Name.Trim(),
            TotalQuantity = request.TotalQuantity,
            AvailableQuantity = request.TotalQuantity, // Client cannot set AvailableQuantity; server assigns it
            IsActive = true
        };

        _context.Equipment.Add(equipment);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<EquipmentResponse>.Ok(ToResponse(equipment));
    }

    public async Task<IReadOnlyList<EquipmentResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Equipment
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => ToResponse(e))
            .ToListAsync(cancellationToken);
    }

    public async Task<EquipmentResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var equipment = await _context.Equipment
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return equipment == null ? null : ToResponse(equipment);
    }

    private static EquipmentResponse ToResponse(Equipment equipment) => new()
    {
        Id = equipment.Id,
        Name = equipment.Name,
        TotalQuantity = equipment.TotalQuantity,
        AvailableQuantity = equipment.AvailableQuantity,
        IsActive = equipment.IsActive
    };
}

