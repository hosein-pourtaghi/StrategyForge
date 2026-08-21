# StrategyForge — Development Guidelines

**Version:** 1.0  
**Last Updated:** August 21, 2026

---

## Table of Contents

1. [Development Philosophy](#1-development-philosophy)
2. [Coding Standards](#2-coding-standards)
3. [Project Structure](#3-project-structure)
4. [Testing Guidelines](#4-testing-guidelines)
5. [Git Workflow](#5-git-workflow)
6. [Code Review](#6-code-review)
7. [Documentation](#7-documentation)
8. [Performance](#8-performance)
9. [Security](#9-security)
10. [Troubleshooting](#10-troubleshooting)

---

## 1. Development Philosophy

### Core Principles

1. **Understand before implementing:** Inspect relevant code and documentation before making changes
2. **Search before creating:** Look for existing functionality before adding new functionality
3. **Reuse before duplicating:** Do not create a second implementation of something that exists
4. **Small steps:** Build the system incrementally
5. **No unnecessary refactoring:** Do not refactor unrelated areas
6. **No unnecessary dependencies:** Prefer existing libraries and platform capabilities
7. **No invented data:** Never fabricate market, company, economic, or news information
8. **Explain important decisions:** Document architectural decisions and their rationale

### Development Process

For each phase:

1. **Explain** what we are building
2. **Explain** why
3. **Show** the files that will change
4. **Implement** only that phase
5. **Build** the solution
6. **Run** tests
7. **Fix** errors
8. **Show** what was completed
9. **Stop** and wait for approval before moving to the next major phase

---

## 2. Coding Standards

### C# Style

- Use `record` types for immutable models
- Use `required` keyword for essential properties
- Use `init` setters for property initialization
- Use `sealed` for classes that should not be inherited
- Use `IReadOnlyList` for collections in public APIs
- Use `async/await` for all I/O operations
- Use `CancellationToken` in all async methods
- Use `ILogger<T>` for structured logging
- Use dependency injection for all services

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `MarketDataBundle` |
| Interfaces | I + PascalCase | `IMarketDataProvider` |
| Methods | PascalCase | `GetHistoricalDataAsync` |
| Properties | PascalCase | `CurrentPrice` |
| Parameters | camelCase | `cancellationToken` |
| Local variables | camelCase | `candleData` |
| Constants | PascalCase | `SectionName` |

### File Organization

- One type per file (with exceptions for small related types)
- File name matches type name
- Organize by feature/layer, not by type
- Use folders for logical grouping

### Comments

- Use XML documentation for public APIs
- Use inline comments for complex logic
- Do not comment obvious code
- Explain "why", not "what"

---

## 3. Project Structure

### Layer Responsibilities

| Project | Purpose | Dependencies |
|---------|---------|--------------|
| `StrategyForge.Domain` | Core models, interfaces | None |
| `StrategyForge.Infrastructure` | External integrations | Domain |
| `StrategyForge.Analysis` | Deterministic calculations | Domain |
| `StrategyForge.AI` | LLM-powered agents | Domain |
| `StrategyForge.Orchestration` | Pipeline coordination | All |
| `StrategyForge.Api` | HTTP endpoints | All (composition root) |

### Adding New Files

**Provider:**
```
src/StrategyForge.Infrastructure/Providers/{ProviderName}Provider.cs
```

**Indicator:**
```
src/StrategyForge.Analysis/Indicators/{IndicatorName}Indicator.cs
```

**Agent:**
```
src/StrategyForge.AI/Agents/{AgentName}Agent.cs
```

**Analyzer:**
```
src/StrategyForge.Analysis/Analyzers/{AnalyzerName}Analyzer.cs
```

**Test:**
```
tests/StrategyForge.{Layer}.Tests/{Feature}/{FeatureTests}.cs
```

---

## 4. Testing Guidelines

### Test Categories

| Category | Purpose | Tools |
|----------|---------|-------|
| Unit Tests | Individual components in isolation | xUnit, Moq |
| Integration Tests | Component interaction with real infra | xUnit, WebApplicationFactory |
| Indicator Tests | Calculations against known data | xUnit with hand-calculated values |

### Test Naming Convention

```
Method_Scenario_ExpectedResult
```

**Examples:**
```csharp
[Fact]
public void Compute_WithOverboughtRsi_ReturnsOverboughtSignal()

[Fact]
public void GetHistoricalDataAsync_WithValidAsset_ReturnsCandles()

[Theory]
[InlineData(75)]
[InlineData(80)]
public void Compute_WhenRsiAbove70_ReturnsOverbought(decimal rsiValue)
```

### Test Structure

```csharp
[Fact]
public void Method_Scenario_ExpectedResult()
{
    // Arrange
    var input = CreateTestData();
    
    // Act
    var result = _systemUnderTest.Method(input);
    
    // Assert
    Assert.Equal(expectedValue, result.Property);
}
```

### Mocking Guidelines

- Mock external dependencies (HTTP, database, LLM)
- Do not mock domain models
- Use `Mock<T>` for interfaces
- Verify interactions when testing side effects
- Use `It.IsAny<T>()` sparingly (prefer specific matchers)

### Test Coverage

- Aim for >80% coverage on critical paths
- 100% coverage on indicator calculations
- Test both success and failure scenarios
- Test edge cases and boundary conditions

---

## 5. Git Workflow

### Branch Naming

```
feature/phase-2-market-data-provider
bugfix/fix-rsi-calculation
docs/update-api-reference
```

### Commit Messages

```
feat: implement TsetmcMarketDataProvider

- Add HTTP client for TSETMC API
- Implement candle data normalization
- Add error handling and retry logic
- Add unit tests with recorded responses

Closes #12
```

### Commit Convention

- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation changes
- `test:` Adding tests
- `refactor:` Code refactoring
- `chore:` Maintenance tasks

---

## 6. Code Review

### Review Checklist

- [ ] Code follows naming conventions
- [ ] No unnecessary dependencies added
- [ ] Domain layer remains independent
- [ ] Tests are included
- [ ] Documentation is updated
- [ ] No hard-coded values
- [ ] Error handling is appropriate
- [ ] Logging is meaningful

### Common Issues

- **Circular dependencies:** Check project references
- **Leaking abstractions:** Domain should not know about Infrastructure
- **Missing cancellation tokens:** All async methods need CancellationToken
- **Swallowing exceptions:** Log errors, don't silently ignore them

---

## 7. Documentation

### Required Documentation

- **XML comments** for all public APIs
- **README.md** updated for new features
- **Architecture docs** updated for design changes
- **API docs** updated for new endpoints
- **Data models** updated for new models

### Documentation Locations

| Document | Location | Update When |
|----------|----------|-------------|
| README.md | Root | New features |
| VISION.md | docs/ | Major changes |
| ARCHITECTURE.md | docs/ | Design changes |
| ROADMAP.md | docs/ | Phase completion |
| API.md | docs/ | New endpoints |
| DATA_MODELS.md | docs/ | New models |
| INTERFACES.md | docs/ | New interfaces |
| DEVELOPMENT.md | docs/ | Process changes |

---

## 8. Performance

### Async/Await

- Use `async/await` for all I/O operations
- Never block on async code (no `.Result`, no `.Wait()`)
- Pass `CancellationToken` through the call chain
- Use `ConfigureAwait(false)` in library code

### Caching

- Cache market data in PostgreSQL (configurable duration)
- Cache indicator results per asset/date range
- Do not cache LLM responses (non-deterministic)
- Use `MemoryCache` for short-lived in-memory caching

### Resource Management

- Use `IHttpClientFactory` for HTTP clients
- Use `IDisposable` pattern for unmanaged resources
- Use `using` statements for disposable objects
- Do not store HTTP clients in singletons

### Database

- Use asynchronous EF Core methods
- Use `AsNoTracking()` for read-only queries
- Use pagination for large result sets
- Use indexes for frequently queried columns

---

## 9. Security

### Secrets Management

- Never hard-code API keys or connection strings
- Use User Secrets for local development
- Use environment variables for production
- Never commit secrets to source control

### Input Validation

- Validate all external input
- Use model validation in controllers
- Sanitize user input
- Validate asset symbols and identifiers

### API Security

- Add authentication in V4+
- Rate limiting in production
- CORS configuration
- HTTPS in production

### Data Security

- Log sensitive data carefully
- Never expose credentials in API responses
- Encrypt sensitive data at rest
- Use secure connections for external APIs

---

## 10. Troubleshooting

### Common Issues

**Build fails after adding package:**
```bash
dotnet restore
dotnet build
```

**Tests fail:**
```bash
dotnet test --verbosity normal
```

**API won't start:**
- Check PostgreSQL is running
- Check FreeLLM is running
- Check ports are not in use

**Indicator calculation wrong:**
- Verify input data (candles)
- Check period parameters
- Compare with known values

**LLM connection fails:**
- Verify FreeLLM is running
- Check BaseUrl in appsettings.json
- Check network connectivity

### Debugging

- Use `ILogger<T>` for structured logging
- Check logs for errors and warnings
- Use debugger for complex logic
- Add breakpoints at key pipeline stages

### Getting Help

1. Check documentation in `docs/`
2. Search existing issues
3. Review test cases for examples
4. Ask for help with specific error messages
