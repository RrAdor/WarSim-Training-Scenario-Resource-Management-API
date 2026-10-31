using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WarSim.Data;
using WarSim.DTOs;
using WarSim.DTOs.Allocations;
using WarSim.Models;

namespace WarSim.Services;

public class AllocationService : IAllocationService
{
    private readonly ApplicationDbContext _context;

    public AllocationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<AllocationResponse>> CreateAsync(
        int scenarioId,
        CreateAllocationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            return ServiceResult<AllocationResponse>.BadRequest("Allocation quantity must be greater than zero.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Verify scenario exists and is in Draft state
            var scenario = await _context.Scenarios
                .FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);

            if (scenario == null)
            {
                return ServiceResult<AllocationResponse>.NotFound($"Scenario with ID {scenarioId} was not found.");
            }

            if (scenario.Status != ScenarioStatus.Draft)
            {
                return ServiceResult<AllocationResponse>.BadRequest("Only draft scenarios may receive allocations.");
            }

            // 2. Verify team exists and is active
            var team = await _context.Teams
                .FirstOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);

            if (team == null)
            {
                return ServiceResult<AllocationResponse>.NotFound($"Team with ID {request.TeamId} was not found.");
            }

            if (!team.IsActive)
            {
                return ServiceResult<AllocationResponse>.BadRequest($"Team '{team.Name}' is not active.");
            }

            // 3. Verify equipment exists and is active
            var equipment = await _context.Equipment
                .FirstOrDefaultAsync(e => e.Id == request.EquipmentId, cancellationToken);

            if (equipment == null)
            {
                return ServiceResult<AllocationResponse>.NotFound($"Equipment with ID {request.EquipmentId} was not found.");
            }

            if (!equipment.IsActive)
            {
                return ServiceResult<AllocationResponse>.BadRequest($"Equipment '{equipment.Name}' is not active.");
            }

            // 4. Reject duplicate allocation (ScenarioId, TeamId, EquipmentId)
            bool exists = await _context.Allocations
                .AnyAsync(a => a.ScenarioId == scenarioId &&
                               a.TeamId == request.TeamId &&
                               a.EquipmentId == request.EquipmentId, cancellationToken);

            if (exists)
            {
                return ServiceResult<AllocationResponse>.Conflict(
                    "An allocation for this team and equipment combination already exists in this scenario.");
            }

            // 5. Verify sufficient available stock
            if (equipment.AvailableQuantity < request.Quantity)
            {
                return ServiceResult<AllocationResponse>.BadRequest(
                    $"Insufficient equipment available. Requested: {request.Quantity}, Available: {equipment.AvailableQuantity}.");
            }

            // 6. Decrease equipment availability
            equipment.AvailableQuantity -= request.Quantity;

            // 7. Create allocation record
            var allocation = new Allocation
            {
                ScenarioId = scenarioId,
                TeamId = request.TeamId,
                EquipmentId = request.EquipmentId,
                Quantity = request.Quantity,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            _context.Allocations.Add(allocation);

            // 8. Commit atomically
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var response = new AllocationResponse
            {
                Id = allocation.Id,
                ScenarioId = allocation.ScenarioId,
                TeamId = allocation.TeamId,
                TeamName = team.Name,
                EquipmentId = allocation.EquipmentId,
                EquipmentName = equipment.Name,
                Quantity = allocation.Quantity,
                CreatedAtUtc = allocation.CreatedAtUtc
            };

            return ServiceResult<AllocationResponse>.Ok(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<AllocationResponse>.Conflict(
                "A concurrency conflict occurred while updating equipment inventory. Please retry.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true ||
                                          ex.Message.Contains("UNIQUE") ||
                                          ex.InnerException?.Message.Contains("CHECK") == true)
        {
            await transaction.RollbackAsync(cancellationToken);
            if (ex.InnerException?.Message.Contains("UNIQUE") == true || ex.Message.Contains("UNIQUE"))
            {
                return ServiceResult<AllocationResponse>.Conflict(
                    "An allocation for this team and equipment combination already exists in this scenario.");
            }

            return ServiceResult<AllocationResponse>.BadRequest("The allocation violates inventory or quantity constraints.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<IReadOnlyList<AllocationResponse>>> GetByScenarioIdAsync(
        int scenarioId,
        CancellationToken cancellationToken = default)
    {
        bool scenarioExists = await _context.Scenarios
            .AnyAsync(s => s.Id == scenarioId, cancellationToken);

        if (!scenarioExists)
        {
            return ServiceResult<IReadOnlyList<AllocationResponse>>.NotFound(
                $"Scenario with ID {scenarioId} was not found.");
        }

        var allocations = await _context.Allocations
            .AsNoTracking()
            .Where(a => a.ScenarioId == scenarioId)
            .Include(a => a.Team)
            .Include(a => a.Equipment)
            .OrderBy(a => a.Id)
            .Select(a => new AllocationResponse
            {
                Id = a.Id,
                ScenarioId = a.ScenarioId,
                TeamId = a.TeamId,
                TeamName = a.Team.Name,
                EquipmentId = a.EquipmentId,
                EquipmentName = a.Equipment.Name,
                Quantity = a.Quantity,
                CreatedAtUtc = a.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<AllocationResponse>>.Ok(allocations);
    }

    public async Task<ServiceResult> DeleteAsync(
        int allocationId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var allocation = await _context.Allocations
                .Include(a => a.Equipment)
                .FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken);

            if (allocation == null)
            {
                return ServiceResult.NotFound($"Allocation with ID {allocationId} was not found.");
            }

            // Restore equipment availability
            var equipment = allocation.Equipment;
            equipment.AvailableQuantity += allocation.Quantity;

            // Invariant guard: AvailableQuantity must never exceed TotalQuantity
            if (equipment.AvailableQuantity > equipment.TotalQuantity)
            {
                equipment.AvailableQuantity = equipment.TotalQuantity;
            }

            _context.Allocations.Remove(allocation);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult.Conflict("A concurrency conflict occurred while restoring equipment inventory. Please retry.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

