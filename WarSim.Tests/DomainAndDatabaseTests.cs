using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WarSim.Data;
using WarSim.Models;
using Xunit;

namespace WarSim.Tests;

public class DomainAndDatabaseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;

    public DomainAndDatabaseTests()
    {
        // Open an in-memory SQLite connection. The database lives as long as this connection is open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // SQLite requires explicitly enabling foreign key enforcement per connection
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        _contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Ensure database tables, indexes, and constraints are created from the EF Core model
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_contextOptions);

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public void Scenario_Status_Defaults_To_Draft()
    {
        var scenario = new Scenario
        {
            Name = "Alpha Test Scenario"
        };

        Assert.Equal(ScenarioStatus.Draft, scenario.Status);
        Assert.True(scenario.CreatedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Can_Persist_And_Retrieve_Full_Domain_Graph()
    {
        using (var context = CreateContext())
        {
            var scenario = new Scenario
            {
                Name = "Urban Logistics Exercise",
                Description = "Testing administrative team allocations",
                Status = ScenarioStatus.Draft
            };
            var team = new Team
            {
                Name = "Alpha Medical",
                MemberCount = 6,
                IsActive = true
            };
            var equipment = new Equipment
            {
                Name = "First Aid Kit",
                TotalQuantity = 10,
                AvailableQuantity = 10,
                IsActive = true
            };

            context.Scenarios.Add(scenario);
            context.Teams.Add(team);
            context.Equipment.Add(equipment);
            context.SaveChanges();

            var allocation = new Allocation
            {
                ScenarioId = scenario.Id,
                TeamId = team.Id,
                EquipmentId = equipment.Id,
                Quantity = 3
            };
            var simEvent = new SimulationEvent
            {
                ScenarioId = scenario.Id,
                EventType = "Created",
                Description = "Scenario draft created"
            };

            context.Allocations.Add(allocation);
            context.SimulationEvents.Add(simEvent);
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            var savedScenario = context.Scenarios
                .Include(s => s.Allocations)
                    .ThenInclude(a => a.Team)
                .Include(s => s.Allocations)
                    .ThenInclude(a => a.Equipment)
                .Include(s => s.SimulationEvents)
                .Single(s => s.Name == "Urban Logistics Exercise");

            Assert.Equal(ScenarioStatus.Draft, savedScenario.Status);
            Assert.Single(savedScenario.Allocations);
            Assert.Single(savedScenario.SimulationEvents);

            var allocation = savedScenario.Allocations.First();
            Assert.Equal(3, allocation.Quantity);
            Assert.Equal("Alpha Medical", allocation.Team.Name);
            Assert.Equal("First Aid Kit", allocation.Equipment.Name);
        }
    }

    [Fact]
    public void Duplicate_Scenario_Team_Equipment_Allocation_Is_Prevented_By_Unique_Index()
    {
        using (var context = CreateContext())
        {
            var scenario = new Scenario { Name = "Exercise A" };
            var team = new Team { Name = "Team A", MemberCount = 4 };
            var equipment = new Equipment { Name = "Radio", TotalQuantity = 10, AvailableQuantity = 10 };

            context.AddRange(scenario, team, equipment);
            context.SaveChanges();

            // First allocation
            context.Allocations.Add(new Allocation
            {
                ScenarioId = scenario.Id,
                TeamId = team.Id,
                EquipmentId = equipment.Id,
                Quantity = 2
            });
            context.SaveChanges();

            // Second duplicate allocation (same scenario, team, equipment)
            context.Allocations.Add(new Allocation
            {
                ScenarioId = scenario.Id,
                TeamId = team.Id,
                EquipmentId = equipment.Id,
                Quantity = 1
            });

            // SQLite unique index violation
            Assert.Throws<DbUpdateException>(() => context.SaveChanges());
        }
    }

    [Fact]
    public void Allocation_With_Invalid_ForeignKey_Is_Rejected()
    {
        using var context = CreateContext();

        var team = new Team { Name = "Bravo Team", MemberCount = 5 };
        var equipment = new Equipment { Name = "Laptops", TotalQuantity = 5, AvailableQuantity = 5 };
        context.AddRange(team, equipment);
        context.SaveChanges();

        // Scenario ID 999 does not exist
        context.Allocations.Add(new Allocation
        {
            ScenarioId = 999,
            TeamId = team.Id,
            EquipmentId = equipment.Id,
            Quantity = 1
        });

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Equipment_AvailableQuantity_Exceeding_TotalQuantity_Is_Rejected_By_Check_Constraint()
    {
        using var context = CreateContext();

        var invalidEquipment = new Equipment
        {
            Name = "Training Vehicle",
            TotalQuantity = 5,
            AvailableQuantity = 10 // Violates AvailableQuantity <= TotalQuantity
        };

        context.Equipment.Add(invalidEquipment);
        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Equipment_Negative_Quantity_Is_Rejected_By_Check_Constraint()
    {
        using var context = CreateContext();

        var invalidEquipment = new Equipment
        {
            Name = "Generators",
            TotalQuantity = -5,
            AvailableQuantity = -5
        };

        context.Equipment.Add(invalidEquipment);
        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Allocation_Zero_Or_Negative_Quantity_Is_Rejected_By_Check_Constraint()
    {
        using var context = CreateContext();

        var scenario = new Scenario { Name = "Exercise B" };
        var team = new Team { Name = "Team B", MemberCount = 2 };
        var equipment = new Equipment { Name = "Lanterns", TotalQuantity = 10, AvailableQuantity = 10 };
        context.AddRange(scenario, team, equipment);
        context.SaveChanges();

        context.Allocations.Add(new Allocation
        {
            ScenarioId = scenario.Id,
            TeamId = team.Id,
            EquipmentId = equipment.Id,
            Quantity = 0 // Violates Quantity > 0
        });

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Team_Negative_MemberCount_Is_Rejected_By_Check_Constraint()
    {
        using var context = CreateContext();

        var invalidTeam = new Team
        {
            Name = "Invalid Team",
            MemberCount = -3
        };

        context.Teams.Add(invalidTeam);
        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Restrict_Delete_Behavior_Blocks_Scenario_Deletion_With_Allocations()
    {
        int scenarioId;

        // 1. Arrange: Create and persist scenario with dependent allocation
        using (var context = CreateContext())
        {
            var scenario = new Scenario { Name = "Restricted Scenario" };
            var team = new Team { Name = "Team R", MemberCount = 3 };
            var equipment = new Equipment { Name = "Gear", TotalQuantity = 10, AvailableQuantity = 10 };
            context.AddRange(scenario, team, equipment);
            context.SaveChanges();

            scenarioId = scenario.Id;

            context.Allocations.Add(new Allocation
            {
                ScenarioId = scenario.Id,
                TeamId = team.Id,
                EquipmentId = equipment.Id,
                Quantity = 2
            });
            context.SaveChanges();
        }

        // 2. Database-level verification: In a new context (child allocations not tracked in memory),
        // attempting to delete the scenario sends DELETE FROM Scenarios to SQLite, which
        // SQLite's FOREIGN KEY ... ON DELETE RESTRICT constraint rejects with DbUpdateException.
        using (var deleteContext = CreateContext())
        {
            var scenarioToDelete = deleteContext.Scenarios.Find(scenarioId);
            Assert.NotNull(scenarioToDelete);

            deleteContext.Scenarios.Remove(scenarioToDelete);
            Assert.Throws<DbUpdateException>(() => deleteContext.SaveChanges());
        }
    }
}
