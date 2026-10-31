using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WarSim.Controllers;
using WarSim.Data;
using WarSim.DTOs;
using WarSim.DTOs.Simulation;
using WarSim.Models;
using WarSim.Services;
using Xunit;

namespace WarSim.Tests;

public class Phase4SimulationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;

    public Phase4SimulationTests()
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

    private async Task<Scenario> CreateScenarioAsync(string name = "Tactical Exercise Alpha", ScenarioStatus status = ScenarioStatus.Draft)
    {
        using var context = CreateContext();
        var scenario = new Scenario
        {
            Name = name,
            Description = "Fictional administrative scenario",
            Status = status,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();
        return scenario;
    }

    private async Task<Team> CreateTeamAsync(string name, int memberCount = 8, bool isActive = true)
    {
        using var context = CreateContext();
        var team = new Team
        {
            Name = name,
            MemberCount = memberCount,
            IsActive = isActive
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync();
        return team;
    }

    private async Task<Equipment> CreateEquipmentAsync(string name, int totalQuantity = 20, int availableQuantity = 20, bool isActive = true)
    {
        using var context = CreateContext();
        var equipment = new Equipment
        {
            Name = name,
            TotalQuantity = totalQuantity,
            AvailableQuantity = availableQuantity,
            IsActive = isActive
        };
        context.Equipment.Add(equipment);
        await context.SaveChangesAsync();
        return equipment;
    }

    private async Task<Allocation> CreateAllocationAsync(int scenarioId, int teamId, int equipmentId, int quantity)
    {
        using var context = CreateContext();
        var allocation = new Allocation
        {
            ScenarioId = scenarioId,
            TeamId = teamId,
            EquipmentId = equipmentId,
            Quantity = quantity,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        context.Allocations.Add(allocation);
        await context.SaveChangesAsync();
        return allocation;
    }

    // ==========================================
    // 1. RUN SIMULATION - PREREQUISITES & VALIDATION
    // ==========================================

    [Fact]
    public async Task RunSimulation_NonexistentScenario_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.RunSimulationAsync(9999);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Contains("9999", result.ErrorMessage);
    }

    [Fact]
    public async Task RunSimulation_ControllerNonexistentScenario_Returns404ProblemDetails()
    {
        using var context = CreateContext();
        var service = new SimulationService(context);
        var controller = new SimulationController(service);

        var response = await controller.Run(9999, default);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task RunSimulation_ScenarioWithNoAllocations_ReturnsBadRequest()
    {
        var scenario = await CreateScenarioAsync("Empty Scenario");

        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.RunSimulationAsync(scenario.Id);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
        Assert.Equal("Scenario must have at least one allocation to run a simulation.", result.ErrorMessage);
    }

    [Fact]
    public async Task RunSimulation_ControllerScenarioWithNoAllocations_Returns400ProblemDetails()
    {
        var scenario = await CreateScenarioAsync("Empty Scenario");

        using var context = CreateContext();
        var service = new SimulationService(context);
        var controller = new SimulationController(service);

        var response = await controller.Run(scenario.Id, default);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.Equal(400, problem.Status);
        Assert.Equal("Scenario must have at least one allocation to run a simulation.", problem.Detail);
    }

    // ==========================================
    // 2. RUN SIMULATION - ADMINISTRATIVE CALCULATION
    // ==========================================

    [Fact]
    public async Task RunSimulation_ValidScenario_ReturnsAccurateMetrics()
    {
        var scenario = await CreateScenarioAsync("Alpha Exercise");
        var teamA = await CreateTeamAsync("Team Alpha", isActive: true);
        var teamB = await CreateTeamAsync("Team Bravo", isActive: true);
        var radio = await CreateEquipmentAsync("Tactical Radios", totalQuantity: 20, availableQuantity: 12);

        await CreateAllocationAsync(scenario.Id, teamA.Id, radio.Id, 5);
        await CreateAllocationAsync(scenario.Id, teamB.Id, radio.Id, 3);

        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.RunSimulationAsync(scenario.Id);

        Assert.True(result.Success);
        var summary = result.Value!;
        Assert.Equal(scenario.Id, summary.ScenarioId);
        Assert.Equal("Alpha Exercise", summary.ScenarioName);
        Assert.Equal(2, summary.TotalActiveTeams);
        Assert.Equal(2, summary.TeamCount);
        Assert.Equal(2, summary.TeamsWithAllocations);
        Assert.Equal(2, summary.TotalAllocations);
        Assert.Equal(2, summary.AllocationCount);
        Assert.Equal(8, summary.TotalAllocatedEquipmentUnits);
        Assert.Equal(8, summary.AllocatedEquipmentUnits);
        Assert.Equal(100.00m, summary.TeamAllocationCoveragePercent);
        Assert.Contains("2 of 2 active teams have at least one equipment allocation. Administrative summary only.", summary.Summary);
    }

    [Fact]
    public async Task RunSimulation_TotalActiveTeamCount_CountsOnlyActiveTeamsInSystem()
    {
        var scenario = await CreateScenarioAsync("Beta Exercise");
        var activeTeam1 = await CreateTeamAsync("Active Team 1", isActive: true);
        var activeTeam2 = await CreateTeamAsync("Active Team 2", isActive: true);
        var inactiveTeam = await CreateTeamAsync("Inactive Team", isActive: false);
        var radio = await CreateEquipmentAsync("Radios", 20, 15);

        await CreateAllocationAsync(scenario.Id, activeTeam1.Id, radio.Id, 5);

        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.RunSimulationAsync(scenario.Id);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.TotalActiveTeams);
    }

    [Fact]
    public async Task RunSimulation_TeamsWithAllocations_CountsOnlyActiveTeamsInScenarioAllocations()
    {
        var scenario = await CreateScenarioAsync("Gamma Exercise");
        var activeTeam = await CreateTeamAsync("Active Team", isActive: true);
        var inactiveTeam = await CreateTeamAsync("Deactivated Team", isActive: false);
        var radio = await CreateEquipmentAsync("Radios", 20, 10);

        await CreateAllocationAsync(scenario.Id, activeTeam.Id, radio.Id, 5);
        await CreateAllocationAsync(scenario.Id, inactiveTeam.Id, radio.Id, 5);

        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.RunSimulationAsync(scenario.Id);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.TotalActiveTeams);
        Assert.Equal(1, result.Value!.TeamsWithAllocations);
    }

    [Fact]
    public async Task RunSimulation_TotalAllocations_ReturnsCorrectAllocationRecordCount()
    {
        var scenario = await CreateScenarioAsync("Delta Exercise");
        var team1 = await CreateTeamAsync("Team 1");
        var radio = await CreateEquipmentAsync("Radios", 20, 10);
        var laptop = await CreateEquipmentAsync("Laptops", 20, 10);
        var firstAid = await CreateEquipmentAsync("First Aid Kits", 20, 10);

        await CreateAllocationAsync(scenario.Id, team1.Id, radio.Id, 2);
        await CreateAllocationAsync(scenario.Id, team1.Id, laptop.Id, 3);
        await CreateAllocationAsync(scenario.Id, team1.Id, firstAid.Id, 4);

        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.RunSimulationAsync(scenario.Id);

        Assert.True(result.Success);
        Assert.Equal(3, result.Value!.TotalAllocations);
    }

    [Fact]
    public async Task RunSimulation_TotalAllocatedEquipmentUnits_SumsQuantitiesAccurately()
    {
        var scenario = await CreateScenarioAsync("Epsilon Exercise");
        var team1 = await CreateTeamAsync("Team 1");
        var team2 = await CreateTeamAsync("Team 2");
        var radio = await CreateEquipmentAsync("Radios", 50, 20);

        await CreateAllocationAsync(scenario.Id, team1.Id, radio.Id, 12);
        await CreateAllocationAsync(scenario.Id, team2.Id, radio.Id, 18);

        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.RunSimulationAsync(scenario.Id);

        Assert.True(result.Success);
        Assert.Equal(30, result.Value!.TotalAllocatedEquipmentUnits);
    }

    [Fact]
    public async Task RunSimulation_CoveragePercentage_CalculatesAccuratelyAndRoundsToTwoDecimalPlaces()
    {
        var scenario = await CreateScenarioAsync("Zeta Exercise");
        var team1 = await CreateTeamAsync("Team 1", isActive: true);
        var team2 = await CreateTeamAsync("Team 2", isActive: true);
        var team3 = await CreateTeamAsync("Team 3", isActive: true);
        var radio = await CreateEquipmentAsync("Radios", 20, 15);

        // 1 of 3 active teams covered => (1 / 3) * 100 = 33.3333...% => 33.33%
        await CreateAllocationAsync(scenario.Id, team1.Id, radio.Id, 5);

        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.RunSimulationAsync(scenario.Id);

        Assert.True(result.Success);
        Assert.Equal(3, result.Value!.TotalActiveTeams);
        Assert.Equal(1, result.Value!.TeamsWithAllocations);
        Assert.Equal(33.33m, result.Value!.TeamAllocationCoveragePercent);
    }

    [Fact]
    public async Task RunSimulation_CoveragePercentage_FiftyPercentCoverageWorkflow()
    {
        var scenario = await CreateScenarioAsync("Workflow Exercise");
        var team1 = await CreateTeamAsync("Team 1", isActive: true);
        var team2 = await CreateTeamAsync("Team 2", isActive: true);
        var radio = await CreateEquipmentAsync("Radios", 20, 15);

        // 1 of 2 active teams covered => (1 / 2) * 100 = 50.00%
        await CreateAllocationAsync(scenario.Id, team1.Id, radio.Id, 5);

        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.RunSimulationAsync(scenario.Id);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.TotalActiveTeams);
        Assert.Equal(1, result.Value!.TeamsWithAllocations);
        Assert.Equal(50.00m, result.Value!.TeamAllocationCoveragePercent);
    }

    [Fact]
    public async Task RunSimulation_ZeroActiveTeams_DoesNotCauseDivisionByZero()
    {
        var scenario = await CreateScenarioAsync("Zero Active Teams Exercise");
        var inactiveTeam = await CreateTeamAsync("Inactive Team", isActive: false);
        var radio = await CreateEquipmentAsync("Radios", 20, 15);

        await CreateAllocationAsync(scenario.Id, inactiveTeam.Id, radio.Id, 5);

        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.RunSimulationAsync(scenario.Id);

        Assert.True(result.Success);
        Assert.Equal(0, result.Value!.TotalActiveTeams);
        Assert.Equal(0, result.Value!.TeamsWithAllocations);
        Assert.Equal(0.00m, result.Value!.TeamAllocationCoveragePercent);
    }

    [Fact]
    public async Task RunSimulation_MultipleAllocationsForSameTeam_CountsTeamOnce()
    {
        var scenario = await CreateScenarioAsync("Multi-Alloc Exercise");
        var team1 = await CreateTeamAsync("Team 1", isActive: true);
        var team2 = await CreateTeamAsync("Team 2", isActive: true);
        var radio = await CreateEquipmentAsync("Radios", 20, 10);
        var laptop = await CreateEquipmentAsync("Laptops", 20, 10);

        // Team 1 receives both Radios and Laptops; Team 2 has zero allocations
        await CreateAllocationAsync(scenario.Id, team1.Id, radio.Id, 4);
        await CreateAllocationAsync(scenario.Id, team1.Id, laptop.Id, 2);

        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.RunSimulationAsync(scenario.Id);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.TotalActiveTeams);
        Assert.Equal(1, result.Value!.TeamsWithAllocations);
        Assert.Equal(2, result.Value!.TotalAllocations);
        Assert.Equal(6, result.Value!.TotalAllocatedEquipmentUnits);
        Assert.Equal(50.00m, result.Value!.TeamAllocationCoveragePercent);
    }

    // ==========================================
    // 3. SIMULATION EVENTS & PERSISTENCE
    // ==========================================

    [Fact]
    public async Task RunSimulation_SuccessfulRun_CreatesSimulationEvent()
    {
        var scenario = await CreateScenarioAsync("Event Test Scenario");
        var team = await CreateTeamAsync("Alpha Team");
        var radio = await CreateEquipmentAsync("Field Radios", 20, 15);
        await CreateAllocationAsync(scenario.Id, team.Id, radio.Id, 5);

        using (var context = CreateContext())
        {
            var service = new SimulationService(context);
            var result = await service.RunSimulationAsync(scenario.Id);
            Assert.True(result.Success);
        }

        using (var verifyContext = CreateContext())
        {
            var events = await verifyContext.SimulationEvents
                .Where(e => e.ScenarioId == scenario.Id)
                .ToListAsync();

            Assert.Single(events);
            var simEvent = events[0];
            Assert.Equal("SimulationExecuted", simEvent.EventType);
            Assert.Equal(scenario.Id, simEvent.ScenarioId);
            Assert.Contains("1 of 1 active teams", simEvent.Description);
            Assert.True((DateTimeOffset.UtcNow - simEvent.OccurredAtUtc).TotalMinutes < 1);
        }
    }

    [Fact]
    public async Task RunSimulation_FailedRun_DoesNotCreateSimulationEvent()
    {
        var scenario = await CreateScenarioAsync("No Allocations Scenario");

        using (var context = CreateContext())
        {
            var service = new SimulationService(context);
            var result = await service.RunSimulationAsync(scenario.Id);
            Assert.False(result.Success);
        }

        using (var verifyContext = CreateContext())
        {
            var events = await verifyContext.SimulationEvents
                .Where(e => e.ScenarioId == scenario.Id)
                .ToListAsync();

            Assert.Empty(events);
        }
    }

    [Fact]
    public async Task RunSimulation_RepeatedRuns_CreatesSeparateEventRecords()
    {
        var scenario = await CreateScenarioAsync("Repeated Run Scenario");
        var team = await CreateTeamAsync("Bravo Team");
        var radio = await CreateEquipmentAsync("Radios", 20, 15);
        await CreateAllocationAsync(scenario.Id, team.Id, radio.Id, 3);

        using (var context = CreateContext())
        {
            var service = new SimulationService(context);
            var run1 = await service.RunSimulationAsync(scenario.Id);
            var run2 = await service.RunSimulationAsync(scenario.Id);

            Assert.True(run1.Success);
            Assert.True(run2.Success);
        }

        using (var verifyContext = CreateContext())
        {
            var events = await verifyContext.SimulationEvents
                .Where(e => e.ScenarioId == scenario.Id)
                .ToListAsync();

            Assert.Equal(2, events.Count);
        }
    }

    [Fact]
    public async Task GetScenarioEvents_ReturnsEventsOrderedNewestFirst()
    {
        var scenario = await CreateScenarioAsync("Event Order Scenario");

        using (var context = CreateContext())
        {
            context.SimulationEvents.AddRange(
                new SimulationEvent
                {
                    ScenarioId = scenario.Id,
                    EventType = "SimulationExecuted",
                    Description = "Oldest event",
                    OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-30)
                },
                new SimulationEvent
                {
                    ScenarioId = scenario.Id,
                    EventType = "SimulationExecuted",
                    Description = "Middle event",
                    OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-15)
                },
                new SimulationEvent
                {
                    ScenarioId = scenario.Id,
                    EventType = "SimulationExecuted",
                    Description = "Newest event",
                    OccurredAtUtc = DateTimeOffset.UtcNow
                }
            );
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext())
        {
            var service = new SimulationService(context);
            var result = await service.GetScenarioEventsAsync(scenario.Id);

            Assert.True(result.Success);
            Assert.Equal(3, result.Value!.Count);
            Assert.Equal("Newest event", result.Value[0].Description);
            Assert.Equal("Middle event", result.Value[1].Description);
            Assert.Equal("Oldest event", result.Value[2].Description);
        }
    }

    [Fact]
    public async Task GetScenarioEvents_NonexistentScenario_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.GetScenarioEventsAsync(8888);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Contains("8888", result.ErrorMessage);
    }

    [Fact]
    public async Task GetScenarioEvents_ExistingScenarioWithNoEvents_ReturnsEmptyList()
    {
        var scenario = await CreateScenarioAsync("Zero Events Scenario");

        using var context = CreateContext();
        var service = new SimulationService(context);

        var result = await service.GetScenarioEventsAsync(scenario.Id);

        Assert.True(result.Success);
        Assert.Empty(result.Value!);
    }

    // ==========================================
    // 4. SIDE EFFECT INVARIANTS
    // ==========================================

    [Fact]
    public async Task RunSimulation_DoesNotModifyEquipmentInventory()
    {
        var scenario = await CreateScenarioAsync("Inventory Check Scenario");
        var team = await CreateTeamAsync("Team 1");
        var radio = await CreateEquipmentAsync("Radios", totalQuantity: 30, availableQuantity: 20);
        await CreateAllocationAsync(scenario.Id, team.Id, radio.Id, 10);

        using (var context = CreateContext())
        {
            var service = new SimulationService(context);
            var result = await service.RunSimulationAsync(scenario.Id);
            Assert.True(result.Success);
        }

        using (var verifyContext = CreateContext())
        {
            var equipment = await verifyContext.Equipment.FindAsync(radio.Id);
            Assert.NotNull(equipment);
            Assert.Equal(30, equipment.TotalQuantity);
            Assert.Equal(20, equipment.AvailableQuantity);
        }
    }

    [Fact]
    public async Task RunSimulation_DoesNotChangeScenarioStatus()
    {
        var scenario = await CreateScenarioAsync("Draft Status Scenario", status: ScenarioStatus.Draft);
        var team = await CreateTeamAsync("Team 1");
        var radio = await CreateEquipmentAsync("Radios", 20, 15);
        await CreateAllocationAsync(scenario.Id, team.Id, radio.Id, 5);

        using (var context = CreateContext())
        {
            var service = new SimulationService(context);
            var result = await service.RunSimulationAsync(scenario.Id);
            Assert.True(result.Success);
        }

        using (var verifyContext = CreateContext())
        {
            var persistedScenario = await verifyContext.Scenarios.FindAsync(scenario.Id);
            Assert.NotNull(persistedScenario);
            Assert.Equal(ScenarioStatus.Draft, persistedScenario.Status);
        }
    }
}

