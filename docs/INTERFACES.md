# StrategyForge — Interface Contracts

**Version:** 1.0  
**Last Updated:** August 21, 2026

---

## Table of Contents

1. [Overview](#1-overview)
2. [Provider Interfaces](#2-provider-interfaces)
3. [Analysis Interfaces](#3-analysis-interfaces)
4. [AI Interfaces](#4-ai-interfaces)
5. [Orchestration Interface](#5-orchestration-interface)
6. [Implementation Guidelines](#6-implementation-guidelines)

---

## 1. Overview

StrategyForge uses interface-based design to ensure:
- **Loose coupling** between components
- **Testability** via mock implementations
- **Extensibility** without modifying existing code
- **Replaceability** of any component

All interfaces are defined in `StrategyForge.Domain.Interfaces`.

---

## 2. Provider Interfaces

### IMarketDataProvider

Fetches OHLCV candle data for financial assets.

```csharp
public interface IMarketDataProvider
{
    string Name { get; }
    
    Task<IReadOnlyList<Candle>> GetHistoricalDataAsync(
        Asset asset,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
    
    Task<Candle?> GetLatestCandleAsync(
        Asset asset,
        CancellationToken cancellationToken = default);
    
    bool Supports(Asset asset);
}
```

**Contract:**

| Method | Returns | Behavior |
|--------|---------|----------|
| `Name` | `string` | Provider identifier (e.g., "TSETMC") |
| `GetHistoricalDataAsync` | `IReadOnlyList<Candle>` | Candles ordered oldest to newest; empty list if no data |
| `GetLatestCandleAsync` | `Candle?` | Most recent candle; null if unavailable |
| `Supports` | `bool` | Whether this provider can serve the asset |

**Implementation Rules:**
- Throw on network errors (caller handles via try-catch)
- Do not return null for historical data (return empty list)
- Preserve metadata in each candle
- Validate candle consistency before returning

---

### INewsProvider

Fetches news items and announcements.

```csharp
public interface INewsProvider
{
    string Name { get; }
    
    Task<IReadOnlyList<NewsItem>> GetRecentNewsAsync(
        Asset asset,
        int maxItems = 20,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<NewsItem>> GetMarketNewsAsync(
        IReadOnlyList<string> topics,
        int maxItems = 20,
        CancellationToken cancellationToken = default);
    
    bool Supports(Asset asset);
}
```

**Contract:**

| Method | Returns | Behavior |
|--------|---------|----------|
| `GetRecentNewsAsync` | `IReadOnlyList<NewsItem>` | News ordered most recent first |
| `GetMarketNewsAsync` | `IReadOnlyList<NewsItem>` | News matching topics |
| `Supports` | `bool` | Whether this provider serves the asset |

**Implementation Rules:**
- Preserve source attribution
- Preserve publication timestamp
- Return empty list if no news available

---

### IEconomicDataProvider

Fetches macroeconomic indicator data.

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

**Contract:**

| Method | Returns | Behavior |
|--------|---------|----------|
| `GetIndicatorsAsync` | `IReadOnlyList<EconomicIndicator>` | All available indicators |
| `GetIndicatorAsync` | `EconomicIndicator?` | Specific indicator; null if unavailable |

---

### ICompanyDataProvider

Fetches company fundamental data.

```csharp
public interface ICompanyDataProvider
{
    string Name { get; }
    
    Task<CompanyInfo?> GetCompanyInfoAsync(
        Asset asset,
        CancellationToken cancellationToken = default);
    
    bool Supports(Asset asset);
}
```

**Contract:**

| Method | Returns | Behavior |
|--------|---------|----------|
| `GetCompanyInfoAsync` | `CompanyInfo?` | Company info; null if unavailable |
| `Supports` | `bool` | Whether this provider has data for the asset |

---

### ICurrencyProvider

Fetches currency exchange rates.

```csharp
public interface ICurrencyProvider
{
    string Name { get; }
    
    Task<CurrencyRate?> GetRateAsync(
        string baseCurrency,
        string quoteCurrency,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<CurrencyRate>> GetHistoricalRatesAsync(
        string baseCurrency,
        string quoteCurrency,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
```

**Contract:**

| Method | Returns | Behavior |
|--------|---------|----------|
| `GetRateAsync` | `CurrencyRate?` | Current rate; null if unavailable |
| `GetHistoricalRatesAsync` | `IReadOnlyList<CurrencyRate>` | Rates ordered oldest to newest |

---

### IGoldPriceProvider

Fetches gold price data.

```csharp
public interface IGoldPriceProvider
{
    string Name { get; }
    
    Task<GoldPrice?> GetCurrentPriceAsync(
        string? unit = null,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<GoldPrice>> GetHistoricalPricesAsync(
        DateOnly from,
        DateOnly to,
        string? unit = null,
        CancellationToken cancellationToken = default);
}
```

**Contract:**

| Method | Returns | Behavior |
|--------|---------|----------|
| `GetCurrentPriceAsync` | `GoldPrice?` | Current price; null if unavailable |
| `GetHistoricalPricesAsync` | `IReadOnlyList<GoldPrice>` | Prices ordered oldest to newest |

---

### IAssetRepository

Manages asset storage and retrieval.

```csharp
public interface IAssetRepository
{
    Task<IReadOnlyList<Asset>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Asset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Asset?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);
    Task<Asset> AddAsync(Asset asset, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}
```

---

## 3. Analysis Interfaces

### IIndicator

Deterministic technical indicator.

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

| Property/Method | Returns | Behavior |
|-----------------|---------|----------|
| `Name` | `string` | Indicator identifier (e.g., "RSI") |
| `Description` | `string` | Human-readable description |
| `Compute` | `IReadOnlyList<IndicatorResult>` | Results for each computable date |

**Implementation Rules:**
- Pure function (no side effects)
- Deterministic (same input → same output)
- Return empty list if insufficient data (do not throw)
- Do not access external resources
- Preserve indicator name in results

---

### IIndicatorEngine

Orchestrates indicator computation.

```csharp
public interface IIndicatorEngine
{
    IReadOnlyList<IIndicator> RegisteredIndicators { get; }
    
    IndicatorEngineResult ComputeAll(
        IReadOnlyList<Candle> candles,
        IndicatorConfiguration? configuration = null);
}
```

**Contract:**

| Property/Method | Returns | Behavior |
|-----------------|---------|----------|
| `RegisteredIndicators` | `IReadOnlyList<IIndicator>` | All available indicators |
| `ComputeAll` | `IndicatorEngineResult` | Aggregated results from all enabled indicators |

---

### IAnalyzer

Higher-level analysis module.

```csharp
public interface IAnalyzer
{
    string Name { get; }
    
    Task<AnalyzerResult> AnalyzeAsync(
        MarketDataBundle dataBundle,
        IndicatorEngineResult indicatorResults,
        CancellationToken cancellationToken = default);
}
```

**Contract:**

| Method | Returns | Behavior |
|--------|---------|----------|
| `AnalyzeAsync` | `AnalyzerResult` | Structured findings from analysis |

---

## 4. AI Interfaces

### ILLMProvider

LLM provider abstraction.

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

| Method | Returns | Behavior |
|--------|---------|----------|
| `CompleteAsync` | `LlmResponse` | LLM response (Success=false on error) |
| `IsAvailableAsync` | `bool` | Whether provider is reachable |

**Implementation Rules:**
- Return error in LlmResponse (do not throw on LLM errors)
- Preserve token usage information
- Handle timeouts gracefully
- Support cancellation

---

### IAgent

Specialist AI agent.

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

| Method | Returns | Behavior |
|--------|---------|----------|
| `AnalyzeAsync` | `AgentAnalysisResult` | Structured analysis from this agent |

**Implementation Rules:**
- Do not fetch external data (receive evidence)
- Do not calculate indicators (receive calculated results)
- Produce structured output (not arbitrary text)
- Preserve evidence traceability
- Handle LLM errors gracefully

---

## 5. Orchestration Interface

### IStrategyOrchestrator

Coordinates the full analysis pipeline.

```csharp
public interface IStrategyOrchestrator
{
    Task<StrategyReport> GenerateStrategyAsync(
        Asset asset,
        CancellationToken cancellationToken = default);
    
    Task<MarketDataBundle> CollectDataAsync(
        Asset asset,
        CancellationToken cancellationToken = default);
    
    Task<AnalysisEvidence> AnalyzeAsync(
        MarketDataBundle dataBundle,
        CancellationToken cancellationToken = default);
}
```

**Contract:**

| Method | Returns | Behavior |
|--------|---------|----------|
| `GenerateStrategyAsync` | `StrategyReport` | Complete strategy for the asset |
| `CollectDataAsync` | `MarketDataBundle` | Raw data collection |
| `AnalyzeAsync` | `AnalysisEvidence` | Indicator analysis on data |

---

## 6. Implementation Guidelines

### Adding a New Provider

1. Create class implementing the provider interface
2. Implement all interface members
3. Register in `ServiceCollectionExtensions`
4. Add unit tests with recorded responses
5. Add integration tests against real API (optional)

### Adding a New Indicator

1. Create class implementing `IIndicator`
2. Implement `Name`, `Description`, and `Compute`
3. Register in `ServiceCollectionExtensions`
4. Add unit tests with hand-calculated expected values
5. Verify indicator produces correct results

### Adding a New Agent

1. Create class implementing `IAgent`
2. Design prompt template
3. Implement `AnalyzeAsync` with evidence → prompt → LLM → parse
4. Register in `ServiceCollectionExtensions`
5. Add unit tests with mock LLM responses

### Adding a New Analyzer

1. Create class implementing `IAnalyzer`
2. Implement `AnalyzeAsync` with data + indicators → findings
3. Register in `ServiceCollectionExtensions`
4. Add unit tests with known data

### Error Handling

- **Providers:** Throw on network errors (caller catches)
- **LLM:** Return error in LlmResponse (do not throw)
- **Agents:** Handle LLM errors gracefully, log failures
- **Orchestrator:** Catch and record all errors, continue pipeline

### Testing

- **Providers:** Mock HTTP responses for unit tests
- **Indicators:** Test with known inputs/outputs
- **Agents:** Mock LLM responses, test prompt construction
- **Orchestrator:** Mock all dependencies, test pipeline flow
