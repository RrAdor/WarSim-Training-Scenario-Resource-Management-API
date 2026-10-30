using Microsoft.EntityFrameworkCore;
using WarSim.Models;

namespace WarSim.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Scenario> Scenarios => Set<Scenario>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Allocation> Allocations => Set<Allocation>();
    public DbSet<SimulationEvent> SimulationEvents => Set<SimulationEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Scenario entity configuration
        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(s => s.Description)
                .HasMaxLength(1000);

            entity.Property(s => s.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(s => s.CreatedAtUtc)
                .IsRequired();

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Scenario_Name_NotEmpty", "length(trim(\"Name\")) > 0");
            });
        });

        // 2. Team entity configuration
        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(t => t.MemberCount)
                .IsRequired();

            entity.Property(t => t.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Team_Name_NotEmpty", "length(trim(\"Name\")) > 0");
                t.HasCheckConstraint("CK_Team_MemberCount_NonNegative", "\"MemberCount\" >= 0");
            });
        });

        // 3. Equipment entity configuration
        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.TotalQuantity)
                .IsRequired();

            entity.Property(e => e.AvailableQuantity)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Equipment_Name_NotEmpty", "length(trim(\"Name\")) > 0");
                t.HasCheckConstraint("CK_Equipment_TotalQuantity_NonNegative", "\"TotalQuantity\" >= 0");
                t.HasCheckConstraint("CK_Equipment_AvailableQuantity_Valid", "\"AvailableQuantity\" >= 0 AND \"AvailableQuantity\" <= \"TotalQuantity\"");
            });
        });

        // 4. Allocation entity configuration
        modelBuilder.Entity<Allocation>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Quantity)
                .IsRequired();

            entity.Property(a => a.CreatedAtUtc)
                .IsRequired();

            // Unique composite index: (ScenarioId, TeamId, EquipmentId)
            entity.HasIndex(a => new { a.ScenarioId, a.TeamId, a.EquipmentId })
                .IsUnique();

            // Foreign keys with explicit DeleteBehavior.Restrict
            entity.HasOne(a => a.Scenario)
                .WithMany(s => s.Allocations)
                .HasForeignKey(a => a.ScenarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Team)
                .WithMany(t => t.Allocations)
                .HasForeignKey(a => a.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Equipment)
                .WithMany(e => e.Allocations)
                .HasForeignKey(a => a.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Allocation_Quantity_Positive", "\"Quantity\" > 0");
            });
        });

        // 5. SimulationEvent entity configuration
        modelBuilder.Entity<SimulationEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(60);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.OccurredAtUtc)
                .IsRequired();

            // Foreign key to Scenario with DeleteBehavior.Restrict
            entity.HasOne(e => e.Scenario)
                .WithMany(s => s.SimulationEvents)
                .HasForeignKey(e => e.ScenarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_SimulationEvent_EventType_NotEmpty", "length(trim(\"EventType\")) > 0");
                t.HasCheckConstraint("CK_SimulationEvent_Description_NotEmpty", "length(trim(\"Description\")) > 0");
            });
        });
    }
}

