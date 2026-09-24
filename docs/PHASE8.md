# Phase 8 — Deterministic Strategy & Market Regime Layer

This document describes the **implemented** Phase 8 layer: a deterministic,
evidence-driven strategy and market-regime component that runs on top of the
Phase 7 historical-data pipeline. Everything here is deterministic
(same observations + same configuration ⇒ same output), contains **no LLM
calls**, produces **no trading instructions**, and **never fabricates missing
market data**.

---

## 1. Position in the architecture

```text
Phase 6  ingestion      → HistoricalDataset store (raw, provenance-preserving)
Phase 7  processing     → EnrichedObservation store (validated + indicators)
Phase 8  THIS LAYER     → regimes, conditions, setups (pure functions of the above)
Phase 9  preparation    → AI-ready JSONL datasets (consumes Phase 8 features)
```

Phase 8 reads stored indicator values only. It never recomputes indicators,
never touches providers/HTTP, never reads or writes raw market data, and
writes nothing to the database.

## 2. Market regime classifier

`MarketRegimeClassifier` (Analysis layer) classifies each observation along
three independent, deterministic dimensions:

**Trend** — from `PriceAboveSma` and `EmaFastAboveSlow` (the stored MACD line,
which equals EMA(fast) − EMA(slow)):

| Condition | Result |
|---|---|
| both true | `Bullish` |
| both false | `Bearish` |
| signals disagree | `Sideways` |
| either input missing | `Unknown` |

**Volatility** — Bollinger bandwidth percent `(Upper − Lower) / Middle × 100`
(derived arithmetically from stored components):

| Condition | Result |
|---|---|
| `bw% < LowVolatilityBandwidthPercent` (default 1.5) | `Low` |
| `bw% > HighVolatilityBandwidthPercent` (default 5.0) | `High` |
| otherwise | `Normal` |
| Bollinger unavailable (or middle ≤ 0) | `Unknown` |

**Momentum** — RSI bands (defaults: oversold 30, weak-upper 45, strong-lower 55,
overbought 70):

| Condition | Result |
|---|---|
| `RSI < 30` | `Oversold` |
| `30 ≤ RSI < 45` | `Weak` |
| `45 ≤ RSI ≤ 55` | `Neutral` |
| `55 < RSI < 70` | `Strong` |
| `RSI ≥ 70` | `Overbought` |
| RSI missing | `Unknown` |

All thresholds live in `StrategyThresholds` (Domain) with documented defaults
described as *exploration starting points, not universal financial definitions*.
Boundary semantics (inclusive/exclusive) are covered by tests, including
exact-threshold cases.

## 3. Deterministic conditions (`MarketFeatures`)

`MarketFeatureExtractor` projects one enriched observation (+ its predecessor,
for rising/falling comparisons) into `MarketFeatures`:

- `PriceAboveSma` — `Close > SMA`; null when SMA missing
- `EmaFastAboveSlow` — `MACD > 0`; null when MACD line missing
- `MacdAboveSignal` — `MACD > Signal`; null when either missing
- `HistogramRising` — histogram greater than previous observation's; null when either missing
- `BandwidthPercent` — dimensionless volatility; null when components missing or middle ≤ 0
- `PercentB` — stored directly from the Bollinger component
- `HasMeaningfulVolume` — `Volume > 0` (Phase 3/7 semantics: zero volume is
  "not supplied / no trades", never treated as real volume)

Missing indicator components yield **null** — values are never defaulted to
zero or fabricated. (A specific bug where a missing `Histogram` component was
read as `0m` was found and fixed during this phase; see §9.)

## 4. Setup model (Domain: `StrategySetup.cs`)

A **setup** is the deterministic output of a setup rule: structured evidence,
not advice.

| Field | Meaning |
|---|---|
| `SetupId` | `instrumentId|source|ruleName|yyyy-MM-dd` — stable, content-derived identity |
| `RuleName` | which setup rule produced it |
| `InstrumentId` / `Source` / `ObservationDate` | Phase 7 dataset identity (traceability) |
| `Close` | underlying observation's close (snapshot) |
| `Direction` | `Long` / `Short` / `Neutral` — analytical bias vocabulary only |
| `Regime` | the 3-dimensional regime at the observation |
| `EntryCondition` | deterministic definition of what qualified |
| `SupportingEvidence` | actual stored values behind every condition |
| `Invalidation` | measurable condition + feature names + optional reference level that would remove the premise on later observations |
| `Risk` | RSI, %B, bandwidth%, close-vs-SMA% (actual or null), volatility state |
| `UnavailableEvidence` | always empty in emitted setups (engine never emits with missing evidence) |
| `ProcessedBy` | Phase 7 pipeline version of the underlying enriched observation |

The invalidation is a **definition to be evaluated by the consumer on later
observations**; the setup layer never simulates the future.

## 5. Setup rules (Analysis: `SetupRules.cs`)

Two rules ship with the phase (deliberately few, strongly tested). Each
explicitly defines required inputs, regime compatibility, entry, exclusions,
and invalidation. Rules return `SetupRuleEvaluation` (Domain `ISetupRule.cs`);
the engine adds identity/provenance/risk — rules never see dataset identity.

### TrendContinuation (Long)

