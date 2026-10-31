using System;
using System.ComponentModel.DataAnnotations;
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

public class Phase5PolishAndBoundaryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;

    public Phase5PolishAndBoundaryTests()
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

    // ==========================================
    // 1. SCENARIOS CONTROLLER STATUS CODES
    // ==========================================

    [Fact]
    public async Task ScenariosController_GetById_NonExistent_Returns404ProblemDetails()
    {
        using var context = CreateContext();
        var service = new ScenarioService(context);
        var controller = new ScenariosController(service);

        var response = await controller.GetById(9999, default);

        var notFound = Assert.IsType<NotFoundObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task ScenariosController_Update_DraftScenario_Returns200Ok()
    {
        using var context = CreateContext();
        var scenario = new Scenario { Name = "Draft Scenario", Status = ScenarioStatus.Draft };
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();

        var service = new ScenarioService(context);
        var controller = new ScenariosController(service);

        var response = await controller.Update(scenario.Id, new UpdateScenarioRequest
        {
            Name = "Updated Name",
            Description = "Updated Description"
        }, default);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var updated = Assert.IsType<ScenarioResponse>(okResult.Value);
        Assert.Equal("Updated Name", updated.Name);
    }

    [Fact]
    public async Task ScenariosController_Update_NonDraftScenario_Returns400ProblemDetails()
    {
        using var context = CreateContext();
        var scenario = new Scenario { Name = "Ready Scenario", Status = ScenarioStatus.Ready };
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();

        var service = new ScenarioService(context);
        var controller = new ScenariosController(service);

        var response = await controller.Update(scenario.Id, new UpdateScenarioRequest
        {
            Name = "Attempted Update"
        }, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problem.Status);
        Assert.Contains("Only draft scenarios may be edited", problem.Detail);
    }

    [Fact]
    public async Task ScenariosController_Update_NonExistent_Returns404ProblemDetails()
    {
        using var context = CreateContext();
        var service = new ScenarioService(context);
        var controller = new ScenariosController(service);

        var response = await controller.Update(9999, new UpdateScenarioRequest
        {
            Name = "Missing Scenario"
        }, default);

        var notFound = Assert.IsType<NotFoundObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task ScenariosController_Delete_WithAllocations_Returns400ProblemDetails()
    {
        using var context = CreateContext();
        var scenario = new Scenario { Name = "Scenario With Allocations", Status = ScenarioStatus.Draft };
        var team = new Team { Name = "Team A", MemberCount = 5, IsActive = true };
        var equipment = new Equipment { Name = "Equipment A", TotalQuantity = 10, AvailableQuantity = 10, IsActive = true };
        context.AddRange(scenario, team, equipment);
        await context.SaveChangesAsync();

        var allocation = new Allocation
        {
            ScenarioId = scenario.Id,
            TeamId = team.Id,
            EquipmentId = equipment.Id,
            Quantity = 2,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        context.Allocations.Add(allocation);
        await context.SaveChangesAsync();

        var service = new ScenarioService(context);
        var controller = new ScenariosController(service);

        var response = await controller.Delete(scenario.Id, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problem.Status);
        Assert.Contains("Cannot delete scenario with active allocations", problem.Detail);
    }

    [Fact]
    public async Task ScenariosController_Delete_WithSimulationEvents_Returns400ProblemDetails()
    {
        using var context = CreateContext();
        var scenario = new Scenario { Name = "Scenario With Events", Status = ScenarioStatus.Draft };
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();

        var simEvent = new SimulationEvent
        {
            ScenarioId = scenario.Id,
            EventType = "SimulationExecuted",
            Description = "Audit log",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };
        context.SimulationEvents.Add(simEvent);
        await context.SaveChangesAsync();

        var service = new ScenarioService(context);
        var controller = new ScenariosController(service);

        var response = await controller.Delete(scenario.Id, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problem.Status);
        Assert.Contains("Cannot delete scenario with recorded simulation events", problem.Detail);
    }

    [Fact]
    public async Task ScenariosController_Delete_NonExistent_Returns404ProblemDetails()
    {
        using var context = CreateContext();
        var service = new ScenarioService(context);
        var controller = new ScenariosController(service);

        var response = await controller.Delete(8888, default);

        var notFound = Assert.IsType<NotFoundObjectResult>(response);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problem.Status);
    }

    // ==========================================
    // 2. TEAMS & EQUIPMENT CONTROLLER STATUS CODES
    // ==========================================

    [Fact]
    public async Task TeamsController_GetById_NonExistent_Returns404ProblemDetails()
    {
        using var context = CreateContext();
        var service = new TeamService(context);
        var controller = new TeamsController(service);

        var response = await controller.GetById(9999, default);

        var notFound = Assert.IsType<NotFoundObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task EquipmentController_GetById_NonExistent_Returns404ProblemDetails()
    {
        using var context = CreateContext();
        var service = new EquipmentService(context);
        var controller = new EquipmentController(service);

        var response = await controller.GetById(9999, default);

        var notFound = Assert.IsType<NotFoundObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problem.Status);
    }

    // ==========================================
    // 3. INVENTORY RESTORATION BOUNDARY GUARD
    // ==========================================

    [Fact]
    public async Task DeleteAllocation_WhenRestoredStockExceedsTotal_CapsAvailableQuantityAtTotal()
    {
        using var context = CreateContext();
        var scenario = new Scenario { Name = "Cap Test Scenario" };
        var team = new Team { Name = "Team Cap" };
        // Equipment where TotalQuantity is 10, but AvailableQuantity somehow is 9 and allocation quantity is 3
        var equipment = new Equipment { Name = "Test Gear", TotalQuantity = 10, AvailableQuantity = 9 };
        context.AddRange(scenario, team, equipment);
        await context.SaveChangesAsync();

        var allocation = new Allocation
        {
            ScenarioId = scenario.Id,
            TeamId = team.Id,
            EquipmentId = equipment.Id,
            Quantity = 3,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        context.Allocations.Add(allocation);
        await context.SaveChangesAsync();

        var service = new AllocationService(context);
        var result = await service.DeleteAsync(allocation.Id);

        Assert.True(result.Success);

        using var verifyContext = CreateContext();
        var updatedEquipment = await verifyContext.Equipment.FindAsync(equipment.Id);
        Assert.NotNull(updatedEquipment);
        // Invariant guard must cap at TotalQuantity (10), not allow 12
        Assert.Equal(10, updatedEquipment.AvailableQuantity);
        Assert.Equal(10, updatedEquipment.TotalQuantity);
    }

    // ==========================================
    // 4. DATA ANNOTATIONS VALIDATION RULES
    // ==========================================

    [Fact]
    public void CreateScenarioRequest_ValidationAttributes_RejectInvalidNames()
    {
        var validDto = new CreateScenarioRequest { Name = new string('A', 120) };
        var results = new System.Collections.Generic.List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(validDto, new ValidationContext(validDto), results, true);
        Assert.True(isValid);

        var tooLongDto = new CreateScenarioRequest { Name = new string('A', 121) };
        results.Clear();
        bool isTooLongValid = Validator.TryValidateObject(tooLongDto, new ValidationContext(tooLongDto), results, true);
        Assert.False(isTooLongValid);

        var emptyDto = new CreateScenarioRequest { Name = "" };
        results.Clear();
        bool isEmptyValid = Validator.TryValidateObject(emptyDto, new ValidationContext(emptyDto), results, true);
        Assert.False(isEmptyValid);
    }

    [Fact]
    public void CreateTeamRequest_ValidationAttributes_RejectInvalidNamesAndNegativeMembers()
    {
        var validDto = new CreateTeamRequest { Name = "Valid Team", MemberCount = 0 };
        var results = new System.Collections.Generic.List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(validDto, new ValidationContext(validDto), results, true);
        Assert.True(isValid);

        var negativeMembersDto = new CreateTeamRequest { Name = "Valid Team", MemberCount = -1 };
        results.Clear();
        bool isNegativeValid = Validator.TryValidateObject(negativeMembersDto, new ValidationContext(negativeMembersDto), results, true);
        Assert.False(isNegativeValid);

        var tooLongNameDto = new CreateTeamRequest { Name = new string('T', 101), MemberCount = 5 };
        results.Clear();
        bool isTooLongValid = Validator.TryValidateObject(tooLongNameDto, new ValidationContext(tooLongNameDto), results, true);
        Assert.False(isTooLongValid);
    }

    [Fact]
    public void CreateEquipmentRequest_ValidationAttributes_RejectInvalidQuantitiesAndNames()
    {
        var validDto = new CreateEquipmentRequest { Name = "Valid Gear", TotalQuantity = 100 };
        var results = new System.Collections.Generic.List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(validDto, new ValidationContext(validDto), results, true);
        Assert.True(isValid);

        var negativeTotalDto = new CreateEquipmentRequest { Name = "Valid Gear", TotalQuantity = -5 };
        results.Clear();
        bool isNegativeValid = Validator.TryValidateObject(negativeTotalDto, new ValidationContext(negativeTotalDto), results, true);
        Assert.False(isNegativeValid);

        var tooLongNameDto = new CreateEquipmentRequest { Name = new string('G', 101), TotalQuantity = 10 };
        results.Clear();
        bool isTooLongValid = Validator.TryValidateObject(tooLongNameDto, new ValidationContext(tooLongNameDto), results, true);
        Assert.False(isTooLongValid);
    }
}

