using StrategyForge.Domain.Configuration;
using StrategyForge.Analysis;
using StrategyForge.AI;
using StrategyForge.Infrastructure;
using StrategyForge.Orchestration;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
builder.Services.Configure<LlmSettings>(
    builder.Configuration.GetSection(LlmSettings.SectionName));
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection(DatabaseSettings.SectionName));
builder.Services.Configure<DataSourceSettings>(
    builder.Configuration.GetSection(DataSourceSettings.SectionName));

// --- StrategyForge Layers ---
// Each layer registers its own services via extension methods.
// Order matters: Infrastructure → Analysis → AI → Orchestration
builder.Services.AddStrategyForgeInfrastructure();
builder.Services.AddStrategyForgeAnalysis();
builder.Services.AddStrategyForgeAI();
builder.Services.AddStrategyForgeOrchestration();

// --- ASP.NET Core ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

var app = builder.Build();

// --- Middleware ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// --- Health Check Endpoint ---
app.MapGet("/health", () => Results.Ok(new { status = "healthy", version = "1.0.0" }))
    .WithName("HealthCheck")
    .WithOpenApi();

app.Run();