- **Regime gate:** `Trend = Bullish` AND momentum ∈ {Neutral, Strong, Overbought}
- **Entry:** `Close > SMA` AND `MACD > Signal` AND `MACD > 0` AND `RSI ≥ TrendFollowingRsiMinimum` (default 50, inclusive)
- **Exclusion:** MACD histogram explicitly **falling** (a *missing* histogram does not exclude)
- **Invalidation:** MACD line crosses below signal (reference: signal at setup time)
- **Missing inputs:** SMA, MACD, MACD.Signal, RSI → no setup, missing items listed

### MeanReversionPullback (Long)

- **Regime gate:** `Trend ∈ {Sideways, Bullish}` AND volatility ≠ Unknown (never fade a confirmed Bearish trend)
- **Entry:** `%B ≤ MeanReversionMaxPercentB` (default 0.2, inclusive) AND `RSI ≤ MeanReversionMaxRsi` (default 40, inclusive)
- **Invalidation:** close below the setup observation's lower Bollinger band (reference: lower band value)
- **Missing inputs:** BollingerBands.PercentB, RSI → no setup, missing items listed

`SetupRuleRegistry.BuiltIn` holds them in stable order and is separate from the
existing `StrategyRuleRegistry` (which backs the historical evaluation engine
and its pinned tests).

## 6. Setup generation engine (Analysis: `StrategySetupEngine`)

`Generate(observations, instrumentId, source, ruleNames?, thresholds?)`:

- iterates observations in chronological order, extracting features with the
  predecessor available (deterministic)
- evaluates every selected rule per observation; unknown rule names throw with
  the known-name list
- materializes setups with stable identity, `ProcessedBy` provenance from the
  underlying observation, and risk metadata from actual stored values
- accounting: `ObservationsEvaluated`, `Setups` (ordered by date then rule name,
  ordinal), `SetupsPerRule`, `SkippedInsufficientEvidence` (per-rule count of
  observations skipped for missing required evidence — never silent)
- hard guarantees: no clocks, no randomness, no LLM calls; one instrument and
  one source at a time (sources are never merged)

## 7. API surface

`POST /api/Strategy/setups` (`StrategyController.GenerateSetups`) — request:
`instrument`, `source` (required), `from`/`to` (Gregorian, inclusive),
optional `ruleNames`. Response (`StrategySetupsResponse`): structured setups
with full evidence, `SetupsPerRule`, `SkippedInsufficientEvidence`, and
`NO_DATA` / `INSTRUMENT_NOT_FOUND` / `INVALID_REQUEST` error codes.
The endpoint is provider-neutral and returns structured results, never prose.

## 8. Determinism guarantees

- Rule evaluation, regime classification, and feature extraction are pure
  functions of (observations, thresholds).
- `SetupId` is content-derived; identical input yields identical identities.
- Output ordering is (observation date, rule name ordinal) — stable.
- Engine-level determinism is proven by a test that serializes the full setup
  list from two independent runs and asserts byte-identity (record equality on
  `IReadOnlyList` members would only compare references).
- The only non-deterministic artifacts allowed are provenance timestamps
  carried *in from* Phase 7 data (`ProcessedBy` is a version string; no wall
  clock is read anywhere in Phase 8 code).

## 9. Missing-data behavior

- Missing required evidence ⇒ **no setup**, with the missing items recorded in
  the evaluation and counted in `SkippedInsufficientEvidence`. Unknown is never
  silently converted to false.
- Missing optional context (e.g., no predecessor for histogram comparison)
  yields null features; the TrendContinuation exclusion applies only when the
  histogram evidence exists and is explicitly falling.
- Risk metadata fields are null when their inputs were unavailable.
- Fixed during this phase: `MarketFeatureExtractor` read a missing MACD
  `Histogram` (or `Signal`) component as `0m` via `GetValueOrDefault`; it now
  distinguishes "absent" (null) from "provider reported 0". Existing Phase 8
  tests were unaffected (they always stored full components); new tests cover
  the distinction.

## 10. Configuration & thresholds

`StrategyThresholds` (Domain record) is the single configuration point; all
values are explicit and documented. Defaults: RSI 30/45/55/70, volatility
bandwidth 1.5%/5.0%, TrendFollowingRsiMinimum 50, MeanReversionMaxPercentB 0.2,
MeanReversionMaxRsi 40. No hidden constants exist in classifier or rules.

## 11. Persistence

None added — deliberately. Setups are a cheap pure function of already-persisted
enriched observations plus thresholds, so persisting them would duplicate data
without adding information (spec §9: persist only when genuinely required).
Re-running `POST /api/Strategy/setups` or the CLI `--setups` mode recomputes
them deterministically. Raw historical data is never mutated.

## 12. Verification performed

- Solution build: 0 errors (16 pre-existing warnings, none from Phase 8 files).
- Full test suite: 876/876 passing (up from 843; +33 Phase 8 tests across
  Analysis and Api test projects).
- Real-dataset verification (PostgreSQL-backed Phase 7 data, TGJU USD/IRR,
  2024-01-01..2026-09-22, 89 enriched observations):
  - 15 TrendContinuation setups generated with populated regime/risk values
  - 33 early observations skipped for insufficient evidence (short history),
    19 observations skipped for MeanReversionPullback (no Bollinger context)
  - determinism re-run: IDENTICAL
  - setups exported to `dataset-output/setups.json`
- Limitation: the dataset's ~89-observation history means regime/signal
  coverage starts mid-series; longer histories will produce earlier signals.

## 13. Non-goals respected

No LLM involvement, no trading instructions or order/execution semantics, no
portfolio logic, no new infrastructure (Redis/Kafka/queues), no new indicators,
no fabricated data, no persistence tables, no provider-coupled identifiers.
