# StrategyForge — Complete Vision Document

**Version:** 1.0  
**Last Updated:** August 21, 2026

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Core Principles](#2-core-principles)
3. [What StrategyForge Is](#3-what-strategyforge-is)
4. [What StrategyForge Is Not](#4-what-strategyforge-is-not)
5. [Target Market](#5-target-market)
6. [System Overview](#6-system-overview)
7. [Architecture Vision](#7-architecture-vision)
8. [Data Layer Vision](#8-data-layer-vision)
9. [Analysis Layer Vision](#9-analysis-layer-vision)
10. [AI Strategy Layer Vision](#10-ai-strategy-layer-vision)
11. [Strategy Output Vision](#11-strategy-output-vision)
12. [Free-First Philosophy](#12-free-first-philosophy)
13. [Version Roadmap](#13-version-roadmap)
14. [Version 1 Scope](#14-version-1-scope)
15. [Version 2 Scope](#15-version-2-scope)
16. [Version 3 Scope](#16-version-3-scope)
17. [Long-Term Vision](#17-long-term-vision)
18. [Success Criteria](#18-success-criteria)
19. [Risks and Mitigations](#19-risks-and-mitigations)
20. [Appendix](#20-appendix)

---

## 1. Executive Summary

**StrategyForge** is an AI-assisted financial market analysis platform that helps human investors understand assets and construct investment strategies.

The system operates on a fundamental principle:

> **Evidence → Analysis → Reasoning → Strategy → Human Decision**

StrategyForge collects legally accessible market, economic, political, and financial information. It transforms this raw information into structured evidence through deterministic analysis. Specialized AI agents then reason over the evidence to produce a coherent investment strategy.

The final result is presented to a human investor who makes the actual trading decision manually and outside the system.

**StrategyForge is decision-support software, not a trading system.**

---

## 2. Core Principles

### Evidence-Driven

Every conclusion must be traceable to available evidence. The system never fabricates data or presents speculation as fact.

### Deterministic Analysis First

Technical indicators and quantitative calculations are performed by deterministic software, not by AI. The AI receives calculated results rather than raw data.

### Structured Reasoning

AI agents receive structured evidence and produce structured output. No arbitrary text generation without clear inputs and outputs.

### No False Certainty

Financial markets are uncertain. The system explicitly distinguishes between:
- Facts
- Calculations
- Interpretations
- Scenarios
- Predictions

### Human Control

The human remains in full control. The system produces analysis and strategy. The human decides whether to act.

### Free-First

The system should function with free/open-source infrastructure. Paid services are optional add-ons, not foundations.

### Modular and Extensible

Adding new capabilities should require isolated, additive changes. No rewriting the entire system.

---

## 3. What StrategyForge Is

### Decision-Support Software

StrategyForge helps investors understand:
- What is happening in the market?
- What evidence supports different interpretations?
- What are the bullish, bearish, and base scenarios?
- What are the key price levels?
- What could invalidate the thesis?
- What is the risk/reward profile?

### Evidence Processor

The system collects and structures evidence from multiple sources:
- Market data (prices, volume, technical indicators)
- Company fundamentals (financials, valuation)
- Economic indicators (inflation, interest rates, currency)
- News and events
- Political and geopolitical context

### Strategy Generator

The system produces structured strategies covering:
- Short-term tactics (days to weeks)
- Medium-term positioning (weeks to months)
- Long-term thesis (months to years)

### Learning Platform

Over time, StrategyForge can help investors:
- Understand market dynamics
- Learn technical analysis concepts
- See how different factors interact
- Develop their own analytical framework

---

## 4. What StrategyForge Is Not

### NOT an Automated Trading Bot

StrategyForge does NOT:
- Automatically buy assets
- Automatically sell assets
- Manage a brokerage account
- Execute orders
- Place trades
- Connect to any trading platform

### NOT a Simple Signal Generator

StrategyForge does NOT produce simplistic "BUY" or "SELL" signals. It produces comprehensive strategies with:
- Multiple scenarios
- Entry and exit conditions
- Risk assessments
- Confidence levels
- Evidence traceability

### NOT a Prediction Engine

StrategyForge does NOT:
- Predict future prices with certainty
- Claim guaranteed outcomes
- Present scenarios as inevitable
- Ignore uncertainty

### NOT a Chatbot

StrategyForge is NOT primarily a conversational AI. It is an evidence-processing system where AI is one component in a larger pipeline.

### NOT a Black Box

StrategyForge maintains complete traceability:
- Every data source is recorded
- Every calculation is deterministic
- Every AI conclusion is linked to evidence
- Missing information is explicitly noted

---

## 5. Target Market

### Initial Focus: Iranian Financial Markets

StrategyForge initially targets the Iranian financial market ecosystem:

| Asset Class | Examples |
|-------------|----------|
| **Iranian Stocks** | Individual company shares on TSE |
| **Market Indices** | TEDPIX, TSE Index |
| **Gold** | Gold coins, gold bullion |
| **USD/IRR** | US Dollar to Iranian Rial |
| **USDT/IRR** | Tether to Iranian Rial |
| **Other Currencies** | EUR, GBP, AED |
| **Commodities** | Relevant commodities |

### Market Characteristics

The Iranian market has unique characteristics:
- Multiple exchange rates (official, free market)
- Significant political influence on markets
- Sanctions affecting international trade
- Currency volatility
- Unique trading mechanisms
- Limited standardized data sources

StrategyForge is designed to handle these complexities while remaining extensible to other markets.

### Future Markets

The architecture supports expansion to:
- Other Middle Eastern markets
- Emerging markets
- Developed markets
- Cryptocurrency markets
- Commodity markets
- Forex markets

---

## 6. System Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        API LAYER                                │
│                   (ASP.NET Core Web API)                        │
│              HTTP endpoints, request/response                   │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                   ORCHESTRATION LAYER                            │
│              (StrategyOrchestrator)                              │
│         Coordinates the full analysis pipeline                  │
└───┬─────────────────────┬──────────────────────┬────────────────┘
    │                     │                      │
    ▼                     ▼                      ▼
┌──────────┐    ┌─────────────────┐    ┌───────────────────┐
│  DATA    │    │   ANALYSIS      │    │   AI STRATEGY     │
│  LAYER   │───▶│   LAYER         │───▶│   LAYER           │
│          │    │                 │    │                   │
│ Collects │    │ Deterministic   │    │ Specialist agents │
│ and      │    │ calculations    │    │ reason over       │
│ normalizes│   │ and quantitative│    │ structured        │
│ external │    │ analysis        │    │ evidence          │
│ data     │    │                 │    │                   │
└──────────┘    └─────────────────┘    └────────┬──────────┘
                                                 │
                                                 ▼
                                        ┌─────────────────┐
                                        │  STRATEGY AGENT  │
                                        │  Synthesizes all │
                                        │  specialist      │
                                        │  outputs into    │
                                        │  coherent report │
                                        └────────┬────────┘
                                                 │
                                                 ▼
                                        ┌─────────────────┐
                                        │ StrategyReport   │
                                        │ → Human User     │
                                        └─────────────────┘
```

### Data Flow

```
1. USER REQUEST
   "Analyze asset X and generate a strategy"
       │
       ▼
2. ORCHESTRATOR resolves asset identity
       │
       ▼
3. DATA LAYER collects all available information
   - Market data (OHLCV candles, volume)
   - Company information (if applicable)
   - Economic data (inflation, rates)
   - Currency data (USD/IRR, USDT/IRR)
   - Gold prices
   - News (if provider available)
   - Political/macro context (if available)
       │
       ▼
   Output: MarketDataBundle
       │
       ▼
4. ANALYSIS LAYER processes the data bundle
   - Technical indicators (RSI, MACD, MA, etc.)
   - Trend detection
   - Volatility metrics
   - Support/resistance estimation
   - Fundamental metrics (if data available)
       │
       ▼
   Output: AnalysisEvidence
       │
       ▼
5. SPECIALIZED AI AGENTS analyze evidence independently
   - TechnicalAnalystAgent → TechnicalAnalysisResult
   - FundamentalAnalystAgent → FundamentalAnalysisResult
   - MacroAnalystAgent → MacroAnalysisResult
   - NewsAnalystAgent → NewsAnalysisResult
   - PoliticalRiskAnalystAgent → PoliticalRiskAnalysisResult
   - RiskAnalystAgent → RiskAnalysisResult
       │
       ▼
   Output: AgentAnalysisResult[]
       │
       ▼
6. STRATEGY AGENT synthesizes all specialist outputs
   - Reasons about agreements and conflicts
   - Constructs scenarios (bullish, base, bearish)
   - Identifies entry/exit zones, invalidation levels
   - Explicitly marks uncertainty
       │
       ▼
   Output: StrategyReport
       │
       ▼
7. HUMAN USER reviews the StrategyReport
   - Makes their own decision
   - Executes trades manually outside the system
```

---

## 7. Architecture Vision

### Clean Architecture Principles

1. **Domain Independence:** The Domain project has zero external dependencies
2. **Dependency Inversion:** High-level modules depend on abstractions, not implementations
3. **Single Responsibility:** Each project has a clear, focused responsibility
4. **Open/Closed:** Open for extension, closed for modification
5. **Interface Segregation:** Small, focused interfaces

### Layer Responsibilities

| Layer | Project | Responsibility |
|-------|---------|---------------|
| **Domain** | `StrategyForge.Domain` | Core models, enums, interfaces, contracts |
| **Infrastructure** | `StrategyForge.Infrastructure` | External integrations (providers, DB, LLM) |
| **Analysis** | `StrategyForge.Analysis` | Deterministic calculations and indicators |
| **AI** | `StrategyForge.AI` | LLM-powered agents and prompts |
| **Orchestration** | `StrategyForge.Orchestration` | Pipeline coordination |
| **API** | `StrategyForge.Api` | HTTP endpoints and web interface |

### Dependency Rules

```
Domain → (nothing)
Analysis → Domain
AI → Domain
Infrastructure → Domain
Orchestration → Domain + Analysis + AI + Infrastructure
Api → All projects (composition root)
```

**Forbidden:**
- Domain referencing any other project
- Analysis referencing Infrastructure, AI, or Orchestration
- AI referencing Infrastructure, Analysis, or Orchestration
- Circular dependencies

---

## 8. Data Layer Vision

### Provider Abstraction Pattern

Every external data source is represented by an interface:

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

### Provider Categories

| Interface | Purpose | Examples |
|-----------|---------|----------|
| `IMarketDataProvider` | OHLCV price data | TSETMC, Yahoo Finance |
| `INewsProvider` | News and announcements | IRNA, financial news sites |
| `IEconomicDataProvider` | Economic indicators | Central Bank data |
| `ICompanyDataProvider` | Company fundamentals | TSETMC company data |
| `ICurrencyProvider` | Exchange rates | Official/free market rates |
| `IGoldPriceProvider` | Gold prices | Gold coin/bullion prices |
| `IAssetRepository` | Asset management | In-memory, PostgreSQL |

### Data Provenance

Every piece of data carries metadata:

```csharp
public sealed record DataMetadata
{
    public string Source { get; init; }        // "TSETMC"
    public DateTimeOffset RetrievedAt { get; init; }
    public DateTimeOffset? DataTimestamp { get; init; }
    public DataSourceType DataType { get; init; }
    public string? Reliability { get; init; }  // "Verified", "Estimated"
}
```

### Provider Fallback Strategy

```
Provider A (primary) → Success? → Use data
    ↓ Failure
Provider B (secondary) → Success? → Use data
    ↓ Failure
Provider C (tertiary) → Success? → Use data
    ↓ Failure
Record error, continue with available data
```

### Iranian Market Data Strategy

**Initial Approach:** TSETMC (Tehran Securities Exchange Technology Management Co.)

TSETMC exposes publicly accessible HTTP endpoints that return market data in JSON format. These are unofficial but widely used.

**Implementation:**
- `TsetmcMarketDataProvider` implements `IMarketDataProvider`
- HTTP calls to TSETMC public endpoints
- Response parsing and normalization
- Error handling and retry logic

**Fallback (Future):**
- Multiple providers registered for the same interface
- Automatic fallback on failure
- Provider health monitoring

---

## 9. Analysis Layer Vision

### Indicator Architecture

#### Interface

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

#### Indicator Registry

The `IndicatorEngine` maintains a registry of all available indicators:

```csharp
public interface IIndicatorEngine
{
    IReadOnlyList<IIndicator> RegisteredIndicators { get; }
    IndicatorEngineResult ComputeAll(
        IReadOnlyList<Candle> candles,
        IndicatorConfiguration? configuration = null);
}
```

### Initial Indicators (V1)

| Indicator | Description | Output |
|-----------|-------------|--------|
| **RSI** | Relative Strength Index | Value (0-100), Signal (Overbought/Oversold) |
| **MACD** | Moving Average Convergence Divergence | MACD, Signal, Histogram, Crossover |
| **SMA** | Simple Moving Average | Value (configurable periods) |
| **EMA** | Exponential Moving Average | Value (configurable periods) |
| **Bollinger Bands** | Bollinger Bands | Upper, Middle, Lower, %B |
| **ATR** | Average True Range | Value, Volatility classification |

### Future Indicators (V2+)

| Indicator | Description |
|-----------|-------------|
| **CCI** | Commodity Channel Index |
| **ADX** | Average Directional Index |
| **Stochastic** | Stochastic Oscillator |
| **Ichimoku** | Ichimoku Cloud |
| **VWAP** | Volume Weighted Average Price |
| **OBV** | On-Balance Volume |
| **MFI** | Money Flow Index |
| **Williams %R** | Williams Percent Range |
| **Parabolic SAR** | Parabolic Stop and Reverse |
| **Keltner Channels** | Keltner Channels |

### Extensibility

Adding a new indicator requires:
1. Create a new class implementing `IIndicator`
2. Register it in the DI container
3. No changes to the indicator engine, analysis layer, or any other indicator

**This is an isolated, additive change.**

### Analyzer Modules

Higher-level analysis modules coordinate multiple indicators:

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

**Potential Analyzers:**
- `TrendDetector` - Identifies market regime
- `SupportResistanceAnalyzer` - Finds key price levels
- `VolumeAnalyzer` - Analyzes volume patterns
- `VolatilityAnalyzer` - Assesses volatility regime
- `PatternRecognizer` - Identifies chart patterns

---

## 10. AI Strategy Layer Vision

### LLM Abstraction

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

**Implementation:** `OpenAiCompatibleLlmProvider`
- Connects to any OpenAI-compatible API
- Configurable base URL and model
- Works with FreeLLM, Ollama, LM Studio, etc.

### Agent Interface

```csharp
public interface IAgent
{
    string Name { get; }
    Task<AgentAnalysisResult> AnalyzeAsync(
        AnalysisEvidence evidence,
        CancellationToken cancellationToken = default);
}
```

### Specialist Agents

#### Technical Analyst Agent

**Inputs:** Price data, indicator results, trend data, volume analysis

**Analyzes:**
- Price action and patterns
- Trend direction and strength
- Momentum indicators
- Volume analysis
- Support/resistance levels
- Volatility assessment
- Moving average relationships
- Entry/exit zones
- Technical invalidation levels

**Output:** `TechnicalAnalysisResult`

---

#### Fundamental Analyst Agent

**Inputs:** Company financials, valuation metrics, sector data

**Analyzes:**
- Revenue and profit trends
- Earnings per share
- Price-to-earnings, price-to-book
- Margins and profitability
- Financial health
- Dividend history
- Sector comparison
- Company growth trajectory

**Output:** `FundamentalAnalysisResult`

**Note:** May produce "insufficient data" if company fundamentals are unavailable.

---

#### Macro/Economic Analyst Agent

**Inputs:** Economic indicators, inflation data, interest rates, currency data

**Analyzes:**
- Inflation trends
- Interest rate environment
- Currency stability (IRR vs USD/USDT)
- Monetary policy direction
- Economic growth indicators
- Macroeconomic risks
- Liquidity conditions

**Output:** `MacroAnalysisResult`

---

#### News Analyst Agent

**Inputs:** Recent news items, company announcements, market events

**Analyzes:**
- Relevant news topics
- News sentiment (positive/negative/neutral)
- Market relevance of events
- Company-specific developments
- Sector news
- Information freshness and reliability

**Output:** `NewsAnalysisResult`

**Principle:** News items must retain source attribution. Unverified claims must be marked as such.

---

#### Political/Risk Analyst Agent

**Inputs:** Political developments, sanctions information, geopolitical context

**Analyzes:**
- Sanctions status and changes
- Political stability
- Geopolitical tensions affecting markets
- Regulatory changes
- Government economic policy
- International relations affecting Iranian markets

**Output:** `PoliticalRiskAnalysisResult`

---

#### Risk Analyst Agent

**Inputs:** All available evidence, volatility data, trend context

**Analyzes:**
- Downside scenarios
- Volatility and drawdown risk
- Invalidating conditions for each scenario
- Risk/reward assessment
- Major uncertainties
- Liquidity risk
- Correlation risks

**Output:** `RiskAnalysisResult`

**Principle:** This agent must be conservative and explicitly identify uncertainty.

---

#### Strategy Agent (Synthesis)

**Inputs:** All specialist AgentAnalysisResult outputs + raw AnalysisEvidence

**Responsibilities:**
- Receive outputs from all specialist agents
- Identify areas of agreement and disagreement
- Reason about conflicts
- Construct coherent scenarios (bullish, base, bearish)
- Determine entry/exit zones with conditions
- Identify invalidation levels
- Assess overall confidence
- Mark missing information
- Produce the final StrategyReport

**Critical Rules:**
- Must NOT simply vote between agents
- Must explicitly identify and explain conflicts
- Must distinguish facts from interpretations from scenarios
- Must not invent evidence not present in specialist outputs
- Must acknowledge uncertainty explicitly

### Agent Extensibility

Adding a new agent requires:
1. Create a new class implementing `IAgent`
2. Create a prompt template
3. Register in DI
4. No changes to orchestrator or other agents

---

## 11. Strategy Output Vision

### StrategyReport Structure

```csharp
public sealed record StrategyReport
{
    // Metadata
    public Asset Asset { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public DateTimeOffset DataAsOf { get; init; }

    // High-level
    public ExecutiveSummary ExecutiveSummary { get; init; }
    public MarketContext MarketContext { get; init; }

    // Specialist analyses
    public AgentAnalysisResult? TechnicalAnalysis { get; init; }
    public AgentAnalysisResult? FundamentalAnalysis { get; init; }
    public AgentAnalysisResult? MacroAnalysis { get; init; }
    public AgentAnalysisResult? NewsAnalysis { get; init; }
    public AgentAnalysisResult? PoliticalRiskAnalysis { get; init; }
    public AgentAnalysisResult? RiskAnalysis { get; init; }

    // Scenarios
    public Scenario? BullishScenario { get; init; }
    public Scenario? BaseScenario { get; init; }
    public Scenario? BearishScenario { get; init; }

    // Time-horizon strategies
    public StrategySection? ShortTermStrategy { get; init; }
    public StrategySection? MediumTermStrategy { get; init; }
    public StrategySection? LongTermStrategy { get; init; }

    // Risk and confidence
    public RiskRewardAssessment? RiskReward { get; init; }
    public ConfidenceAssessment? Confidence { get; init; }

    // Evidence traceability
    public IReadOnlyList<EvidenceItem> SupportingEvidence { get; init; }
    public IReadOnlyList<EvidenceItem> ContradictingEvidence { get; init; }
    public IReadOnlyList<string> MissingInformation { get; init; }
    public IReadOnlyList<string> InvalidationConditions { get; init; }
    public IReadOnlyList<string> MonitoringRecommendations { get; init; }
}
```

### Scenario Structure

```csharp
public sealed record Scenario
{
    public string Name { get; init; }          // "Bullish", "Base", "Bearish"
    public string Description { get; init; }
    public IReadOnlyList<string> Assumptions { get; init; }
    public IReadOnlyList<EvidenceItem> SupportingEvidence { get; init; }
    public IReadOnlyList<EvidenceItem> WeakeningEvidence { get; init; }
    public string? ProbabilityAssessment { get; init; }  // Qualitative
    public string? ExpectedOutcome { get; init; }
    public IReadOnlyList<string> ConfirmationConditions { get; init; }
    public IReadOnlyList<string> InvalidationConditions { get; init; }
}
```

### Strategy Section Structure

```csharp
public sealed record StrategySection
{
    public TimeHorizon TimeHorizon { get; init; }
    public string? EntryScenario { get; init; }
    public IReadOnlyList<string> EntryZones { get; init; }
    public IReadOnlyList<string> ConfirmationConditions { get; init; }
    public string? StopInvalidation { get; init; }
    public IReadOnlyList<string> TargetLevels { get; init; }
    public string? ExitConditions { get; init; }
    public string? RiskAssessment { get; init; }
    public IReadOnlyList<string> MonitoringActions { get; init; }
}
```

### Evidence Classification

```csharp
public enum EvidenceType
{
    Fact,           // Verified data point
    Calculation,    // Deterministic calculation
    Interpretation, // AI reasoning
    Scenario,       // Hypothetical construction
    Uncertain       // Insufficient data
}
```

### Critical Rules

1. **Every conclusion must be traceable to evidence**
2. **Facts must be distinguished from interpretations**
3. **Scenarios must be labeled as scenarios, not predictions**
4. **Missing information must be explicitly listed**
5. **Confidence must be qualified, never presented as certainty**
6. **AI must never invent prices, numbers, or data points**

---

## 12. Free-First Philosophy

### Principles

1. **Free APIs preferred:** Start with publicly accessible, free data sources
2. **Open-source libraries:** Use established .NET open-source packages
3. **Local infrastructure:** PostgreSQL runs locally; LLM runs locally
4. **No paid dependencies in core:** Paid services are optional add-ons
5. **Replaceable providers:** Any provider can be swapped without redesign

### V1 Free Stack

| Component | Solution | Cost |
|-----------|----------|------|
| Framework | .NET 10.0 / ASP.NET Core | Free |
| Database | PostgreSQL (Docker) | Free |
| LLM | FreeLLM (local, OpenAI-compatible) | Free |
| Market Data | TSETMC public endpoints | Free |
| Indicators | Custom implementation | Free |
| Testing | xUnit + Moq | Free |

### When Paid Services Are Acceptable

- A free-tier API that requires registration is acceptable
- A paid API may be added as an **optional** provider
- The system must remain functional without any paid service
- Provider abstraction makes paid alternatives pluggable

---

## 13. Version Roadmap

### Version 1: Foundation

**Timeline:** Weeks 1-6  
**Goal:** Establish the complete foundation and prove the architecture works

**Phase 0:** Architecture confirmation ✅
**Phase 1:** Solution foundation ✅
**Phase 2:** Market data provider (TSETMC)
**Phase 3:** Indicator engine (RSI, MACD, SMA, EMA, Bollinger, ATR)
**Phase 4:** LLM integration + Technical Analyst agent
**Phase 5:** Orchestration pipeline + API endpoints
**Phase 6:** Additional specialist agents
**Phase 7:** Strategy Agent synthesis
**Phase 8:** Polish and testing

### Version 2: Data Expansion

**Timeline:** Weeks 7-12  
**Goal:** Comprehensive data coverage for Iranian markets

**Features:**
- Gold price provider
- Currency rate provider (USD/IRR, USDT/IRR)
- Economic indicator provider
- News provider
- Company fundamentals provider
- Multiple provider fallback
- Data caching with PostgreSQL
- Provider health monitoring
- Additional indicators (CCI, ADX, Stochastic, Ichimoku)
- Trend detection analyzer
- Support/resistance analyzer

### Version 3: Intelligence

**Timeline:** Weeks 13-18  
**Goal:** Sophisticated multi-agent analysis

**Features:**
- All specialist agents fully implemented
- Strategy Agent with conflict reasoning
- Agent prompt optimization
- Sentiment analysis
- Pattern recognition
- Advanced risk modeling
- Backtesting framework
- Strategy evaluation and scoring
- Historical strategy comparison

### Version 4: User Experience

**Timeline:** Weeks 19-24  
**Goal:** Complete user interface and workflow

**Features:**
- Web dashboard with:
  - Asset watchlist
  - Strategy history
  - Visual strategy reports
  - Interactive charts
  - News feed
  - Economic calendar
- User authentication
- User preferences and settings
- Custom indicator support
- Custom agent rules
- Alert system
- Email notifications
- Mobile-responsive design

### Version 5: Advanced Analytics

**Timeline:** Weeks 25-30  
**Goal:** Advanced analytical capabilities

**Features:**
- Portfolio analysis
- Correlation analysis
- Sector rotation analysis
- Market regime detection
- Volatility forecasting
- Risk parity models
- Monte Carlo simulation
- Scenario analysis
- Stress testing
- Custom strategy templates

### Version 6: Ecosystem

**Timeline:** Weeks 31-36  
**Goal:** Platform ecosystem and extensibility

**Features:**
- Plugin system for custom indicators
- Plugin system for custom agents
- Plugin system for custom providers
- Strategy sharing and community
- API for third-party integration
- Webhook support
- Export to Excel/PDF
- Multi-language support
- International market expansion
- Advanced reporting

---

## 14. Version 1 Scope

### V1 Includes

| Component | Status | Description |
|-----------|--------|-------------|
| Solution structure | ✅ | 6 projects with clean architecture |
| Domain models | ✅ | 20+ models covering all domains |
| Provider interfaces | ✅ | 7 provider interfaces |
| Analysis interfaces | ✅ | Indicator, engine, analyzer interfaces |
| AI interfaces | ✅ | LLM provider and agent interfaces |
| Orchestration interface | ✅ | Strategy orchestrator interface |
| Configuration | ✅ | LLM, database, data source settings |
| DI foundation | ✅ | Extension methods for all layers |
| Unit tests | ✅ | 34 tests for domain models |
| IndicatorEngine | ✅ | Placeholder implementation |
| StrategyOrchestrator | ✅ | Skeleton pipeline |
| TSETMC provider | ⏳ | Phase 2 |
| Indicator implementations | ⏳ | Phase 3 |
| LLM provider | ⏳ | Phase 4 |
| Technical Analyst agent | ⏳ | Phase 4 |
| API endpoints | ⏳ | Phase 5 |

### V1 Does NOT Include

- Full multi-agent system (only Technical Analyst)
- Strategy synthesis agent (Phase 7)
- News, economic, political data providers
- User authentication
- Web dashboard or UI
- Automatic trading
- Multiple market support
- Backtesting
- Complex caching

---

## 15. Version 2 Scope

### Data Providers

| Provider | Description | Priority |
|----------|-------------|----------|
| `TsetmcMarketDataProvider` | Iranian stock OHLCV data | High |
| `GoldPriceProvider` | Gold coin/bullion prices | High |
| `CurrencyProvider` | USD/IRR, USDT/IRR rates | High |
| `EconomicIndicatorProvider` | Inflation, interest rates | Medium |
| `NewsProvider` | Financial news | Medium |
| `CompanyDataProvider` | Company fundamentals | Medium |

### Indicators

| Indicator | Description | Priority |
|-----------|-------------|----------|
| CCI | Commodity Channel Index | High |
| ADX | Average Directional Index | High |
| Stochastic | Stochastic Oscillator | High |
| Ichimoku | Ichimoku Cloud | Medium |
| VWAP | Volume Weighted Average Price | Medium |
| OBV | On-Balance Volume | Medium |
| MFI | Money Flow Index | Low |
| Williams %R | Williams Percent Range | Low |

### Analyzers

| Analyzer | Description | Priority |
|----------|-------------|----------|
| TrendDetector | Market regime identification | High |
| SupportResistanceAnalyzer | Key price level detection | High |
| VolumeAnalyzer | Volume pattern analysis | Medium |
| VolatilityAnalyzer | Volatility regime assessment | Medium |

---

## 16. Version 3 Scope

### Agent Improvements

| Agent | Improvement | Priority |
|-------|-------------|----------|
| TechnicalAnalyst | Pattern recognition | High |
| FundamentalAnalyst | Full financial statement analysis | High |
| MacroAnalyst | Currency and commodity integration | High |
| NewsAnalyst | Sentiment analysis | Medium |
| PoliticalRiskAnalyst | Sanctions tracking | Medium |
| RiskAnalyst | Advanced risk modeling | Medium |
| StrategyAgent | Conflict resolution reasoning | High |

### Analytics

| Feature | Description | Priority |
|---------|-------------|----------|
| Backtesting | Test strategies against historical data | High |
| Strategy evaluation | Score strategy accuracy | High |
| Historical comparison | Compare past strategies | Medium |
| Pattern recognition | Chart pattern detection | Medium |

---

## 17. Long-Term Vision

### Platform Evolution

**From Tool to Platform:**
- StrategyForge evolves from a single-user tool to a platform
- Supports multiple users with different preferences
- Enables strategy sharing and community features
- Provides API for third-party integration

**From Iran to Global:**
- Architecture supports multiple markets
- Can be adapted for any financial market
- International expansion with market-specific providers

**From Analysis to Intelligence:**
- Advanced machine learning for pattern recognition
- Predictive analytics for market regimes
- Natural language generation for strategy narratives
- Automated strategy optimization

### Future Capabilities

| Capability | Timeline | Description |
|------------|----------|-------------|
| **Portfolio Analysis** | V5 | Multi-asset portfolio optimization |
| **Correlation Analysis** | V5 | Cross-asset correlation tracking |
| **Sector Rotation** | V5 | Sector rotation strategies |
| **Volatility Forecasting** | V5 | Predict volatility regimes |
| **Risk Parity** | V5 | Risk parity portfolio construction |
| **Monte Carlo** | V5 | Scenario simulation |
| **Plugin System** | V6 | Custom indicators, agents, providers |
| **Community** | V6 | Strategy sharing and discussion |
| **Mobile App** | V6 | Native mobile interface |
| **API Ecosystem** | V6 | Third-party integration |

---

## 18. Success Criteria

### V1 Success

- [ ] Solution builds with zero errors
- [ ] All unit tests pass
- [ ] Can fetch real Iranian stock data from TSETMC
- [ ] Indicators compute correctly against known values
- [ ] LLM connects to FreeLLM and produces analysis
- [ ] Technical Analyst agent produces structured output
- [ ] Orchestration pipeline produces a StrategyReport
- [ ] API endpoints respond correctly

### V2 Success

- [ ] Can analyze any Iranian stock with available data
- [ ] Gold and currency data integrated
- [ ] Economic indicators displayed
- [ ] News integrated into analysis
- [ ] Multiple indicators providing comprehensive view

### V3 Success

- [ ] All specialist agents producing quality analysis
- [ ] Strategy Agent synthesizes agent outputs coherently
- [ ] Conflicts between agents are identified and explained
- [ ] StrategyReport is actionable and well-structured
- [ ] Backtesting shows reasonable strategy quality

### V4 Success

- [ ] Complete web dashboard with all features
- [ ] User can manage asset watchlist
- [ ] Strategy history is preserved and searchable
- [ ] Visual charts and interactive analysis
- [ ] Responsive design works on mobile

---

## 19. Risks and Mitigations

### Technical Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| TSETMC endpoints change | Data collection fails | Provider abstraction allows rapid replacement |
| LLM output quality inconsistent | Analysis unreliable | Structured prompts, output validation, retry |
| LLM context window limits | Cannot feed all evidence | Analysis Layer summarizes before sending |
| PostgreSQL connection issues | Caching fails | System functions without cache |
| Indicator calculations wrong | Incorrect analysis | Comprehensive unit tests with known values |

### Domain Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Iranian data sources less standardized | Data quality varies | Provider normalization, metadata tracking |
| Free data sources unreliable | Data gaps | Multiple providers, caching, graceful errors |
| AI appears authoritative | User over-trusts | StrategyReport marks uncertainty explicitly |
| News/political data hard to source | Limited context | Agents note missing information |
| Legal considerations | Compliance issues | Publicly accessible data only |

### Architectural Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Over-engineering | Wasted effort | Strict V1 scope, build only when needed |
| Prompt engineering difficult | Delayed agent quality | Prompts versioned, iterative refinement |
| Strategy synthesis hard | Poor output | Start simple, improve with feedback |

---

## 20. Appendix

### Glossary

| Term | Definition |
|------|-----------|
| **Candle** | OHLCV price bar for a specific time period |
| **Indicator** | Deterministic mathematical calculation on price/volume data |
| **Agent** | AI-powered analysis module reasoning over evidence via LLM |
| **Provider** | Software adapter fetching data from an external source |
| **Evidence** | Structured data available for AI analysis |
| **StrategyReport** | Final structured output combining all analyses |
| **TSETMC** | Tehran Securities Exchange Technology Management Co. |
| **FreeLLM** | User's locally running OpenAI-compatible LLM API |
| **Orchestrator** | Component coordinating the full pipeline |

### Assumptions

1. TSETMC public HTTP endpoints are accessible
2. FreeLLM API is running locally
3. PostgreSQL is available via Docker
4. Internet access is available
5. Iranian stock symbols are provided by the user
6. .NET 10.0 is the target runtime
7. Docker is available for infrastructure

### References

- [Architecture Document](ARCHITECTURE.md)
- [Development Roadmap](ROADMAP.md)
- [API Reference](API.md)
- [Data Models](DATA_MODELS.md)
- [Interface Contracts](INTERFACES.md)
- [Development Guidelines](DEVELOPMENT.md)
