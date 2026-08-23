# StrategyForge — Data Models Reference

**Version:** 1.0  
**Last Updated:** August 21, 2026

---

## Table of Contents

1. [Overview](#1-overview)
2. [Core Models](#2-core-models)
3. [Market Data Models](#3-market-data-models)
4. [News Models](#4-news-models)
5. [Fundamental Models](#5-fundamental-models)
6. [Economic Models](#6-economic-models)
7. [Analysis Models](#7-analysis-models)
8. [AI Models](#8-ai-models)
9. [Strategy Models](#9-strategy-models)
10. [Configuration Models](#10-configuration-models)

---

## 1. Overview

StrategyForge uses strongly-typed C# records for all domain models. Models are immutable by default and follow functional programming principles.

### Design Principles

1. **Immutability:** All models are `record` types (immutable by default)
2. **Required Properties:** Use `required` keyword for essential data
3. **Nullable Properties:** Use `?` for optional data
4. **Initialization:** Use `init` setters for property initialization
5. **Defaults:** Provide sensible defaults where appropriate

---

## 2. Core Models

### Asset

Represents a financial instrument that can be analyzed.

```csharp
public sealed record Asset
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Symbol { get; init; }
    public required string Name { get; init; }
    public required string Market { get; init; }
    public required AssetType AssetType { get; init; }
    public string? Sector { get; init; }
    public string? Isin { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
```

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Id` | `Guid` | Auto | Unique identifier |
| `Symbol` | `string` | Yes | Ticker symbol (e.g., "فولاد") |
| `Name` | `string` | Yes | Human-readable name |
| `Market` | `string` | Yes | Exchange/market (e.g., "TSE") |
| `AssetType` | `AssetType` | Yes | Type of financial instrument |
| `Sector` | `string` | No | Sector classification |
| `Isin` | `string` | No | International Securities Identification Number |
| `Metadata` | `Dictionary` | No | Additional key-value pairs |

---

### DataMetadata

Tracks data provenance and freshness.

```csharp
public sealed record DataMetadata
{
    public required string Source { get; init; }
    public required DateTimeOffset RetrievedAt { get; init; }
    public DateTimeOffset? DataTimestamp { get; init; }
    public required DataSourceType DataType { get; init; }
    public string? Reliability { get; init; }
    public IReadOnlyDictionary<string, string>? ExtraProperties { get; init; }
}
```

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Source` | `string` | Yes | Data source name (e.g., "TSETMC") |
| `RetrievedAt` | `DateTimeOffset` | Yes | When data was retrieved |
| `DataTimestamp` | `DateTimeOffset` | No | When data was generated |
| `DataType` | `DataSourceType` | Yes | Category of data |
| `Reliability` | `string` | No | "Verified", "Estimated", "Unverified" |
| `ExtraProperties` | `Dictionary` | No | Additional metadata |

---

## 3. Market Data Models

### Candle

Represents a single OHLCV price bar.

```csharp
public sealed record Candle
{
    public required DateOnly Date { get; init; }
    public required decimal Open { get; init; }
    public required decimal High { get; init; }
    public required decimal Low { get; init; }
    public required decimal Close { get; init; }
    public required long Volume { get; init; }
    public long? TradeCount { get; init; }
    public DataMetadata? Metadata { get; init; }
    
    public bool IsValid =>
        High >= Open && High >= Close && High >= Low &&
        Low <= Open && Low <= Close &&
        Open > 0 && Close > 0;
}
```

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Date` | `DateOnly` | Yes | Date of the candle |
| `Open` | `decimal` | Yes | Opening price |
| `High` | `decimal` | Yes | Highest price |
| `Low` | `decimal` | Yes | Lowest price |
| `Close` | `decimal` | Yes | Closing price |
| `Volume` | `long` | Yes | Trading volume |
| `TradeCount` | `long` | No | Number of trades |
| `Metadata` | `DataMetadata` | No | Data provenance |

**Validation:**
- `IsValid` property checks OHLCV consistency
- High must be >= Open, Close, and Low
- Low must be <= Open, Close, and High
- Open and Close must be > 0

---

### MarketDataBundle

Aggregated data bundle for a specific asset.

```csharp
public sealed record MarketDataBundle
{
    public required Asset Asset { get; init; }
    public required DateTimeOffset CollectedAt { get; init; }
    public IReadOnlyList<Candle> Candles { get; init; } = [];
    public CompanyInfo? CompanyInfo { get; init; }
    public IReadOnlyList<NewsItem> News { get; init; } = [];
    public IReadOnlyList<EconomicIndicator> EconomicIndicators { get; init; } = [];
    public IReadOnlyList<CurrencyRate> CurrencyRates { get; init; } = [];
    public IReadOnlyList<GoldPrice> GoldPrices { get; init; } = [];
    public IReadOnlyList<string> SuccessfulProviders { get; init; } = [];
    public IReadOnlyList<string> FailedProviders { get; init; } = [];
    public IReadOnlyList<DataCollectionError> Errors { get; init; } = [];
    public DateTimeOffset? DataStartTime { get; init; }
    public DateTimeOffset? DataEndTime { get; init; }
}
```

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Asset` | `Asset` | Yes | The asset this data is about |
| `CollectedAt` | `DateTimeOffset` | Yes | When data was collected |
| `Candles` | `IReadOnlyList<Candle>` | No | OHLCV price data |
| `CompanyInfo` | `CompanyInfo` | No | Company fundamentals |
| `News` | `IReadOnlyList<NewsItem>` | No | Related news |
| `EconomicIndicators` | `IReadOnlyList<EconomicIndicator>` | No | Economic data |
| `CurrencyRates` | `IReadOnlyList<CurrencyRate>` | No | Exchange rates |
| `GoldPrices` | `IReadOnlyList<GoldPrice>` | No | Gold prices |
| `SuccessfulProviders` | `IReadOnlyList<string>` | No | Providers that succeeded |
| `FailedProviders` | `IReadOnlyList<string>` | No | Providers that failed |
| `Errors` | `IReadOnlyList<DataCollectionError>` | No | Collection errors |
| `DataStartTime` | `DateTimeOffset` | No | Earliest data point |
| `DataEndTime` | `DateTimeOffset` | No | Latest data point |

---

### DataCollectionError

Records an error during data collection.

```csharp
public sealed record DataCollectionError
{
    public required string ProviderName { get; init; }
    public required string ErrorMessage { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public string? ExceptionMessage { get; init; }
}
```

---

## 4. News Models

### NewsItem

Represents a news item or announcement.

```csharp
public sealed record NewsItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; init; }
    public string? Content { get; init; }
    public required DateTimeOffset PublishedAt { get; init; }
    public required string Source { get; init; }
    public string? Url { get; init; }
    public IReadOnlyList<string>? RelatedSymbols { get; init; }
    public IReadOnlyList<string>? Topics { get; init; }
    public Sentiment? Sentiment { get; init; }
    public decimal? SentimentConfidence { get; init; }
    public decimal? RelevanceScore { get; init; }
    public DataMetadata? Metadata { get; init; }
}
```

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Id` | `Guid` | Auto | Unique identifier |
| `Title` | `string` | Yes | Headline |
| `Content` | `string` | No | Full content or summary |
| `PublishedAt` | `DateTimeOffset` | Yes | Publication timestamp |
| `Source` | `string` | Yes | Publisher name |
| `Url` | `string` | No | Original article URL |
| `RelatedSymbols` | `IReadOnlyList<string>` | No | Related asset symbols |
| `Topics` | `IReadOnlyList<string>` | No | Topic tags |
| `Sentiment` | `Sentiment` | No | Assessed sentiment |
| `SentimentConfidence` | `decimal` | No | Confidence in sentiment (0-1) |
| `RelevanceScore` | `decimal` | No | Relevance to target asset (0-1) |
| `Metadata` | `DataMetadata` | No | Data provenance |

---

## 5. Fundamental Models

### CompanyInfo

Represents company fundamental data.

```csharp
public sealed record CompanyInfo
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Symbol { get; init; }
    public required string CompanyName { get; init; }
    public string? Sector { get; init; }
    public string? Industry { get; init; }
    public DateOnly? EstablishedDate { get; init; }
    public decimal? Eps { get; init; }
    public decimal? Pe { get; init; }
    public decimal? Pb { get; init; }
    public decimal? DividendYield { get; init; }
    public decimal? MarketCap { get; init; }
    public decimal? Revenue { get; init; }
    public decimal? RevenueGrowth { get; init; }
    public decimal? NetProfit { get; init; }
    public decimal? ProfitGrowth { get; init; }
    public decimal? GrossMargin { get; init; }
    public decimal? NetMargin { get; init; }
    public decimal? TotalDebt { get; init; }
    public decimal? Cash { get; init; }
    public DateOnly? FinancialDataDate { get; init; }
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, string>? AdditionalData { get; init; }
    public DataMetadata? Metadata { get; init; }
}
```

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Symbol` | `string` | Yes | Stock symbol |
| `CompanyName` | `string` | Yes | Full company name |
| `Sector` | `string` | No | Sector classification |
| `Industry` | `string` | No | Industry classification |
| `EstablishedDate` | `DateOnly` | No | Company establishment date |
| `Eps` | `decimal` | No | Earnings per share |
| `Pe` | `decimal` | No | Price-to-earnings ratio |
| `Pb` | `decimal` | No | Price-to-book ratio |
| `DividendYield` | `decimal` | No | Dividend yield (%) |
| `MarketCap` | `decimal` | No | Market capitalization |
| `Revenue` | `decimal` | No | Latest revenue |
| `RevenueGrowth` | `decimal` | No | Revenue growth rate |
| `NetProfit` | `decimal` | No | Net profit |
| `ProfitGrowth` | `decimal` | No | Profit growth rate |
| `GrossMargin` | `decimal` | No | Gross margin (%) |
| `NetMargin` | `decimal` | No | Net margin (%) |
| `TotalDebt` | `decimal` | No | Total debt |
| `Cash` | `decimal` | No | Cash and equivalents |
| `FinancialDataDate` | `DateOnly` | No | Date of financial data |
| `Description` | `string` | No | Business description |
| `AdditionalData` | `Dictionary` | No | Additional data points |
| `Metadata` | `DataMetadata` | No | Data provenance |

---

## 6. Economic Models

### EconomicIndicator

Represents an economic indicator data point.

```csharp
public sealed record EconomicIndicator
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public string? Category { get; init; }
    public required decimal Value { get; init; }
    public string? Unit { get; init; }
    public string? Period { get; init; }
    public DateOnly? ReportedDate { get; init; }
    public decimal? PreviousValue { get; init; }
    public decimal? Change { get; init; }
    public string? Region { get; init; }
    public DataMetadata? Metadata { get; init; }
}
```

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | `string` | Yes | Indicator name |
| `Category` | `string` | No | Category (Monetary, Fiscal, etc.) |
| `Value` | `decimal` | Yes | Indicator value |
| `Unit` | `string` | No | Unit of measurement |
| `Period` | `string` | No | Data period |
| `ReportedDate` | `DateOnly` | No | Publication date |
| `PreviousValue` | `decimal` | No | Previous value |
| `Change` | `decimal` | No | Change from previous |
| `Region` | `string` | No | Country/region |
| `Metadata` | `DataMetadata` | No | Data provenance |

---

### CurrencyRate

Represents a currency exchange rate.

```csharp
public sealed record CurrencyRate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string BaseCurrency { get; init; }
    public required string QuoteCurrency { get; init; }
    public required decimal Rate { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public decimal? Bid { get; init; }
    public decimal? Ask { get; init; }
    public decimal? High { get; init; }
    public decimal? Low { get; init; }
    public DataMetadata? Metadata { get; init; }
}
```

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `BaseCurrency` | `string` | Yes | Base currency code (e.g., "USD") |
| `QuoteCurrency` | `string` | Yes | Quote currency code (e.g., "IRR") |
| `Rate` | `decimal` | Yes | Exchange rate |
| `Timestamp` | `DateTimeOffset` | Yes | Rate timestamp |
| `Bid` | `decimal` | No | Bid price |
| `Ask` | `decimal` | No | Ask price |
| `High` | `decimal` | No | Period high |
| `Low` | `decimal` | No | Period low |
| `Metadata` | `DataMetadata` | No | Data provenance |

---

### GoldPrice

Represents a gold price data point.

```csharp
public sealed record GoldPrice
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required decimal Price { get; init; }
    public required string Unit { get; init; }
    public string? GoldType { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public decimal? Change { get; init; }
    public decimal? ChangePercent { get; init; }
    public DataMetadata? Metadata { get; init; }
}
```

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Price` | `decimal` | Yes | Gold price |
| `Unit` | `string` | Yes | Unit (e.g., "USD/oz", "IRR/mithqal") |
| `GoldType` | `string` | No | Type (Spot, 18K, 24K, Coin) |
| `Timestamp` | `DateTimeOffset` | Yes | Price timestamp |
| `Change` | `decimal` | No | Change from previous |
| `ChangePercent` | `decimal` | No | Percentage change |
| `Metadata` | `DataMetadata` | No | Data provenance |

---

## 7. Analysis Models

### IndicatorResult

Represents the output of a single indicator computation.

```csharp
public sealed record IndicatorResult
{
    public required string IndicatorName { get; init; }
    public required DateOnly Date { get; init; }
    public required decimal Value { get; init; }
    public string? Signal { get; init; }
    public IReadOnlyDictionary<string, decimal>? AdditionalValues { get; init; }
    public int? Period { get; init; }
    public IndicatorParameters? Parameters { get; init; }
}
```

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `IndicatorName` | `string` | Yes | Indicator name |
| `Date` | `DateOnly` | Yes | Calculation date |
| `Value` | `decimal` | Yes | Primary value |
| `Signal` | `string` | No | Signal interpretation |
| `AdditionalValues` | `Dictionary` | No | Additional values (e.g., MACD components) |
| `Period` | `int` | No | Period used |
| `Parameters` | `IndicatorParameters` | No | Parameters used |

---

### IndicatorParameters

Configuration for indicator computation.

```csharp
public sealed record IndicatorParameters
{
    public int? Period { get; init; }
    public int? SecondaryPeriod { get; init; }
    public decimal? StandardDeviation { get; init; }
    public string? PriceSource { get; init; }
    public IReadOnlyDictionary<string, decimal>? Custom { get; init; }
    
    public static IndicatorParameters DefaultRsi => new() { Period = 14 };
    public static IndicatorParameters DefaultMacd => new()
    {
        Period = 12,
        SecondaryPeriod = 26,
        Custom = new Dictionary<string, decimal> { ["SignalPeriod"] = 9 }
    };
    public static IndicatorParameters DefaultBollinger => new()
    {
        Period = 20,
        StandardDeviation = 2.0m
    };
    public static IndicatorParameters DefaultAtr => new() { Period = 14 };
}
```

**Properties:**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Period` | `int` | No | Primary period |
| `SecondaryPeriod` | `int` | No | Secondary period |
| `StandardDeviation` | `decimal` | No | Std dev multiplier |
| `PriceSource` | `string` | No | Source price ("Close", "HL2", etc.) |
| `Custom` | `Dictionary` | No | Custom parameters |

---

### IndicatorEngineResult

Aggregated indicator computation results.

```csharp
public sealed record IndicatorEngineResult
{
    public DateOnly DataStartDate { get; init; }
    public DateOnly DataEndDate { get; init; }
    public int CandleCount { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<IndicatorResult>> Results { get; init; }
    public IReadOnlyList<string> SuccessfulIndicators { get; init; } = [];
    public IReadOnlyList<string> FailedIndicators { get; init; } = [];
    public IReadOnlyList<IndicatorError> Errors { get; init; } = [];
    
    public IndicatorResult? GetLatest(string indicatorName);
    public IReadOnlyDictionary<string, IndicatorResult> GetLatestValues();
}
```

---

### AnalysisEvidence

Structured evidence for AI agents.

```csharp
public sealed record AnalysisEvidence
{
    public required Asset Asset { get; init; }
    public required DateTimeOffset AssembledAt { get; init; }
    public DateOnly DataStartDate { get; init; }
    public DateOnly DataEndDate { get; init; }
    public decimal? CurrentPrice { get; init; }
    public decimal? DailyChangePercent { get; init; }
    public long? LatestVolume { get; init; }
    public decimal? AverageVolume { get; init; }
    public decimal? VolumeRatio { get; init; }
    public IReadOnlyDictionary<string, IndicatorResult> IndicatorValues { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<IndicatorResult>> IndicatorHistory { get; init; }
    public MarketRegime? MarketRegime { get; init; }
    public IReadOnlyList<decimal> SupportLevels { get; init; } = [];
    public IReadOnlyList<decimal> ResistanceLevels { get; init; } = [];
    public string? PriceActionSummary { get; init; }
    public CompanyInfo? CompanyInfo { get; init; }
    public IReadOnlyList<EconomicIndicator> EconomicIndicators { get; init; } = [];
    public IReadOnlyList<CurrencyRate> CurrencyRates { get; init; } = [];
    public IReadOnlyList<GoldPrice> GoldPrices { get; init; } = [];
    public IReadOnlyList<NewsItem> RecentNews { get; init; } = [];
    public IReadOnlyList<string> DataSources { get; init; } = [];
    public IReadOnlyList<string> MissingData { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
```

---

## 8. AI Models

### LlmRequest

Request to an LLM provider.

```csharp
public sealed record LlmRequest
{
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public string? ResponseFormat { get; init; }
    public int? MaxTokens { get; init; }
    public double? Temperature { get; init; }
    public string? Model { get; init; }
}
```

---

### LlmResponse

Response from an LLM provider.

```csharp
public sealed record LlmResponse
{
    public required string Content { get; init; }
    public required string Model { get; init; }
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens => PromptTokens + CompletionTokens;
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? FinishReason { get; init; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan? ResponseDuration { get; init; }
}
```

---

### AgentAnalysisResult

Output from a specialist AI agent.

```csharp
public sealed record AgentAnalysisResult
{
    public required string AgentName { get; init; }
    public required string AssetSymbol { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required Sentiment Sentiment { get; init; }
    public required decimal Confidence { get; init; }
    public required string Summary { get; init; }
    public string? DetailedAnalysis { get; init; }
    public IReadOnlyList<EvidenceItem> SupportingEvidence { get; init; } = [];
    public IReadOnlyList<EvidenceItem> ContradictingEvidence { get; init; } = [];
    public IReadOnlyList<PriceLevel> KeyLevels { get; init; } = [];
    public IReadOnlyList<string> IdentifiedRisks { get; init; } = [];
    public IReadOnlyList<string> InformationGaps { get; init; } = [];
    public IReadOnlyDictionary<string, string>? AgentSpecificData { get; init; }
    public int? TokensUsed { get; init; }
    public TimeSpan? LlmDuration { get; init; }
}
```

---

### PriceLevel

A significant price level identified during analysis.

```csharp
public sealed record PriceLevel
{
    public required decimal Price { get; init; }
    public required string Label { get; init; }
    public TimeHorizon? TimeHorizon { get; init; }
    public decimal? Significance { get; init; }
}
```

---

## 9. Strategy Models

### StrategyReport

The final strategy output.

```csharp
public sealed record StrategyReport
{
    public required Asset Asset { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required DateTimeOffset DataAsOf { get; init; }
    public required ExecutiveSummary ExecutiveSummary { get; init; }
    public required MarketContext MarketContext { get; init; }
    public AgentAnalysisResult? TechnicalAnalysis { get; init; }
    public AgentAnalysisResult? FundamentalAnalysis { get; init; }
    public AgentAnalysisResult? MacroAnalysis { get; init; }
    public AgentAnalysisResult? NewsAnalysis { get; init; }
    public AgentAnalysisResult? PoliticalRiskAnalysis { get; init; }
    public AgentAnalysisResult? RiskAnalysis { get; init; }
    public Scenario? BullishScenario { get; init; }
    public Scenario? BaseScenario { get; init; }
    public Scenario? BearishScenario { get; init; }
    public StrategySection? ShortTermStrategy { get; init; }
    public StrategySection? MediumTermStrategy { get; init; }
    public StrategySection? LongTermStrategy { get; init; }
    public RiskRewardAssessment? RiskReward { get; init; }
    public ConfidenceAssessment? Confidence { get; init; }
    public IReadOnlyList<EvidenceItem> SupportingEvidence { get; init; } = [];
    public IReadOnlyList<EvidenceItem> ContradictingEvidence { get; init; } = [];
    public IReadOnlyList<string> MissingInformation { get; init; } = [];
    public IReadOnlyList<string> InvalidationConditions { get; init; } = [];
    public IReadOnlyList<string> MonitoringRecommendations { get; init; } = [];
    public IReadOnlyList<string> ContributingAgents { get; init; } = [];
    public IReadOnlyList<string> DataProvidersUsed { get; init; } = [];
    public string? LlmModel { get; init; }
    public int? TotalTokensUsed { get; init; }
    public TimeSpan? GenerationDuration { get; init; }
}
```

---

### ExecutiveSummary

High-level strategy summary.

```csharp
public sealed record ExecutiveSummary
{
    public required Sentiment OverallSentiment { get; init; }
    public required string Summary { get; init; }
    public string? KeyTakeaway { get; init; }
    public string? CriticalLevel { get; init; }
    public string? Urgency { get; init; }
}
```

---

### MarketContext

Current market conditions.

```csharp
public sealed record MarketContext
{
    public required MarketRegime Regime { get; init; }
    public required string Description { get; init; }
    public decimal? CurrentPrice { get; init; }
    public decimal? RecentPriceChange { get; init; }
    public string? VolumeContext { get; init; }
    public string? MacroContext { get; init; }
    public IReadOnlyList<string> UpcomingEvents { get; init; } = [];
}
```

---

### Scenario

A market scenario (bullish, base, bearish).

```csharp
public sealed record Scenario
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> Assumptions { get; init; } = [];
    public IReadOnlyList<EvidenceItem> SupportingEvidence { get; init; } = [];
    public IReadOnlyList<EvidenceItem> WeakeningEvidence { get; init; } = [];
    public string? ProbabilityAssessment { get; init; }
    public string? ExpectedOutcome { get; init; }
    public IReadOnlyList<string> ConfirmationConditions { get; init; } = [];
    public IReadOnlyList<string> InvalidationConditions { get; init; } = [];
}
```

---

### StrategySection

Strategy for a specific time horizon.

```csharp
public sealed record StrategySection
{
    public required TimeHorizon TimeHorizon { get; init; }
    public string? EntryScenario { get; init; }
    public IReadOnlyList<string> EntryZones { get; init; } = [];
    public IReadOnlyList<string> ConfirmationConditions { get; init; } = [];
    public string? StopInvalidation { get; init; }
    public IReadOnlyList<string> TargetLevels { get; init; } = [];
    public string? ExitConditions { get; init; }
    public string? RiskAssessment { get; init; }
    public IReadOnlyList<string> MonitoringActions { get; init; } = [];
}
```

---

### RiskRewardAssessment

Risk/reward profile.

```csharp
public sealed record RiskRewardAssessment
{
    public string? PotentialUpside { get; init; }
    public string? PotentialDownside { get; init; }
    public string? RiskRewardRatio { get; init; }
    public string? RiskLevel { get; init; }
    public IReadOnlyList<string> KeyRiskFactors { get; init; } = [];
    public IReadOnlyList<string> FavorableFactors { get; init; } = [];
    public IReadOnlyList<string> UnfavorableFactors { get; init; } = [];
}
```

---

### ConfidenceAssessment

Strategy confidence level.

```csharp
public sealed record ConfidenceAssessment
{
    public required decimal OverallConfidence { get; init; }
    public required string Level { get; init; }
    public IReadOnlyList<string> ConfidenceFactors { get; init; } = [];
    public IReadOnlyList<string> UncertaintyFactors { get; init; } = [];
    public IReadOnlyList<string> InformationThatWouldHelp { get; init; } = [];
    public int DataSourcesUsed { get; init; }
    public int AgentsContributed { get; init; }
}
```

---

### EvidenceItem

A single evidence item.

```csharp
public sealed record EvidenceItem
{
    public required string Content { get; init; }
    public required EvidenceType Type { get; init; }
    public required string Source { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public decimal Confidence { get; init; }
    public TimeHorizon? RelevantHorizon { get; init; }
}
```

---

## 10. Configuration Models

### LlmSettings

LLM provider configuration.

```csharp
public sealed record LlmSettings
{
    public const string SectionName = "LlmSettings";
    public string Provider { get; init; } = "OpenAiCompatible";
    public string BaseUrl { get; init; } = "http://localhost:3000/v1";
    public string Model { get; init; } = "default";
    public string ApiKey { get; init; } = string.Empty;
    public int DefaultMaxTokens { get; init; } = 4096;
    public double DefaultTemperature { get; init; } = 0.3;
    public int TimeoutSeconds { get; init; } = 120;
    public int RetryAttempts { get; init; } = 2;
}
```

---

### DatabaseSettings

Database configuration.

```csharp
public sealed record DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";
    public string ConnectionString { get; init; }
        = "Host=localhost;Port=5432;Database=strategyforge;Username=postgres;Password=postgres";
    public bool AutoMigrate { get; init; } = true;
    public int CommandTimeoutSeconds { get; init; } = 30;
}
```

---

### DataSourceSettings

Data source configuration.

```csharp
public sealed record DataSourceSettings
{
    public const string SectionName = "DataSourceSettings";
    public int HttpTimeoutSeconds { get; init; } = 30;
    public int RetryAttempts { get; init; } = 2;
    public string UserAgent { get; init; } = "StrategyForge/1.0";
    public int DefaultMaxCandles { get; init; } = 365;
    public int CacheDurationMinutes { get; init; } = 15;
}
```
