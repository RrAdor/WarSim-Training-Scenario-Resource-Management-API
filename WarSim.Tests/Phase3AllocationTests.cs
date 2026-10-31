using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WarSim.Controllers;
using WarSim.Data;
using WarSim.DTOs;
using WarSim.DTOs.Allocations;
using WarSim.Models;
using WarSim.Services;
using Xunit;

namespace WarSim.Tests;

public class Phase3AllocationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;

    public Phase3AllocationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        _contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_contextOptions);

    public void Dispose()
    {
        _connection.Dispose();
    }

    private async Task<(int ScenarioId, int TeamId, int EquipmentId)> SeedPrerequisitesAsync(
        ScenarioStatus scenarioStatus = ScenarioStatus.Draft,
        bool isTeamActive = true,
        bool isEquipmentActive = true,
        int totalStock = 20,
        int availableStock = 20)
    {
        using var context = CreateContext();

        var scenario = new Scenario
        {
            Name = "Tactical Exercise Alpha",
            Status = scenarioStatus
        };

        var team = new Team
        {
            Name = "Alpha Squad",
            MemberCount = 8,
            IsActive = isTeamActive
        };

        var equipment = new Equipment
        {
            Name = "Field Radios",
            TotalQuantity = totalStock,
            AvailableQuantity = availableStock,
            IsActive = isEquipmentActive
        };

        context.AddRange(scenario, team, equipment);
        await context.SaveChangesAsync();

        return (scenario.Id, team.Id, equipment.Id);
    }

    // ==========================================
    // SUCCESSFUL OPERATIONS
    // ==========================================

    [Fact]
    public async Task CreateAllocation_WithSufficientStock_DecreasesAvailableQuantityAndPersists()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync(totalStock: 20, availableStock: 20);

        using (var context = CreateContext())
        {
            var service = new AllocationService(context);
            var result = await service.CreateAsync(scenarioId, new CreateAllocationRequest
            {
                TeamId = teamId,
                EquipmentId = equipmentId,
                Quantity = 5
            });

            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.True(result.Value.Id > 0);
            Assert.Equal(scenarioId, result.Value.ScenarioId);
            Assert.Equal(teamId, result.Value.TeamId);
            Assert.Equal("Alpha Squad", result.Value.TeamName);
            Assert.Equal(equipmentId, result.Value.EquipmentId);
            Assert.Equal("Field Radios", result.Value.EquipmentName);
            Assert.Equal(5, result.Value.Quantity);
        }

        // Verify database state: AvailableQuantity must be 15
        using (var context = CreateContext())
        {
            var equipment = await context.Equipment.FindAsync(equipmentId);
            Assert.NotNull(equipment);
            Assert.Equal(20, equipment.TotalQuantity);
            Assert.Equal(15, equipment.AvailableQuantity);
        }
    }

    [Fact]
    public async Task GetByScenarioId_ReturnsAllAllocationsWithTeamAndEquipmentNames()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync();

        using (var context = CreateContext())
        {
            var service = new AllocationService(context);
            await service.CreateAsync(scenarioId, new CreateAllocationRequest
            {
                TeamId = teamId,
                EquipmentId = equipmentId,
                Quantity = 3
            });
        }

        using (var context = CreateContext())
        {
            var service = new AllocationService(context);
            var result = await service.GetByScenarioIdAsync(scenarioId);

            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.Single(result.Value);

            var allocation = result.Value[0];
            Assert.Equal(3, allocation.Quantity);
            Assert.Equal("Alpha Squad", allocation.TeamName);
            Assert.Equal("Field Radios", allocation.EquipmentName);
        }
    }

    [Fact]
    public async Task DeleteAllocation_RestoresAvailableQuantityAtomically()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync(totalStock: 20, availableStock: 20);
        int allocationId;

        using (var context = CreateContext())
        {
            var service = new AllocationService(context);
            var created = await service.CreateAsync(scenarioId, new CreateAllocationRequest
            {
                TeamId = teamId,
                EquipmentId = equipmentId,
                Quantity = 6
            });
            allocationId = created.Value!.Id;
        }

        // Verify intermediate available stock
        using (var context = CreateContext())
        {
            var equipment = await context.Equipment.FindAsync(equipmentId);
            Assert.Equal(14, equipment!.AvailableQuantity);
        }

        // Delete allocation
        using (var context = CreateContext())
        {
            var service = new AllocationService(context);
            var deleteResult = await service.DeleteAsync(allocationId);
            Assert.True(deleteResult.Success);
        }

        // Verify restored available stock and record removal
        using (var context = CreateContext())
        {
            var equipment = await context.Equipment.FindAsync(equipmentId);
            Assert.Equal(20, equipment!.AvailableQuantity);

            var allocation = await context.Allocations.FindAsync(allocationId);
            Assert.Null(allocation);
        }
    }

    // ==========================================
    // INVALID OPERATIONS
    // ==========================================

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateAllocation_ZeroOrNegativeQuantity_ReturnsBadRequest(int invalidQty)
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync();

        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = await service.CreateAsync(scenarioId, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = invalidQty
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
    }

    [Fact]
    public async Task CreateAllocation_WhenQuantityExceedsAvailableStock_ReturnsBadRequest()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync(totalStock: 10, availableStock: 10);

        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = await service.CreateAsync(scenarioId, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = 15 // Exceeds available stock (10)
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
        Assert.Contains("Insufficient equipment available", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateAllocation_WithUnknownScenario_ReturnsNotFound()
    {
        var (_, teamId, equipmentId) = await SeedPrerequisitesAsync();

        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = await service.CreateAsync(9999, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = 2
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task CreateAllocation_WithUnknownTeam_ReturnsNotFound()
    {
        var (scenarioId, _, equipmentId) = await SeedPrerequisitesAsync();

        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = await service.CreateAsync(scenarioId, new CreateAllocationRequest
        {
            TeamId = 9999,
            EquipmentId = equipmentId,
            Quantity = 2
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task CreateAllocation_WithUnknownEquipment_ReturnsNotFound()
    {
        var (scenarioId, teamId, _) = await SeedPrerequisitesAsync();

        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = await service.CreateAsync(scenarioId, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = 9999,
            Quantity = 2
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task CreateAllocation_ToInactiveTeam_ReturnsBadRequest()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync(isTeamActive: false);

        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = await service.CreateAsync(scenarioId, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = 2
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
        Assert.Contains("not active", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateAllocation_WithInactiveEquipment_ReturnsBadRequest()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync(isEquipmentActive: false);

        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = await service.CreateAsync(scenarioId, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = 2
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
        Assert.Contains("not active", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateAllocation_ToNonDraftScenario_ReturnsBadRequest()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync(scenarioStatus: ScenarioStatus.Ready);

        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = await service.CreateAsync(scenarioId, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = 2
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
        Assert.Contains("Only draft scenarios", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateAllocation_DuplicateRow_ReturnsConflict()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync(totalStock: 20, availableStock: 20);

        using var context = CreateContext();
        var service = new AllocationService(context);

        var firstResult = await service.CreateAsync(scenarioId, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = 2
        });
        Assert.True(firstResult.Success);

        // Attempting to allocate same scenario, team, equipment again
        var duplicateResult = await service.CreateAsync(scenarioId, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = 3
        });

        Assert.False(duplicateResult.Success);
        Assert.Equal(ServiceErrorType.Conflict, duplicateResult.ErrorType);
    }

    [Fact]
    public async Task DeleteAllocation_NonExistentId_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = await service.DeleteAsync(9999);
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetByScenarioId_UnknownScenario_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = await service.GetByScenarioIdAsync(9999);
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    // ==========================================
    // DATABASE INTEGRITY & TRANSACTION ROLLBACK
    // ==========================================

    [Fact]
    public async Task FailedAllocation_DoesNotChangeInventory()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync(totalStock: 10, availableStock: 10);

        using (var context = CreateContext())
        {
            var service = new AllocationService(context);
            // Requesting 15 when only 10 available
            var result = await service.CreateAsync(scenarioId, new CreateAllocationRequest
            {
                TeamId = teamId,
                EquipmentId = equipmentId,
                Quantity = 15
            });

            Assert.False(result.Success);
        }

        // Verify inventory in database remains completely untouched
        using (var context = CreateContext())
        {
            var equipment = await context.Equipment.FindAsync(equipmentId);
            Assert.Equal(10, equipment!.AvailableQuantity);
            Assert.Empty(context.Allocations);
        }
    }

    [Fact]
    public async Task OptimisticConcurrency_ConflictOnEquipment_RollsBackTransaction()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync(totalStock: 10, availableStock: 10);

        using var context1 = CreateContext();
        using var context2 = CreateContext();

        // 1. Context 1 and Context 2 both load the same equipment record
        var eq1 = await context1.Equipment.FindAsync(equipmentId);
        var eq2 = await context2.Equipment.FindAsync(equipmentId);
        Assert.NotNull(eq1);
        Assert.NotNull(eq2);

        // 2. Context 1 updates stock to 5 and saves
        eq1.AvailableQuantity = 5;
        await context1.SaveChangesAsync();

        // 3. Context 2 attempts to save an update based on stale AvailableQuantity (10)
        eq2.AvailableQuantity = 3;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context2.SaveChangesAsync());
    }

    // ==========================================
    // CONTROLLER HTTP RESPONSES
    // ==========================================

    [Fact]
    public async Task AllocationsController_Create_ReturnsCreatedAtAction()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync();

        using var context = CreateContext();
        var service = new AllocationService(context);
        var controller = new AllocationsController(service);

        var response = await controller.Create(scenarioId, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = 4
        }, default);

        var createdAtResult = Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Equal(201, createdAtResult.StatusCode);
        var allocation = Assert.IsType<AllocationResponse>(createdAtResult.Value);
        Assert.Equal(4, allocation.Quantity);
    }

    [Fact]
    public async Task AllocationsController_Create_Duplicate_ReturnsConflict409()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync();

        using var context = CreateContext();
        var service = new AllocationService(context);
        var controller = new AllocationsController(service);

        await controller.Create(scenarioId, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = 2
        }, default);

        var response = await controller.Create(scenarioId, new CreateAllocationRequest
        {
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = 2
        }, default);

        var conflictResult = Assert.IsType<ConflictObjectResult>(response.Result);
        Assert.Equal(409, conflictResult.StatusCode);
    }

    [Fact]
    public async Task AllocationsController_Delete_Returns204NoContent()
    {
        var (scenarioId, teamId, equipmentId) = await SeedPrerequisitesAsync();
        int allocationId;

        using (var context = CreateContext())
        {
            var service = new AllocationService(context);
            var created = await service.CreateAsync(scenarioId, new CreateAllocationRequest
            {
                TeamId = teamId,
                EquipmentId = equipmentId,
                Quantity = 2
            });
            allocationId = created.Value!.Id;
        }

        using (var context = CreateContext())
        {
            var service = new AllocationService(context);
            var controller = new AllocationsController(service);

            var deleteResponse = await controller.Delete(allocationId, default);
            Assert.IsType<NoContentResult>(deleteResponse);
        }
    }
}

