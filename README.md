# StrategyForge

**AI-Assisted Financial Market Analysis Platform**

StrategyForge is a free-first, evidence-driven AI strategy generation platform focused initially on Iranian financial markets. It collects market data, economic indicators, political information, and news, then feeds this structured evidence to specialized AI agents that produce structured investment strategies.

> **StrategyForge is NOT an automated trading bot.**  
> It produces analysis and strategy. The human makes the final decision and executes trades manually.

---

## Vision

```
USER REQUEST: "Analyze this stock"
        ↓
┌─────────────────────────────────────────┐
│           DATA LAYER                    │
│  Market data, economic indicators,      │
│  news, company fundamentals,            │
│  currency rates, gold prices            │
└──────────────────┬──────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│         ANALYSIS LAYER                  │
│  Technical indicators (RSI, MACD, etc), │
│  trend detection, support/resistance,   │
│  fundamental metrics                    │
└──────────────────┬──────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│        AI STRATEGY LAYER                │
│  Technical Agent, Fundamental Agent,    │
│  Macro Agent, News Agent, Risk Agent,   │
│  Strategy Agent (synthesis)             │
└──────────────────┬──────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│        STRATEGY REPORT                  │
│  Bull/Base/Bear scenarios,              │
│  entry/exit zones, stop levels,         │
│  risk/reward, confidence, reasoning     │
└──────────────────┬──────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│        HUMAN DECISION                   │
│  User evaluates the strategy and        │
│  decides whether to trade               │
└─────────────────────────────────────────┘
```

---

## Quick Start

### Prerequisites

- .NET 10.0 SDK or later
- Docker (for PostgreSQL)
- FreeLLM API running locally (for AI analysis)

### Setup

```bash
# Clone the repository
git clone <repository-url>
cd StrategyForge

# Build the solution
dotnet build

# Run tests
dotnet test

# Start PostgreSQL via Docker
docker run -d --name strategyforge-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=strategyforge \
  -p 5432:5432 \
  postgres:16

# Run the API
cd src/StrategyForge.Api
dotnet run
```

### API Endpoints

```
GET  /health                    - Health check
GET  /api/assets                - List all assets
POST /api/assets                - Register a new asset
GET  /api/assets/{id}           - Get asset details
POST /api/strategy/{assetId}    - Generate strategy for asset
```

---

## Architecture

StrategyForge follows a clean three-layer architecture:

| Layer | Responsibility | Project |
|-------|---------------|---------|
| **Data Layer** | Collect and normalize external information | `StrategyForge.Infrastructure` |
| **Analysis Layer** | Deterministic calculations and indicator computation | `StrategyForge.Analysis` |
| **AI Strategy Layer** | LLM-powered agents reason over structured evidence | `StrategyForge.AI` |
| **Orchestration** | Coordinate the three layers into a pipeline | `StrategyForge.Orchestration` |
| **Domain** | Core models, interfaces, contracts | `StrategyForge.Domain` |
| **API** | ASP.NET Core Web API endpoints | `StrategyForge.Api` |

### Dependency Flow

```
Domain (zero dependencies)
    ↑
    ├── Analysis
    ├── AI
    ├── Infrastructure
    │       ↑
    │       └── Orchestration ← Analysis + AI
    │               ↑
    └───────────────└── Api (composition root)
```

---

## Project Structure

```
StrategyForge/
├── StrategyForge.sln
├── README.md
├── docs/
│   ├── VISION.md                 # Complete project vision
│   ├── ARCHITECTURE.md           # Architecture details
│   ├── ROADMAP.md                # Development roadmap
│   ├── API.md                    # API reference
│   ├── DATA_MODELS.md            # Data models reference
│   ├── INTERFACES.md             # Interface contracts
│   └── DEVELOPMENT.md            # Development guidelines
├── src/
│   ├── StrategyForge.Domain/     # Core domain (no dependencies)
│   ├── StrategyForge.Infrastructure/  # Data providers, DB, LLM
│   ├── StrategyForge.Analysis/   # Indicator engine
│   ├── StrategyForge.AI/         # Agents and LLM integration
│   ├── StrategyForge.Orchestration/  # Pipeline coordination
│   └── StrategyForge.Api/        # ASP.NET Core Web API
└── tests/
    ├── StrategyForge.Domain.Tests/
    ├── StrategyForge.Analysis.Tests/
    ├── StrategyForge.AI.Tests/
    ├── StrategyForge.Orchestration.Tests/
    └── StrategyForge.Integration.Tests/
```

---

## Technology Stack

| Component | Technology | Cost |
|-----------|-----------|------|
| Framework | .NET 10.0 / ASP.NET Core | Free |
| Database | PostgreSQL (Docker) | Free |
| LLM | FreeLLM (local, OpenAI-compatible) | Free |
| Market Data | TSETMC public endpoints | Free |
| Indicators | Custom implementation | Free |
| Testing | xUnit + Moq | Free |

---

## Current Status

**Phase 1: Foundation** ✅ Complete

- [x] Solution structure with 6 projects
- [x] Core domain models (20+ models)
- [x] All provider interfaces
- [x] All analysis interfaces
- [x] All AI interfaces
- [x] Orchestration interface
- [x] Configuration models
- [x] DI foundation
- [x] Unit tests (34 tests passing)
- [x] IndicatorEngine implementation
- [x] StrategyOrchestrator skeleton

**Next: Phase 2 - Market Data Provider**

---

## Documentation

| Document | Description |
|----------|-------------|
| [VISION.md](docs/VISION.md) | Complete project vision and future roadmap |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Architecture details and design decisions |
| [ROADMAP.md](docs/ROADMAP.md) | Development phases and milestones |
| [API.md](docs/API.md) | REST API reference |
| [DATA_MODELS.md](docs/DATA_MODELS.md) | Domain models and data structures |
| [INTERFACES.md](docs/INTERFACES.md) | Interface contracts |
| [DEVELOPMENT.md](docs/DEVELOPMENT.md) | Development guidelines |

---

## License

[License TBD]

---

## Contributing

[Contributing guidelines TBD]
