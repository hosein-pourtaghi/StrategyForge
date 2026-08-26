using StrategyForge.AI;
using StrategyForge.AI.Providers;
using StrategyForge.Analysis;
using StrategyForge.Domain.Configuration;
using System.Reflection;
using StrategyForge.Api.Services;
using StrategyForge.Infrastructure;
using StrategyForge.Orchestration;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
var configuration = builder.Configuration;

// --- Core Infrastructure ---
builder.Services.AddStrategyForgeInfrastructure(configuration);
builder.Services.AddStrategyForgeAnalysis();
builder.Services.AddStrategyForgeAI();
builder.Services.AddStrategyForgeOrchestration();
builder.Services.Configure<LlmProviderSettings>(configuration.GetSection(LlmProviderSettings.SectionName));
builder.Services.Configure<BackgroundSettings>(configuration.GetSection(BackgroundSettings.SectionName));

// --- Application Services ---
builder.Services.AddScoped<InstrumentService>();
builder.Services.AddScoped<MarketDataService>();
builder.Services.AddScoped<DataSourceService>();
builder.Services.AddScoped<EvidenceQueryPipeline>();
builder.Services.AddScoped<CrossSourceValidator>();

// --- Controllers ---
builder.Services.AddControllers();

// --- Swagger / OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "StrategyForge API",
        Version = "v1",
        Description = "StrategyForge — Evidence-driven financial market intelligence API for Iranian financial markets.\n\n" +
            "This API provides access to instrument resolution, market data acquisition, and data source management.\n\n" +
            "**Key Concepts:**\n" +
            "- **Canonical Instrument ID**: StrategyForge's internal instrument identity, independent of any source.\n" +
            "- **Source-Specific ID**: Provider-specific identifier (e.g., TSETMC InsCode).\n" +
            "- **Provenance**: Records where data came from, when it was fetched, and how fresh it is.\n" +
            "- **Freshness**: How old the data is and whether it exceeds configured staleness thresholds.\n" +
            "- **Quality**: Deterministic quality score based on completeness, consistency, and freshness.\n" +
            "- **Fallback**: Automatic use of an alternative source when the primary is unavailable."
    });

    // Include XML comments if available
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.SchemaFilter<StrategyForge.Api.Swagger.EnumSchemaFilter>();
});

// --- Health Checks ---
builder.Services.AddHealthChecks();

// --- JSON Configuration ---
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var app = builder.Build();

// --- Middleware ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "StrategyForge API v1");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at root in dev
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
