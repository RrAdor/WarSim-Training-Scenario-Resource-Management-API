using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WarSim.Data;
using WarSim.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings (e.g. "Draft", "Ready")
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Configure SQLite Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register application service layer dependencies
builder.Services.AddScoped<IScenarioService, ScenarioService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IAllocationService, AllocationService>();
builder.Services.AddScoped<ISimulationService, SimulationService>();

// Configure OpenAPI for API documentation
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program accessible for integration testing
public partial class Program { }
