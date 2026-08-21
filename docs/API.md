# StrategyForge — API Reference

**Version:** 1.0  
**Last Updated:** August 21, 2026

---

## Table of Contents

1. [Overview](#1-overview)
2. [Authentication](#2-authentication)
3. [Base URL](#3-base-url)
4. [Endpoints](#4-endpoints)
5. [Request/Response Models](#5-requestresponse-models)
6. [Error Handling](#6-error-handling)
7. [Examples](#7-examples)

---

## 1. Overview

StrategyForge exposes a REST API for interacting with the analysis platform. The API follows standard REST conventions and returns JSON responses.

### API Versioning

The current API is version 1. Future versions may introduce breaking changes.

### Rate Limiting

No rate limiting is currently implemented. Future versions may add rate limiting based on usage patterns.

---

## 2. Authentication

**V1:** No authentication required.

**Future:** API key authentication will be added for production use.

---

## 3. Base URL

```
http://localhost:5000 (HTTP)
https://localhost:5001 (HTTPS)
```

The base URL can be configured in `appsettings.json` or via environment variables.

---

## 4. Endpoints

### Health Check

```
GET /health
```

**Description:** Returns the health status of the API.

**Response:**
```json
{
  "status": "healthy",
  "version": "1.0.0"
}
```

**Status Codes:**
- `200 OK`: Service is healthy

---

### List Assets

```
GET /api/assets
```

**Description:** Returns a list of all registered assets.

**Response:**
```json
{
  "assets": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "symbol": "فولاد",
      "name": "Foolad Mobarakeh",
      "market": "TSE",
      "assetType": "Stock",
      "sector": "Metals",
      "isin": "IRO1FOLD0001"
    }
  ],
  "totalCount": 1
}
```

**Status Codes:**
- `200 OK`: Success

---

### Get Asset

```
GET /api/assets/{id}
```

**Description:** Returns details for a specific asset.

**Path Parameters:**
- `id` (string, required): The asset's unique identifier

**Response:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "symbol": "فولاد",
  "name": "Foolad Mobarakeh",
  "market": "TSE",
  "assetType": "Stock",
  "sector": "Metals",
  "isin": "IRO1FOLD0001",
  "metadata": {
    "provider": "TSETMC"
  }
}
```

**Status Codes:**
- `200 OK`: Success
- `404 Not Found`: Asset not found

---

### Create Asset

```
POST /api/assets
```

**Description:** Registers a new asset for analysis.

**Request Body:**
```json
{
  "symbol": "فولاد",
  "name": "Foolad Mobarakeh",
  "market": "TSE",
  "assetType": "Stock",
  "sector": "Metals",
  "isin": "IRO1FOLD0001"
}
```

**Required Fields:**
- `symbol` (string): Ticker symbol
- `name` (string): Asset name
- `market` (string): Exchange/market
- `assetType` (string): One of: Stock, Index, Currency, Commodity, Crypto, ETF, Bond, Other

**Optional Fields:**
- `sector` (string): Sector classification
- `isin` (string): International Securities Identification Number
- `metadata` (object): Additional key-value pairs

**Response:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "symbol": "فولاد",
  "name": "Foolad Mobarakeh",
  "market": "TSE",
  "assetType": "Stock",
  "sector": "Metals",
  "isin": "IRO1FOLD0001"
}
```

**Status Codes:**
- `201 Created`: Asset created successfully
- `400 Bad Request`: Invalid request body

---

### Generate Strategy

```
POST /api/strategy/{assetId}
```

**Description:** Generates a complete strategy report for the specified asset.

**Path Parameters:**
- `assetId` (string, required): The asset's unique identifier

**Query Parameters:**
- `horizons` (string, optional): Comma-separated time horizons (e.g., "ShortTerm,MediumTerm,LongTerm")
- `indicators` (string, optional): Comma-separated indicator names (e.g., "RSI,MACD,SMA")

**Response:**
```json
{
  "asset": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "symbol": "فولاد",
    "name": "Foolad Mobarakeh",
    "market": "TSE",
    "assetType": "Stock"
  },
  "generatedAt": "2026-08-21T10:30:00Z",
  "dataAsOf": "2026-08-20T16:00:00Z",
  "executiveSummary": {
    "overallSentiment": "Bullish",
    "summary": "Based on the available evidence, the stock shows bullish momentum...",
    "keyTakeaway": "RSI indicates strength, MACD shows bullish crossover",
    "criticalLevel": "Support at 12,500 IRR",
    "urgency": "Monitor for confirmation"
  },
  "marketContext": {
    "regime": "Uptrend",
    "description": "Price has been trending upward for 3 weeks",
    "currentPrice": 13200,
    "recentPriceChange": 5.2,
    "volumeContext": "Above average",
    "macroContext": "Currency stable, inflation moderating"
  },
  "technicalAnalysis": {
    "agentName": "TechnicalAnalyst",
    "assetSymbol": "فولاد",
    "generatedAt": "2026-08-21T10:30:00Z",
    "sentiment": "Bullish",
    "confidence": 0.75,
    "summary": "Technical indicators show bullish momentum...",
    "supportingEvidence": [
      {
        "content": "RSI at 65, indicating strength without overbought conditions",
        "type": "Calculation",
        "source": "RSI Indicator",
        "confidence": 0.9
      }
    ],
    "keyLevels": [
      {
        "price": 12500,
        "label": "Support",
        "timeHorizon": "ShortTerm",
        "significance": 0.8
      }
    ]
  },
  "bullishScenario": {
    "name": "Bullish",
    "description": "Price breaks above resistance with volume confirmation",
    "supportingEvidence": [...],
    "probabilityAssessment": "Possible",
    "confirmationConditions": ["Price closes above 13,500", "Volume increases"]
  },
  "baseScenario": {
    "name": "Base",
    "description": "Price consolidates in current range",
    "probabilityAssessment": "Most likely"
  },
  "bearishScenario": {
    "name": "Bearish",
    "description": "Price breaks below support",
    "probabilityAssessment": "Unlikely"
  },
  "shortTermStrategy": {
    "timeHorizon": "ShortTerm",
    "entryScenario": "Wait for pullback to 12,800 support",
    "entryZones": ["12,800 - 13,000 IRR"],
    "confirmationConditions": ["RSI stays above 50", "MACD remains positive"],
    "stopInvalidation": "12,200 IRR (-5%)",
    "targetLevels": ["14,000 IRR", "15,000 IRR"],
    "exitConditions": "RSI reaches 80 or price breaks below 12,200",
    "riskAssessment": "Moderate risk, 1:2 risk/reward"
  },
  "riskReward": {
    "potentialUpside": "10-15%",
    "potentialDownside": "5-7%",
    "riskRewardRatio": "1:2",
    "riskLevel": "Moderate"
  },
  "confidence": {
    "overallConfidence": 0.7,
    "level": "Moderate",
    "confidenceFactors": ["Strong technical momentum", "Stable macro environment"],
    "uncertaintyFactors": ["Limited fundamental data", "Market volatility"]
  },
  "missingInformation": [
    "Recent quarterly earnings",
    "Sector comparison data"
  ],
  "invalidationConditions": [
    "Price breaks below 12,200",
    "RSI drops below 40",
    "Major negative news"
  ],
  "monitoringRecommendations": [
    "Watch for volume confirmation on breakout",
    "Monitor RSI for overbought signals",
    "Track USD/IRR exchange rate"
  ],
  "contributingAgents": ["TechnicalAnalyst", "MacroAnalyst", "RiskAnalyst"],
  "dataProvidersUsed": ["TSETMC"],
  "generationDuration": "00:00:15.234"
}
```

**Status Codes:**
- `200 OK`: Strategy generated successfully
- `404 Not Found`: Asset not found
- `500 Internal Server Error`: Strategy generation failed

**Notes:**
- This is a long-running operation (may take 10-30 seconds)
- The response contains a complete StrategyReport
- Not all sections may be populated (depends on available data)
- Null sections indicate insufficient data for that analysis

---

### Get Latest Strategy

```
GET /api/strategy/{assetId}/latest
```

**Description:** Returns the most recently generated strategy for an asset.

**Path Parameters:**
- `assetId` (string, required): The asset's unique identifier

**Response:** Same as Generate Strategy response.

**Status Codes:**
- `200 OK`: Success
- `404 Not Found`: No strategy found for this asset

---

## 5. Request/Response Models

### Asset

```json
{
  "id": "string (GUID)",
  "symbol": "string",
  "name": "string",
  "market": "string",
  "assetType": "string (enum)",
  "sector": "string (nullable)",
  "isin": "string (nullable)",
  "metadata": "object (nullable)"
}
```

### AssetType Enum

```json
"Stock" | "Index" | "Currency" | "Commodity" | "Crypto" | "ETF" | "Bond" | "Other"
```

### Sentiment Enum

```json
"Bullish" | "Bearish" | "Neutral" | "Unknown"
```

### MarketRegime Enum

```json
"Uptrend" | "Downtrend" | "Sideways" | "Volatile" | "Transitional" | "Unknown"
```

### TimeHorizon Enum

```json
"ShortTerm" | "MediumTerm" | "LongTerm"
```

### EvidenceType Enum

```json
"Fact" | "Calculation" | "Interpretation" | "Scenario" | "Uncertain"
```

---

## 6. Error Handling

### Error Response Format

```json
{
  "error": {
    "code": "ASSET_NOT_FOUND",
    "message": "Asset with ID 'abc' was not found",
    "details": {}
  }
}
```

### Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `ASSET_NOT_FOUND` | 404 | Asset not found |
| `INVALID_REQUEST` | 400 | Invalid request body or parameters |
| `PROVIDER_ERROR` | 502 | External data provider failed |
| `LLM_ERROR` | 502 | LLM provider failed |
| `INTERNAL_ERROR` | 500 | Unexpected server error |

### Partial Failures

When some data providers fail but others succeed, the API returns a 200 response with:
- `dataProvidersUsed`: List of successful providers
- `failedProviders`: List of failed providers
- `errors`: List of error details

---

## 7. Examples

### cURL Examples

**Health Check:**
```bash
curl http://localhost:5000/health
```

**List Assets:**
```bash
curl http://localhost:5000/api/assets
```

**Create Asset:**
```bash
curl -X POST http://localhost:5000/api/assets \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "فولاد",
    "name": "Foolad Mobarakeh",
    "market": "TSE",
    "assetType": "Stock"
  }'
```

**Generate Strategy:**
```bash
curl -X POST http://localhost:5000/api/strategy/550e8400-e29b-41d4-a716-446655440000
```

**Generate Strategy with Parameters:**
```bash
curl -X POST "http://localhost:5000/api/strategy/550e8400-e29b-41d4-a716-446655440000?horizons=ShortTerm,MediumTerm&indicators=RSI,MACD"
```

### Python Example

```python
import requests

# Create asset
asset = requests.post("http://localhost:5000/api/assets", json={
    "symbol": "فولاد",
    "name": "Foolad Mobarakeh",
    "market": "TSE",
    "assetType": "Stock"
}).json()

# Generate strategy
strategy = requests.post(f"http://localhost:5000/api/strategy/{asset['id']}").json()

# Print executive summary
print(f"Sentiment: {strategy['executiveSummary']['overallSentiment']}")
print(f"Summary: {strategy['executiveSummary']['summary']}")
```

### JavaScript Example

```javascript
// Create asset
const asset = await fetch('http://localhost:5000/api/assets', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    symbol: 'فولاد',
    name: 'Foolad Mobarakeh',
    market: 'TSE',
    assetType: 'Stock'
  })
}).then(r => r.json());

// Generate strategy
const strategy = await fetch(`http://localhost:5000/api/strategy/${asset.id}`, {
  method: 'POST'
}).then(r => r.json());

// Print executive summary
console.log(`Sentiment: ${strategy.executiveSummary.overallSentiment}`);
console.log(`Summary: ${strategy.executiveSummary.summary}`);
```

---

## Appendix: Future Endpoints

### V2 Endpoints

```
GET  /api/assets/{id}/data          - Get market data for asset
GET  /api/assets/{id}/indicators    - Get indicator values
GET  /api/assets/{id}/news          - Get news for asset
GET  /api/economic-indicators       - Get economic indicators
GET  /api/currency-rates            - Get currency rates
GET  /api/gold-prices               - Get gold prices
```

### V3 Endpoints

```
GET  /api/strategy/{id}/backtest    - Backtest strategy
GET  /api/strategy/{id}/evaluate    - Evaluate strategy accuracy
GET  /api/strategy/history          - Get strategy history
```

### V4 Endpoints

```
POST /api/auth/login                - User authentication
GET  /api/user/preferences          - Get user preferences
PUT  /api/user/preferences          - Update user preferences
GET  /api/watchlist                 - Get asset watchlist
POST /api/watchlist                 - Add asset to watchlist
DELETE /api/watchlist/{id}          - Remove asset from watchlist
```
