# StrategyForge — Development Roadmap

**Version:** 1.0  
**Last Updated:** August 21, 2026

---

## Table of Contents

1. [Roadmap Overview](#1-roadmap-overview)
2. [Version 1: Foundation](#2-version-1-foundation)
3. [Version 2: Data Expansion](#3-version-2-data-expansion)
4. [Version 3: Intelligence](#4-version-3-intelligence)
5. [Version 4: User Experience](#5-version-4-user-experience)
6. [Version 5: Advanced Analytics](#6-version-5-advanced-analytics)
7. [Version 6: Ecosystem](#7-version-6-ecosystem)
8. [Milestone Definitions](#8-milestone-definitions)
9. [Current Status](#9-current-status)

---

## 1. Roadmap Overview

StrategyForge development follows an incremental approach. Each version produces something runnable and testable.

```
V1: Foundation          → Working pipeline, basic analysis
V2: Data Expansion      → Comprehensive data coverage
V3: Intelligence        → Sophisticated multi-agent analysis
V4: User Experience     → Complete web interface
V5: Advanced Analytics  → Portfolio and risk analysis
V6: Ecosystem           → Platform and extensibility
```

### Timeline Summary

| Version | Focus | Duration | Key Deliverable |
|---------|-------|----------|-----------------|
| V1 | Foundation | 6 weeks | Working strategy generation pipeline |
| V2 | Data Expansion | 6 weeks | Comprehensive Iranian market data |
| V3 | Intelligence | 6 weeks | Multi-agent analysis with synthesis |
| V4 | User Experience | 6 weeks | Complete web dashboard |
| V5 | Advanced Analytics | 6 weeks | Portfolio and risk analysis |
| V6 | Ecosystem | 6 weeks | Platform and extensibility |

---

## 2. Version 1: Foundation

**Timeline:** Weeks 1-6  
**Goal:** Establish the complete foundation and prove the architecture works

### Phase 0: Architecture Confirmation ✅

**Status:** Complete

**Deliverables:**
- [x] Project vision document
- [x] Architecture document
- [x] Development rules established
- [x] Domain boundaries defined
- [x] Interface contracts designed

**Verification:** Architecture reviewed and approved

---

### Phase 1: Solution Foundation ✅

**Status:** Complete

**Deliverables:**
- [x] .NET solution with 6 projects
- [x] Project references configured
- [x] Core domain models (20+ models)
- [x] All provider interfaces
- [x] All analysis interfaces
- [x] All AI interfaces
- [x] Orchestration interface
- [x] Configuration models
- [x] DI foundation
- [x] Unit tests (34 tests)
- [x] IndicatorEngine implementation
- [x] StrategyOrchestrator skeleton

**Verification:**
- [x] `dotnet build` succeeds
- [x] `dotnet test` passes (34/34)

---

### Phase 2: Market Data Provider

**Status:** Pending  
**Goal:** Implement the first real Iranian market data provider

**Deliverables:**
- [ ] `TsetmcMarketDataProvider` implementation
- [ ] HTTP client configuration
- [ ] Response parsing and normalization
- [ ] Error handling and retry logic
- [ ] Provider registration in DI
- [ ] Integration test against real TSETMC

**Files to create/modify:**
```
src/StrategyForge.Infrastructure/
├── Providers/
│   ├── TsetmcMarketDataProvider.cs
│   └── Models/
│       └── TsetmcResponse.cs
└── ServiceCollectionExtensions.cs (update)
```

**Verification:**
- [ ] Can fetch real Iranian stock candle data
- [ ] Data normalizes correctly to domain Candle model
- [ ] Error handling works for network failures

---

### Phase 3: Indicator Engine

**Status:** Pending  
**Goal:** Implement the deterministic analysis engine

**Deliverables:**
- [ ] `RsiIndicator` implementation
- [ ] `MacdIndicator` implementation
- [ ] `SmaIndicator` implementation
- [ ] `EmaIndicator` implementation
- [ ] `BollingerBandsIndicator` implementation
- [ ] `AtrIndicator` implementation
- [ ] Indicator configuration system
- [ ] Comprehensive unit tests with known values

**Files to create:**
```
src/StrategyForge.Analysis/
├── Indicators/
│   ├── RsiIndicator.cs
│   ├── MacdIndicator.cs
│   ├── SmaIndicator.cs
│   ├── EmaIndicator.cs
│   ├── BollingerBandsIndicator.cs
│   └── AtrIndicator.cs
└── ServiceCollectionExtensions.cs (update)
```

**Verification:**
- [ ] Indicators produce correct values against hand-calculated test cases
- [ ] All unit tests pass
- [ ] Indicators can be configured (period, source, etc.)

---

### Phase 4: LLM Integration

**Status:** Pending  
**Goal:** Connect to local FreeLLM and implement first AI agent

**Deliverables:**
- [ ] `OpenAiCompatibleLlmProvider` implementation
- [ ] `ILLMProvider` registration in DI
- [ ] `BaseAgent` class with shared logic
- [ ] `TechnicalAnalystAgent` implementation
- [ ] Prompt templates for Technical Analyst
- [ ] JSON response parsing and validation
- [ ] Error handling for LLM failures

**Files to create:**
```
src/StrategyForge.AI/
├── Providers/
│   └── OpenAiCompatibleLlmProvider.cs
├── Agents/
│   ├── BaseAgent.cs
│   └── TechnicalAnalystAgent.cs
├── Prompts/
│   └── TechnicalAnalystPrompt.cs
└── ServiceCollectionExtensions.cs (update)
```

**Verification:**
- [ ] Can connect to FreeLLM API
- [ ] Technical Analyst agent produces structured output
- [ ] Response parsing handles malformed output gracefully

---

### Phase 5: Orchestration

**Status:** Pending  
**Goal:** Wire up the complete pipeline

**Deliverables:**
- [ ] `StrategyOrchestrator` full implementation
- [ ] Asset registration/retrieval
- [ ] Full pipeline: Data → Analysis → Agent → StrategyReport
- [ ] ASP.NET Core Web API endpoints
- [ ] API response models
- [ ] Error handling and logging throughout
- [ ] Health check endpoint

**Files to create/modify:**
```
src/StrategyForge.Api/
├── Controllers/
│   ├── AssetsController.cs
│   └── StrategyController.cs
└── Program.cs (update)
```

**Verification:**
- [ ] `POST /api/strategy/{assetId}` returns StrategyReport
- [ ] `GET /api/assets` lists registered assets
- [ ] `GET /health` returns healthy status

---

### Phase 6: Specialist Agents

**Status:** Pending  
**Goal:** Add remaining specialist agents

**Deliverables:**
- [ ] `FundamentalAnalystAgent`
- [ ] `MacroAnalystAgent`
- [ ] `NewsAnalystAgent`
- [ ] `PoliticalRiskAnalystAgent`
- [ ] `RiskAnalystAgent`
- [ ] Prompt templates for each
- [ ] Agent registration in DI

**Files to create:**
```
src/StrategyForge.AI/Agents/
├── FundamentalAnalystAgent.cs
├── MacroAnalystAgent.cs
├── NewsAnalystAgent.cs
├── PoliticalRiskAnalystAgent.cs
└── RiskAnalystAgent.cs
```

**Verification:**
- [ ] All agents produce structured output
- [ ] All agents handle missing data gracefully

---

### Phase 7: Strategy Synthesis

**Status:** Pending  
**Goal:** Implement the Strategy Agent

**Deliverables:**
- [ ] `StrategyAgent` implementation
- [ ] Conflict detection logic
- [ ] Scenario construction
- [ ] StrategyReport population
- [ ] Evidence traceability

**Files to create:**
```
src/StrategyForge.AI/Agents/
└── StrategyAgent.cs
```

**Verification:**
- [ ] StrategyAgent produces coherent StrategyReport
- [ ] Conflicts between agents are identified
- [ ] Scenarios are properly constructed

---

### Phase 8: Polish and Testing

**Status:** Pending  
**Goal:** Complete V1 with quality assurance

**Deliverables:**
- [ ] End-to-end integration tests
- [ ] Performance testing
- [ ] Error scenario testing
- [ ] Documentation updates
- [ ] Configuration validation
- [ ] Logging improvements

**Verification:**
- [ ] All tests pass
- [ ] No critical bugs
- [ ] Documentation is complete

---

## 3. Version 2: Data Expansion

**Timeline:** Weeks 7-12  
**Goal:** Comprehensive data coverage for Iranian markets

### Data Providers

| Provider | Description | Priority | Effort |
|----------|-------------|----------|--------|
| `GoldPriceProvider` | Gold coin/bullion prices | High | 2 days |
| `CurrencyProvider` | USD/IRR, USDT/IRR rates | High | 2 days |
| `EconomicIndicatorProvider` | Inflation, interest rates | Medium | 3 days |
| `NewsProvider` | Financial news | Medium | 3 days |
| `CompanyDataProvider` | Company fundamentals | Medium | 3 days |

### Indicators

| Indicator | Description | Priority | Effort |
|-----------|-------------|----------|--------|
| CCI | Commodity Channel Index | High | 1 day |
| ADX | Average Directional Index | High | 1 day |
| Stochastic | Stochastic Oscillator | High | 1 day |
| Ichimoku | Ichimoku Cloud | Medium | 2 days |
| VWAP | Volume Weighted Average Price | Medium | 1 day |
| OBV | On-Balance Volume | Medium | 1 day |
| MFI | Money Flow Index | Low | 1 day |
| Williams %R | Williams Percent Range | Low | 1 day |

### Analyzers

| Analyzer | Description | Priority | Effort |
|----------|-------------|----------|--------|
| TrendDetector | Market regime identification | High | 2 days |
| SupportResistanceAnalyzer | Key price level detection | High | 2 days |
| VolumeAnalyzer | Volume pattern analysis | Medium | 2 days |
| VolatilityAnalyzer | Volatility regime assessment | Medium | 2 days |

### Infrastructure

| Feature | Description | Priority | Effort |
|---------|-------------|----------|--------|
| PostgreSQL caching | Cache market data | High | 3 days |
| Provider fallback | Multiple providers per interface | High | 2 days |
| Provider health monitoring | Track provider reliability | Medium | 2 days |
| Data refresh scheduling | Periodic data updates | Medium | 2 days |

---

## 4. Version 3: Intelligence

**Timeline:** Weeks 13-18  
**Goal:** Sophisticated multi-agent analysis

### Agent Improvements

| Agent | Improvement | Priority | Effort |
|-------|-------------|----------|--------|
| TechnicalAnalyst | Pattern recognition | High | 3 days |
| FundamentalAnalyst | Full financial statement analysis | High | 3 days |
| MacroAnalyst | Currency and commodity integration | High | 2 days |
| NewsAnalyst | Sentiment analysis | Medium | 2 days |
| PoliticalRiskAnalyst | Sanctions tracking | Medium | 2 days |
| RiskAnalyst | Advanced risk modeling | Medium | 3 days |
| StrategyAgent | Conflict resolution reasoning | High | 3 days |

### Analytics

| Feature | Description | Priority | Effort |
|---------|-------------|----------|--------|
| Backtesting | Test strategies against historical data | High | 5 days |
| Strategy evaluation | Score strategy accuracy | High | 3 days |
| Historical comparison | Compare past strategies | Medium | 2 days |
| Pattern recognition | Chart pattern detection | Medium | 3 days |

---

## 5. Version 4: User Experience

**Timeline:** Weeks 19-24  
**Goal:** Complete user interface and workflow

### Web Dashboard Features

| Feature | Description | Priority | Effort |
|---------|-------------|----------|--------|
| Asset watchlist | Manage monitored assets | High | 3 days |
| Strategy history | View past strategies | High | 2 days |
| Visual strategy reports | Interactive report viewer | High | 5 days |
| Charts | Interactive price charts | High | 5 days |
| News feed | Aggregated news display | Medium | 3 days |
| Economic calendar | Upcoming events | Medium | 2 days |
| User authentication | Login and preferences | Medium | 3 days |
| User settings | Customize analysis preferences | Low | 2 days |

### UI Technology

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| Frontend | React/Blazor | Modern, interactive UI |
| Charts | TradingView Lightweight Charts | Free, financial-focused |
| Styling | Tailwind CSS | Rapid development |
| State Management | React Context/Redux | Complex state handling |

### Mobile

| Feature | Description | Priority | Effort |
|---------|-------------|----------|--------|
| Responsive design | Works on mobile browsers | High | 3 days |
| Push notifications | Alert on strategy updates | Medium | 2 days |

---

## 6. Version 5: Advanced Analytics

**Timeline:** Weeks 25-30  
**Goal:** Advanced analytical capabilities

### Portfolio Analysis

| Feature | Description | Priority | Effort |
|---------|-------------|----------|--------|
| Multi-asset analysis | Analyze portfolio of assets | High | 5 days |
| Correlation analysis | Cross-asset correlation | High | 3 days |
| Sector rotation | Sector rotation strategies | Medium | 3 days |
| Risk parity | Risk parity portfolios | Medium | 3 days |

### Risk Analysis

| Feature | Description | Priority | Effort |
|---------|-------------|----------|--------|
| Volatility forecasting | Predict volatility regimes | High | 3 days |
| Monte Carlo simulation | Scenario simulation | Medium | 5 days |
| Stress testing | Stress test portfolios | Medium | 3 days |
| VaR calculation | Value at Risk | Low | 2 days |

### Strategy Templates

| Feature | Description | Priority | Effort |
|---------|-------------|----------|--------|
| Custom templates | User-defined strategy templates | Medium | 3 days |
| Strategy sharing | Share strategies with others | Low | 2 days |
| Strategy optimization | Optimize strategy parameters | Low | 3 days |

---

## 7. Version 6: Ecosystem

**Timeline:** Weeks 31-36  
**Goal:** Platform ecosystem and extensibility

### Plugin System

| Plugin Type | Description | Priority | Effort |
|-------------|-------------|----------|--------|
| Custom indicators | User-defined indicators | High | 5 days |
| Custom agents | User-defined AI agents | Medium | 5 days |
| Custom providers | User-defined data providers | Medium | 5 days |

### API Ecosystem

| Feature | Description | Priority | Effort |
|---------|-------------|----------|--------|
| Public API | Third-party integration | High | 5 days |
| Webhook support | Event notifications | Medium | 2 days |
| SDK | Client libraries | Low | 5 days |

### Community

| Feature | Description | Priority | Effort |
|---------|-------------|----------|--------|
| Strategy sharing | Community strategies | Medium | 3 days |
| Discussion forum | Strategy discussion | Low | 5 days |
| Documentation | Comprehensive docs | High | 5 days |

### Internationalization

| Feature | Description | Priority | Effort |
|---------|-------------|----------|--------|
| Multi-language | UI in multiple languages | Medium | 5 days |
| Multi-market | Support other markets | High | 10 days |
| Multi-currency | Support multiple currencies | Medium | 3 days |

---

## 8. Milestone Definitions

### V1 Milestones

| Milestone | Criteria | Target |
|-----------|----------|--------|
| M1.1 | Solution builds, tests pass | Week 1 ✅ |
| M1.2 | Can fetch real market data | Week 2 |
| M1.3 | Indicators compute correctly | Week 3 |
| M1.4 | LLM produces analysis | Week 4 |
| M1.5 | Full pipeline works | Week 5 |
| M1.6 | All agents functional | Week 6 |

### V2 Milestones

| Milestone | Criteria | Target |
|-----------|----------|--------|
| M2.1 | Gold/currency data integrated | Week 8 |
| M2.2 | Economic indicators available | Week 9 |
| M2.3 | News integrated | Week 10 |
| M2.4 | Caching working | Week 11 |
| M2.5 | All V2 features complete | Week 12 |

### V3 Milestones

| Milestone | Criteria | Target |
|-----------|----------|--------|
| M3.1 | All agents improved | Week 14 |
| M3.2 | Backtesting working | Week 15 |
| M3.3 | Strategy evaluation working | Week 16 |
| M3.4 | Strategy Agent improved | Week 17 |
| M3.5 | All V3 features complete | Week 18 |

---

## 9. Current Status

### V1 Progress

| Phase | Status | Progress |
|-------|--------|----------|
| Phase 0: Architecture | ✅ Complete | 100% |
| Phase 1: Foundation | ✅ Complete | 100% |
| Phase 2: Market Data | ⏳ Pending | 0% |
| Phase 3: Indicators | ⏳ Pending | 0% |
| Phase 4: LLM Integration | ⏳ Pending | 0% |
| Phase 5: Orchestration | ⏳ Pending | 0% |
| Phase 6: Specialist Agents | ⏳ Pending | 0% |
| Phase 7: Strategy Synthesis | ⏳ Pending | 0% |
| Phase 8: Polish | ⏳ Pending | 0% |

### Overall V1 Progress

```
[████████████░░░░░░░░░░░░░░░░░░] 25% Complete
```

### Next Steps

1. **Phase 2:** Implement `TsetmcMarketDataProvider`
2. **Phase 3:** Implement indicator engine with first 6 indicators
3. **Phase 4:** Implement LLM integration and Technical Analyst agent
4. **Phase 5:** Wire up full orchestration pipeline
5. **Phase 6:** Add remaining specialist agents
6. **Phase 7:** Implement Strategy Agent synthesis
7. **Phase 8:** Polish and comprehensive testing

---

## Appendix: Effort Estimates

### V1 Total Effort

| Phase | Estimated Days |
|-------|----------------|
| Phase 0 | 1 |
| Phase 1 | 2 |
| Phase 2 | 3 |
| Phase 3 | 5 |
| Phase 4 | 5 |
| Phase 5 | 5 |
| Phase 6 | 5 |
| Phase 7 | 5 |
| Phase 8 | 4 |
| **Total** | **35 days** |

### V2 Total Effort

| Category | Estimated Days |
|----------|----------------|
| Data Providers | 13 |
| Indicators | 9 |
| Analyzers | 8 |
| Infrastructure | 9 |
| **Total** | **39 days** |

### V3 Total Effort

| Category | Estimated Days |
|----------|----------------|
| Agent Improvements | 18 |
| Analytics | 13 |
| **Total** | **31 days** |

### V4 Total Effort

| Category | Estimated Days |
|----------|----------------|
| Dashboard Features | 25 |
| Mobile | 5 |
| **Total** | **30 days** |

### V5 Total Effort

| Category | Estimated Days |
|----------|----------------|
| Portfolio Analysis | 14 |
| Risk Analysis | 13 |
| Strategy Templates | 8 |
| **Total** | **35 days** |

### V6 Total Effort

| Category | Estimated Days |
|----------|----------------|
| Plugin System | 15 |
| API Ecosystem | 12 |
| Community | 13 |
| Internationalization | 23 |
| **Total** | **63 days** |

### Grand Total

```
V1: 35 days
V2: 39 days
V3: 31 days
V4: 30 days
V5: 35 days
V6: 63 days
─────────────
Total: 233 days (~47 weeks)
```

**Note:** These are rough estimates. Actual effort may vary based on complexity, learning curve, and unforeseen challenges.
