# StrategyForge — Architecture Document

**Version:** 1.0  
**Last Updated:** August 21, 2026

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Project Structure](#2-project-structure)
3. [Dependency Rules](#3-dependency-rules)
4. [Layer Details](#4-layer-details)
5. [Data Flow](#5-data-flow)
6. [Interface Contracts](#6-interface-contracts)
7. [Configuration](#7-configuration)
8. [Dependency Injection](#8-dependency-injection)
9. [Error Handling](#9-error-handling)
10. [Logging](#10-logging)
11. [Testing Strategy](#11-testing-strategy)
12. [Security](#12-security)
13. [Performance](#13-performance)
14. [Deployment](#14-deployment)

---

## 1. Architecture Overview

StrategyForge follows **Clean Architecture** principles with clear separation of concerns.

### Design Principles

1. **Domain Independence:** Core models and interfaces have zero external dependencies
2. **Dependency Inversion:** High-level modules depend on abstractions, not implementations
3. **Single Responsibility:** Each project has one clear purpose
4. **Open/Closed:** Open for extension, closed for modification
5. **Interface Segregation:** Small, focused interfaces

### Layer Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        API LAYER                                │
│                   (ASP.NET Core Web API)                        │
│              HTTP endpoints, request/response                   │
│                     StrategyForge.Api                           │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                   ORCHESTRATION LAYER                            │
│              (StrategyOrchestrator)                              │
│         Coordinates the full analysis pipeline                  │
│                  StrategyForge.Orchestration                     │
└───┬─────────────────────┬──────────────────────┬────────────────┘
    │                     │                      │
    ▼                     ▼                      ▼
┌──────────┐    ┌─────────────────┐    ┌───────────────────┐
│  DATA    │    │   ANALYSIS      │    │   AI STRATEGY     │
│  LAYER   │    │   LAYER         │    │   LAYER           │
│          │    │                 │    │                   │
│Infrastruc│    │   StrategyForge │    │  StrategyForge.AI │
│   ture   │    │   .Analysis     │    │                   │
└──────────┘    └─────────────────┘    └───────────────────┘
         │               │                      │
         └───────────────┴──────────────────────┘
                             │
                             ▼
                  ┌─────────────────────┐
                  │   DOMAIN LAYER      │
                  │  (Core Models &     │
                  │   Interfaces)       │
                  │  StrategyForge.     │
                  │     Domain          │
                  └─────────────────────┘
```

---

## 2. Project Structure

```
StrategyForge/
├── StrategyForge.sln
├── README.md
├── docs/
│   ├── VISION.md
│   ├── ARCHITECTURE.md
│   ├── ROADMAP.md
│   ├── API.md
│   ├── DATA_MODELS.md
│   ├── INTERFACES.md
│   └── DEVELOPMENT.md
├── src/
│   ├── StrategyForge.Domain/
│   │   ├── Enums/
│   │   │   ├── AssetType.cs
│   │   │   ├── DataSourceType.cs
│   │   │   ├── EvidenceType.cs
│   │   │   ├── MarketRegime.cs
│   │   │   ├── Sentiment.cs
│   │   │   └── TimeHorizon.cs
│   │   ├── Models/
│   │   │   ├── Asset.cs
│   │   │   ├── Candle.cs
│   │   │   ├── DataMetadata.cs
│   │   │   ├── MarketDataBundle.cs
│   │   │   ├── NewsItem.cs
│   │   │   ├── CompanyInfo.cs
│   │   │   ├── EconomicIndicator.cs
│   │   │   ├── CurrencyRate.cs
│   │   │   ├── GoldPrice.cs
│   │   │   ├── IndicatorResult.cs
│   │   │   ├── IndicatorParameters.cs
│   │   │   ├── IndicatorEngineResult.cs
│   │   │   ├── AnalysisEvidence.cs
│   │   │   ├── LlmRequest.cs
│   │   │   ├── LlmResponse.cs
│   │   │   ├── AgentAnalysisResult.cs
│   │   │   ├── StrategyReport.cs
│   │   │   ├── ExecutiveSummary.cs
│   │   │   ├── MarketContext.cs
│   │   │   ├── Scenario.cs
│   │   │   ├── StrategySection.cs
│   │   │   ├── RiskRewardAssessment.cs
│   │   │   ├── ConfidenceAssessment.cs
│   │   │   └── EvidenceItem.cs
│   │   ├── Interfaces/
│   │   │   ├── Providers/
│   │   │   │   ├── IMarketDataProvider.cs
│   │   │   │   ├── INewsProvider.cs
│   │   │   │   ├── IEconomicDataProvider.cs
│   │   │   │   ├── ICompanyDataProvider.cs
│   │   │   │   ├── ICurrencyProvider.cs
│   │   │   │   ├── IGoldPriceProvider.cs
│   │   │   │   └── IAssetRepository.cs
│   │   │   ├── Analysis/
│   │   │   │   ├── IIndicator.cs
│   │   │   │   ├── IIndicatorEngine.cs
│   │   │   │   └── IAnalyzer.cs
│   │   │   ├── AI/
│   │   │   │   ├── ILLMProvider.cs
│   │   │   │   └── IAgent.cs
│   │   │   └── Orchestration/
│   │   │       └── IStrategyOrchestrator.cs
│   │   └── Configuration/
│   │       ├── LlmSettings.cs
│   │       ├── DatabaseSettings.cs
│   │       └── DataSourceSettings.cs
│   ├── StrategyForge.Infrastructure/
│   │   └── ServiceCollectionExtensions.cs
│   ├── StrategyForge.Analysis/
│   │   ├── IndicatorEngine.cs
│   │   └── ServiceCollectionExtensions.cs
│   ├── StrategyForge.AI/
│   │   └── ServiceCollectionExtensions.cs
│   ├── StrategyForge.Orchestration/
│   │   ├── StrategyOrchestrator.cs
│   │   └── ServiceCollectionExtensions.cs
│   └── StrategyForge.Api/
│       ├── Program.cs
│       ├── appsettings.json
│       └── Properties/
│           └── launchSettings.json
└── tests/
    ├── StrategyForge.Domain.Tests/
    │   └── Models/
    │       ├── AssetTests.cs
    │       ├── CandleTests.cs
    │       ├── IndicatorResultTests.cs
    │       └── StrategyReportTests.cs
    ├── StrategyForge.Analysis.Tests/
    ├── StrategyForge.AI.Tests/
    ├── StrategyForge.Orchestration.Tests/
    └── StrategyForge.Integration.Tests/
```

---

## 3. Dependency Rules

### Allowed Dependencies

```
StrategyForge.Domain          → (nothing)
StrategyForge.Infrastructure  → Domain
StrategyForge.Analysis        → Domain
StrategyForge.AI              → Domain
StrategyForge.Orchestration   → Domain, Analysis, AI, Infrastructure
StrategyForge.Api             → All projects (composition root)
```

### Forbidden Dependencies

| From | To | Reason |
|------|-----|--------|
| Domain | Any other project | Must remain independent |
| Analysis | Infrastructure, AI, Orchestration | Must be technology-agnostic |
| AI | Infrastructure, Analysis, Orchestration | Must be technology-agnostic |
| Any project | Specific LLM implementation | Must use ILLMProvider abstraction |

### Rationale

Domain independence ensures core models and interfaces remain stable when external technologies change. Analysis and AI remain technology-agnostic, allowing implementations to be swapped without touching the rest of the system.

---

## 4. Layer Details

### 4.1 Domain Layer (`StrategyForge.Domain`)

**Purpose:** Core domain models, enums, interfaces, and contracts.

**Dependencies:** None (zero external dependencies beyond .NET BCL)

**Contains:**
- Enums: `AssetType`, `TimeHorizon`, `Sentiment`, `DataSourceType`, `EvidenceType`, `MarketRegime`
- Models: `Asset`, `Candle`, `MarketDataBundle`, `NewsItem`, `CompanyInfo`, etc.
- Provider interfaces: `IMarketDataProvider`, `INewsProvider`, etc.
- Analysis interfaces: `IIndicator`, `IIndicatorEngine`, `IAnalyzer`
- AI interfaces: `ILLMProvider`, `IAgent`
- Orchestration interface: `IStrategyOrchestrator`
- Configuration models: `LlmSettings`, `DatabaseSettings`, `DataSourceSettings`

### 4.2 Infrastructure Layer (`StrategyForge.Infrastructure`)

**Purpose:** External integrations — data providers, database, LLM client.

**Dependencies:** `StrategyForge.Domain`

**Contains:**
- Data provider implementations (TSETMC, etc.)
- Database context and repositories
- LLM provider implementation
- HttpClient configuration
- DI extension methods

### 4.3 Analysis Layer (`StrategyForge.Analysis`)

**Purpose:** Deterministic calculations and indicator computation.

**Dependencies:** `StrategyForge.Domain`

**Contains:**
- Indicator implementations (RSI, MACD, SMA, EMA, Bollinger, ATR)
- `IndicatorEngine` with registration system
- Analyzer implementations (TrendDetector, SupportResistance, etc.)
- DI extension methods

### 4.4 AI Layer (`StrategyForge.AI`)

**Purpose:** LLM-powered agents and prompts.

**Dependencies:** `StrategyForge.Domain`

**Contains:**
- Agent implementations (TechnicalAnalyst, FundamentalAnalyst, etc.)
- LLM provider implementation (OpenAI-compatible)
- Prompt templates
- Response parsing and validation
- DI extension methods

### 4.5 Orchestration Layer (`StrategyForge.Orchestration`)

**Purpose:** Pipeline coordination.

**Dependencies:** `StrategyForge.Domain`, `StrategyForge.Analysis`, `StrategyForge.AI`, `StrategyForge.Infrastructure`

**Contains:**
- `StrategyOrchestrator` implementation
- Pipeline coordination logic
- Data collection orchestration
- Analysis orchestration
- Agent orchestration
- DI extension methods

### 4.6 API Layer (`StrategyForge.Api`)

**Purpose:** ASP.NET Core Web API endpoints.

**Dependencies:** All projects (composition root)

**Contains:**
- `Program.cs` with DI configuration
- Controllers (Assets, Analysis, Strategy)
- Middleware
- `appsettings.json` configuration
- Swagger/OpenAPI documentation

---

## 5. Data Flow

### 5.1 Pipeline Stages

```
1. USER REQUEST
   "Analyze asset X and generate a strategy"
       │
       ▼
2. ORCHESTRATOR resolves asset identity
   - Look up asset in repository
   - Validate asset exists
       │
       ▼
3. DATA LAYER collects all available information
   - Call market data providers
   - Call company data providers
   - Call economic data providers
   - Call news providers
   - Call currency providers
   - Call gold price providers
   - Aggregate into MarketDataBundle
       │
       ▼
4. ANALYSIS LAYER processes the data bundle
   - Run indicator engine on candle data
   - Run analyzer modules
   - Build AnalysisEvidence
       │
       ▼
5. SPECIALIZED AI AGENTS analyze evidence independently
   - Each agent receives relevant portions of evidence
   - Each agent calls LLM with structured prompt
   - Each agent parses response into AgentAnalysisResult
       │
       ▼
6. STRATEGY AGENT synthesizes all specialist outputs
   - Receives all AgentAnalysisResult outputs
   - Reasons about agreements and conflicts
   - Constructs scenarios
   - Produces StrategyReport
       │
       ▼
7. API returns StrategyReport to client
```

### 5.2 Data Models Flow

```
External Source
    ↓ (HTTP / scraping)
Raw Provider Response
    ↓ (Provider normalization)
Domain Models (Candle, CompanyInfo, NewsItem, etc.)
    ↓ (Aggregated into)
MarketDataBundle
    ↓ (Indicator computation)
IndicatorResult[]
    ↓ (Compiled into)
AnalysisEvidence
    ↓ (Sent to agents)
AgentAnalysisResult[]
    ↓ (Strategy synthesis)
StrategyReport
```

---

## 6. Interface Contracts

### 6.1 Provider Interfaces

#### IMarketDataProvider

```csharp
public interface IMarketDataProvider
{
    string Name { get; }
    Task<IReadOnlyList<Candle>> GetHistoricalDataAsync(
        Asset asset, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);
    Task<Candle?> GetLatestCandleAsync(
        Asset asset, CancellationToken cancellationToken = default);
    bool Supports(Asset asset);
}
```

**Contract:**
- Returns candles ordered oldest to newest
- Returns empty list if no data available (not null)
- Throws on network errors (not silent failure)
- Preserves data provenance via Candle.Metadata

#### INewsProvider

```csharp
public interface INewsProvider
{
    string Name { get; }
    Task<IReadOnlyList<NewsItem>> GetRecentNewsAsync(
        Asset asset, int maxItems = 20,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsItem>> GetMarketNewsAsync(
        IReadOnlyList<string> topics, int maxItems = 20,
        CancellationToken cancellationToken = default);
    bool Supports(Asset asset);
}
```

**Contract:**
- Returns news ordered most recent first
- Preserves source attribution
- Preserves publication timestamp

#### IEconomicDataProvider

```csharp
public interface IEconomicDataProvider
{
    string Name { get; }
    Task<IReadOnlyList<EconomicIndicator>> GetIndicatorsAsync(
        CancellationToken cancellationToken = default);
    Task<EconomicIndicator?> GetIndicatorAsync(
        string indicatorName,
        CancellationToken cancellationToken = default);
}
```

### 6.2 Analysis Interfaces

#### IIndicator

```csharp
public interface IIndicator
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<IndicatorResult> Compute(
        IReadOnlyList<Candle> candles,
        IndicatorParameters? parameters = null);
}
```

**Contract:**
- Pure function (no side effects)
- Deterministic (same input → same output)
- Returns empty list if insufficient data
- Does not throw on insufficient data (returns empty)

#### IIndicatorEngine

```csharp
public interface IIndicatorEngine
{
    IReadOnlyList<IIndicator> RegisteredIndicators { get; }
    IndicatorEngineResult ComputeAll(
        IReadOnlyList<Candle> candles,
        IndicatorConfiguration? configuration = null);
}
```

### 6.3 AI Interfaces

#### ILLMProvider

```csharp
public interface ILLMProvider
{
    string Name { get; }
    string Model { get; }
    Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
```

**Contract:**
- Returns LlmResponse even on failure (Success = false)
- Does not throw on LLM errors (returns error in response)
- Preserves token usage information

#### IAgent

```csharp
public interface IAgent
{
    string Name { get; }
    Task<AgentAnalysisResult> AnalyzeAsync(
        AnalysisEvidence evidence,
        CancellationToken cancellationToken = default);
}
```

**Contract:**
- Does not fetch external data (receives evidence)
- Does not calculate indicators (receives calculated results)
- Produces structured output (not arbitrary text)
- Preserves evidence traceability

### 6.4 Orchestration Interface

#### IStrategyOrchestrator

```csharp
public interface IStrategyOrchestrator
{
    Task<StrategyReport> GenerateStrategyAsync(
        Asset asset, CancellationToken cancellationToken = default);
    Task<MarketDataBundle> CollectDataAsync(
        Asset asset, CancellationToken cancellationToken = default);
    Task<AnalysisEvidence> AnalyzeAsync(
        MarketDataBundle dataBundle,
        CancellationToken cancellationToken = default);
}
```

---

## 7. Configuration

### 7.1 LLM Settings

```json
{
  "LlmSettings": {
    "Provider": "OpenAiCompatible",
    "BaseUrl": "http://localhost:11434/v1",
    "Model": "default",
    "ApiKey": "",
    "DefaultMaxTokens": 4096,
    "DefaultTemperature": 0.3,
    "TimeoutSeconds": 120,
    "RetryAttempts": 2
  }
}
```

### 7.2 Database Settings

```json
{
  "DatabaseSettings": {
    "ConnectionString": "Host=localhost;Port=5432;Database=strategyforge;Username=postgres;Password=postgres",
    "AutoMigrate": true,
    "CommandTimeoutSeconds": 30
  }
}
```

### 7.3 Data Source Settings

```json
{
  "DataSourceSettings": {
    "HttpTimeoutSeconds": 30,
    "RetryAttempts": 2,
    "UserAgent": "StrategyForge/1.0",
    "DefaultMaxCandles": 365,
    "CacheDurationMinutes": 15
  }
}
```

---

## 8. Dependency Injection

### Registration Pattern

Each project provides a `ServiceCollectionExtensions` class:

```csharp
// StrategyForge.Analysis
public static IServiceCollection AddStrategyForgeAnalysis(
    this IServiceCollection services)
{
    services.AddSingleton<IIndicatorEngine, IndicatorEngine>();
    // Register indicators...
    return services;
}

// StrategyForge.AI
public static IServiceCollection AddStrategyForgeAI(
    this IServiceCollection services)
{
    services.AddSingleton<ILLMProvider, OpenAiCompatibleLlmProvider>();
    // Register agents...
    return services;
}

// StrategyForge.Infrastructure
public static IServiceCollection AddStrategyForgeInfrastructure(
    this IServiceCollection services)
{
    // Register providers...
    return services;
}

// StrategyForge.Orchestration
public static IServiceCollection AddStrategyForgeOrchestration(
    this IServiceCollection services)
{
    services.AddSingleton<IStrategyOrchestrator, StrategyOrchestrator>();
    return services;
}
```

### Composition Root (Api/Program.cs)

```csharp
builder.Services.AddStrategyForgeInfrastructure();
builder.Services.AddStrategyForgeAnalysis();
builder.Services.AddStrategyForgeAI();
builder.Services.AddStrategyForgeOrchestration();
```

---

## 9. Error Handling

### Provider Errors

Providers throw exceptions on network errors. The orchestrator catches and records them:

```csharp
try
{
    var candles = await provider.GetHistoricalDataAsync(...);
    successfulProviders.Add(provider.Name);
}
catch (Exception ex)
{
    failedProviders.Add(provider.Name);
    errors.Add(new DataCollectionError
    {
        ProviderName = provider.Name,
        ErrorMessage = ex.Message,
        OccurredAt = DateTimeOffset.UtcNow
    });
}
```

### LLM Errors

LLM providers return error responses instead of throwing:

```csharp
public async Task<LlmResponse> CompleteAsync(...)
{
    try
    {
        // Call LLM API
        return new LlmResponse { Success = true, Content = ... };
    }
    catch (Exception ex)
    {
        return new LlmResponse
        {
            Success = false,
            Error = ex.Message
        };
    }
}
```

### Agent Errors

Agents are called in a try-catch within the orchestrator:

```csharp
foreach (var agent in agents)
{
    try
    {
        var result = await agent.AnalyzeAsync(evidence, ct);
        agentResults.Add(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Agent {AgentName} failed", agent.Name);
    }
}
```

---

## 10. Logging

### Strategy

- Use `ILogger<T>` throughout (built-in .NET logging)
- Structured logging with consistent property names
- Log at appropriate levels:
  - `Information`: Milestones (strategy generation started/completed)
  - `Debug`: Detailed pipeline steps
  - `Warning`: Recoverable failures (provider fallback)
  - `Error`: Unrecoverable failures (agent crashes)

### Sensitive Data

- Never log API keys, connection strings, or credentials
- Never log full LLM prompts/responses in production
- Log token counts, not full content

---

## 11. Testing Strategy

### Test Categories

| Category | Purpose | Tools |
|----------|---------|-------|
| Unit Tests | Individual components in isolation | xUnit, Moq |
| Integration Tests | Component interaction with real infra | xUnit, WebApplicationFactory |
| Indicator Tests | Calculations against known data | xUnit with hand-calculated values |

### Testing Principles

1. **Deterministic indicators** must be tested with known inputs/outputs
2. **Provider tests** use recorded responses for unit tests
3. **Agent tests** verify prompt construction and output parsing
4. **Orchestration tests** verify pipeline flow with mocked dependencies
5. **Domain model tests** verify invariants

### Test Naming Convention

```
Method_Scenario_ExpectedResult
```

Example: `Compute_WithOverboughtRsi_ReturnsOverboughtSignal`

---

## 12. Security

### Principles

1. **No hard-coded secrets:** Use configuration/environment variables
2. **No secrets in source code:** User Secrets for dev, env vars for production
3. **No secrets in API responses:** Never expose credentials
4. **Input validation:** Validate all external input
5. **No execution capability:** System cannot place trades

### Implementation

- API keys stored in environment variables
- Connection strings in appsettings.json (not committed)
- User Secrets for local development
- Input validation in controllers
- No brokerage integration

---

## 13. Performance

### Caching Strategy

- Market data cached in PostgreSQL (configurable duration)
- Indicator results cached per asset/date range
- LLM responses not cached (non-deterministic)

### Async/Await

- All I/O operations are async
- CancellationToken passed through the pipeline
- Parallel agent execution where possible

### Resource Management

- HttpClient properly managed via IHttpClientFactory
- Database connections managed via EF Core
- LLM connections managed via provider implementation

---

## 14. Deployment

### Development

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run API
cd src/StrategyForge.Api
dotnet run
```

### Docker (Future)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "StrategyForge.Api.dll"]
```

### Production

- PostgreSQL via Docker or managed service
- FreeLLM running locally or on dedicated server
- API deployed as container or bare metal
- Configuration via environment variables
- Logging to file or central logging system
