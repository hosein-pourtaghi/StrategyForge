# StrategyForge API — Preview Run Doc

## How to reproduce artifacts

1. Build the API project:
   ```bash
   dotnet build src/StrategyForge.Api/StrategyForge.Api.csproj -c Release
   ```
   No env files needed — configuration is in `src/StrategyForge.Api/appsettings.json`.
   If the LLM server is running elsewhere, update the `LlmProvider.BaseUrl` in appsettings.json before building.

## How to run the server

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet \
  src/StrategyForge.Api/bin/Release/net10.0/StrategyForge.Api.dll \
  --urls "http://localhost:5120"
```

- **Port**: 5120
- **Swagger UI**: served at root (`/`) in Development mode
- **Swagger JSON**: `/swagger/v1/swagger.json`
- **Health check**: `GET /health`
- **Environment**: Must be `Development` for Swagger UI to appear

## Known dependencies

- PostgreSQL (optional — in-memory instrument resolver is used by default)
- LLM server at `http://localhost:3000/v1` (OpenAI-compatible) — needed for full strategy generation
- External APIs (Nobitex, TGJU, TSETMC) are called for live market data
