using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WarSim.Controllers;
using WarSim.Data;
using WarSim.DTOs;
using WarSim.Models;
using WarSim.Services;
using Xunit;

namespace WarSim.Tests;

public class Phase2ServiceAndApiTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;

    public Phase2ServiceAndApiTests()
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
    // SCENARIO SERVICE TESTS
    // ==========================================

    [Fact]
    public async Task CreateScenario_WithValidData_ReturnsSuccessAndDefaultsToDraft()
    {
        using var context = CreateContext();
        var service = new ScenarioService(context);

        var request = new CreateScenarioRequest
        {
            Name = "Mountain Defense Drill",
            Description = "Logistics readiness drill"
        };

        var result = await service.CreateAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Id > 0);
        Assert.Equal("Mountain Defense Drill", result.Value.Name);
        Assert.Equal("Logistics readiness drill", result.Value.Description);
        Assert.Equal(ScenarioStatus.Draft, result.Value.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateScenario_WithEmptyOrWhitespaceName_ReturnsBadRequest(string? invalidName)
    {
        using var context = CreateContext();
        var service = new ScenarioService(context);

        var request = new CreateScenarioRequest
        {
            Name = invalidName!,
            Description = "Sample"
        };

        var result = await service.CreateAsync(request);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
    }

    [Fact]
    public async Task GetAllScenarios_ReturnsAllPersistedScenarios()
    {
        using (var context = CreateContext())
        {
            var service = new ScenarioService(context);
            await service.CreateAsync(new CreateScenarioRequest { Name = "Scenario 1" });
            await service.CreateAsync(new CreateScenarioRequest { Name = "Scenario 2" });
        }

        using (var context = CreateContext())
        {
            var service = new ScenarioService(context);
            var scenarios = await service.GetAllAsync();

            Assert.Equal(2, scenarios.Count);
            Assert.Contains(scenarios, s => s.Name == "Scenario 1");
            Assert.Contains(scenarios, s => s.Name == "Scenario 2");
        }
    }

    [Fact]
    public async Task GetScenarioById_WithValidId_ReturnsScenario()
    {
        int scenarioId;
        using (var context = CreateContext())
        {
            var service = new ScenarioService(context);
            var created = await service.CreateAsync(new CreateScenarioRequest { Name = "Findable Scenario" });
            scenarioId = created.Value!.Id;
        }

        using (var context = CreateContext())
        {
            var service = new ScenarioService(context);
            var scenario = await service.GetByIdAsync(scenarioId);

            Assert.NotNull(scenario);
            Assert.Equal("Findable Scenario", scenario.Name);
        }
    }

    [Fact]
    public async Task GetScenarioById_WithNonExistentId_ReturnsNull()
    {
        using var context = CreateContext();
        var service = new ScenarioService(context);

        var scenario = await service.GetByIdAsync(9999);
        Assert.Null(scenario);
    }

    [Fact]
    public async Task UpdateScenario_WhenDraft_UpdatesNameAndDescription()
    {
        int scenarioId;
        using (var context = CreateContext())
        {
            var service = new ScenarioService(context);
            var created = await service.CreateAsync(new CreateScenarioRequest
            {
                Name = "Original Name",
                Description = "Original Desc"
            });
            scenarioId = created.Value!.Id;
        }

        using (var context = CreateContext())
        {
            var service = new ScenarioService(context);
            var result = await service.UpdateAsync(scenarioId, new UpdateScenarioRequest
            {
                Name = "Updated Name",
                Description = "Updated Desc"
            });

            Assert.True(result.Success);
            Assert.Equal("Updated Name", result.Value!.Name);
            Assert.Equal("Updated Desc", result.Value.Description);
        }
    }

    [Fact]
    public async Task UpdateScenario_WhenNotDraft_ReturnsBadRequest()
    {
        int scenarioId;
        using (var context = CreateContext())
        {
            var scenario = new Scenario
            {
                Name = "Ready Scenario",
                Status = ScenarioStatus.Ready
            };
            context.Scenarios.Add(scenario);
            await context.SaveChangesAsync();
            scenarioId = scenario.Id;
        }

        using (var context = CreateContext())
        {
            var service = new ScenarioService(context);
            var result = await service.UpdateAsync(scenarioId, new UpdateScenarioRequest
            {
                Name = "Attempted Change"
            });

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
            Assert.Contains("Only draft scenarios may be edited", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task UpdateScenario_WhenNotFound_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = new ScenarioService(context);

        var result = await service.UpdateAsync(9999, new UpdateScenarioRequest
        {
            Name = "Missing Scenario"
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteScenario_WithoutDependencies_DeletesSuccessfully()
    {
        int scenarioId;
        using (var context = CreateContext())
        {
            var service = new ScenarioService(context);
            var created = await service.CreateAsync(new CreateScenarioRequest { Name = "Delete Me" });
            scenarioId = created.Value!.Id;
        }

        using (var context = CreateContext())
        {
            var service = new ScenarioService(context);
            var result = await service.DeleteAsync(scenarioId);
            Assert.True(result.Success);

            var deleted = await service.GetByIdAsync(scenarioId);
            Assert.Null(deleted);
        }
    }

    [Fact]
    public async Task DeleteScenario_WithAllocations_ReturnsBadRequest()
    {
        int scenarioId;
        using (var context = CreateContext())
        {
            var scenario = new Scenario { Name = "Scenario With Allocations" };
            var team = new Team { Name = "Team Alpha", MemberCount = 5 };
            var equipment = new Equipment { Name = "Radios", TotalQuantity = 10, AvailableQuantity = 10 };
            context.AddRange(scenario, team, equipment);
            await context.SaveChangesAsync();

            context.Allocations.Add(new Allocation
            {
                ScenarioId = scenario.Id,
                TeamId = team.Id,
                EquipmentId = equipment.Id,
                Quantity = 2
            });
            await context.SaveChangesAsync();
            scenarioId = scenario.Id;
        }

        using (var context = CreateContext())
        {
            var service = new ScenarioService(context);
            var result = await service.DeleteAsync(scenarioId);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
            Assert.Contains("active allocations", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task DeleteScenario_WithSimulationEvents_ReturnsBadRequest()
    {
        int scenarioId;
        using (var context = CreateContext())
        {
            var scenario = new Scenario { Name = "Scenario With Events" };
            context.Scenarios.Add(scenario);
            await context.SaveChangesAsync();

            context.SimulationEvents.Add(new SimulationEvent
            {
                ScenarioId = scenario.Id,
                EventType = "Created",
                Description = "Initial event"
            });
            await context.SaveChangesAsync();
            scenarioId = scenario.Id;
        }

        using (var context = CreateContext())
        {
            var service = new ScenarioService(context);
            var result = await service.DeleteAsync(scenarioId);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
            Assert.Contains("simulation events", result.ErrorMessage);
        }
    }

    // ==========================================
    // TEAM SERVICE TESTS
    // ==========================================

    [Fact]
    public async Task CreateTeam_WithValidData_ReturnsSuccessAndSetsActive()
    {
        using var context = CreateContext();
        var service = new TeamService(context);

        var result = await service.CreateAsync(new CreateTeamRequest
        {
            Name = "Bravo Recon",
            MemberCount = 8
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal("Bravo Recon", result.Value.Name);
        Assert.Equal(8, result.Value.MemberCount);
        Assert.True(result.Value.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTeam_WithEmptyName_ReturnsBadRequest(string invalidName)
    {
        using var context = CreateContext();
        var service = new TeamService(context);

        var result = await service.CreateAsync(new CreateTeamRequest
        {
            Name = invalidName,
            MemberCount = 5
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
    }

    [Fact]
    public async Task CreateTeam_WithNegativeMemberCount_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var service = new TeamService(context);

        var result = await service.CreateAsync(new CreateTeamRequest
        {
            Name = "Invalid Members Team",
            MemberCount = -4
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
    }

    [Fact]
    public async Task GetAllTeams_ReturnsAllPersistedTeams()
    {
        using (var context = CreateContext())
        {
            var service = new TeamService(context);
            await service.CreateAsync(new CreateTeamRequest { Name = "Team Alpha", MemberCount = 3 });
            await service.CreateAsync(new CreateTeamRequest { Name = "Team Bravo", MemberCount = 4 });
        }

        using (var context = CreateContext())
        {
            var service = new TeamService(context);
            var teams = await service.GetAllAsync();

            Assert.Equal(2, teams.Count);
        }
    }

    // ==========================================
    // EQUIPMENT SERVICE TESTS
    // ==========================================

    [Fact]
    public async Task CreateEquipment_WithValidData_SetsAvailableQuantityEqualToTotalQuantity()
    {
        using var context = CreateContext();
        var service = new EquipmentService(context);

        var result = await service.CreateAsync(new CreateEquipmentRequest
        {
            Name = "Training Laptops",
            TotalQuantity = 15
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal("Training Laptops", result.Value.Name);
        Assert.Equal(15, result.Value.TotalQuantity);
        Assert.Equal(15, result.Value.AvailableQuantity); // Server must set AvailableQuantity = TotalQuantity
        Assert.True(result.Value.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateEquipment_WithEmptyName_ReturnsBadRequest(string invalidName)
    {
        using var context = CreateContext();
        var service = new EquipmentService(context);

        var result = await service.CreateAsync(new CreateEquipmentRequest
        {
            Name = invalidName,
            TotalQuantity = 10
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
    }

    [Fact]
    public async Task CreateEquipment_WithNegativeTotalQuantity_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var service = new EquipmentService(context);

        var result = await service.CreateAsync(new CreateEquipmentRequest
        {
            Name = "Negative Gear",
            TotalQuantity = -10
        });

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
    }

    [Fact]
    public async Task GetAllEquipment_ReturnsAllPersistedEquipment()
    {
        using (var context = CreateContext())
        {
            var service = new EquipmentService(context);
            await service.CreateAsync(new CreateEquipmentRequest { Name = "Lanterns", TotalQuantity = 10 });
            await service.CreateAsync(new CreateEquipmentRequest { Name = "Radios", TotalQuantity = 20 });
        }

        using (var context = CreateContext())
        {
            var service = new EquipmentService(context);
            var equipment = await service.GetAllAsync();

            Assert.Equal(2, equipment.Count);
        }
    }

    // ==========================================
    // CONTROLLER HTTP BEHAVIOR TESTS
    // ==========================================

    [Fact]
    public async Task ScenariosController_Create_ReturnsCreatedAtAction()
    {
        using var context = CreateContext();
        var service = new ScenarioService(context);
        var controller = new ScenariosController(service);

        var response = await controller.Create(new CreateScenarioRequest
        {
            Name = "Controller Scenario"
        }, default);

        var createdAtResult = Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Equal(201, createdAtResult.StatusCode);
        var scenario = Assert.IsType<ScenarioResponse>(createdAtResult.Value);
        Assert.Equal("Controller Scenario", scenario.Name);
    }

    [Fact]
    public async Task TeamsController_Create_ReturnsCreatedAtAction()
    {
        using var context = CreateContext();
        var service = new TeamService(context);
        var controller = new TeamsController(service);

        var response = await controller.Create(new CreateTeamRequest
        {
            Name = "Controller Team",
            MemberCount = 6
        }, default);

        var createdAtResult = Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Equal(201, createdAtResult.StatusCode);
        var team = Assert.IsType<TeamResponse>(createdAtResult.Value);
        Assert.Equal("Controller Team", team.Name);
    }

    [Fact]
    public async Task EquipmentController_Create_ReturnsCreatedAtAction()
    {
        using var context = CreateContext();
        var service = new EquipmentService(context);
        var controller = new EquipmentController(service);

        var response = await controller.Create(new CreateEquipmentRequest
        {
            Name = "Controller Radios",
            TotalQuantity = 12
        }, default);

        var createdAtResult = Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Equal(201, createdAtResult.StatusCode);
        var equipment = Assert.IsType<EquipmentResponse>(createdAtResult.Value);
        Assert.Equal(12, equipment.TotalQuantity);
        Assert.Equal(12, equipment.AvailableQuantity);
    }
}

