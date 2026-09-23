using Microsoft.AspNetCore.Mvc;
using StrategyForge.Api.Contracts;
using StrategyForge.Api.Services;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Orchestration;

namespace StrategyForge.Api.Controllers;

/// <summary>
/// API for strategy generation and synthesis.
/// Generates structured, evidence-driven investment strategy proposals.
/// Also exposes the Phase 8 deterministic strategy layer: market regime
/// classification, historical rule evaluation, and chronological splits.
/// These endpoints are strictly analysis/research — they never produce
/// buy/sell instructions, orders, or execution targets.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StrategyController : ControllerBase
{
    private readonly IStrategyOrchestrator _orchestrator;
    private readonly InstrumentService _instrumentService;
    private readonly StrategyAnalysisApiService _strategyAnalysisService;

    public StrategyController(
        IStrategyOrchestrator orchestrator,
        InstrumentService instrumentService,
        StrategyAnalysisApiService strategyAnalysisService)
    {
        _orchestrator = orchestrator;
        _instrumentService = instrumentService;
        _strategyAnalysisService = strategyAnalysisService;
    }

    /// <summary>
    /// Generate a complete investment strategy for an instrument.
    /// Runs the full pipeline: data collection → indicator analysis → agent analysis → strategy synthesis.
    /// </summary>
    /// <param name="request">Strategy generation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A structured StrategyReport with evidence traceability.</returns>
    /// <response code="200">Strategy generated successfully.</response>
    /// <response code="400">Invalid request parameters.</response>
    /// <response code="404">Instrument not found.</response>
    /// <response code="500">Strategy generation failed.</response>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(StrategyResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateStrategy(
        [FromBody] StrategyRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Instrument))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "Instrument parameter is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Resolve the instrument
        var instrument = await _instrumentService.ResolveAsync(request.Instrument.Trim(), ct);
        if (instrument == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Instrument Not Found",
                Detail = $"No instrument found matching '{request.Instrument}'.",
                Status = StatusCodes.Status404NotFound
            });
        }

        // Build the Asset from the resolved instrument
        var asset = new Domain.Models.Asset
        {
            Symbol = instrument.Symbol,
            Name = instrument.DisplayName,
            Market = instrument.Exchange,
            AssetType = instrument.AssetClass
        };

        try
        {
            var report = await _orchestrator.GenerateStrategyAsync(asset, ct);

            return Ok(new StrategyResultResponse
            {
                Ok = true,
                Data = StrategyReportResponse.FromDomain(report),
                Metadata = new StrategyMetadataResponse
                {
                    LlmModel = report.LlmModel,
                    TokensUsed = report.TotalTokensUsed ?? 0,
                    Duration = report.GenerationDuration,
                    PipelineState = report.PipelineState.ToString(),
                    ExecutionId = report.Diagnostics?.ExecutionId,
                    SuccessfulAgents = report.Diagnostics?.SuccessfulAgentCount ?? 0,
                    FailedAgents = report.Diagnostics?.FailedAgentCount ?? 0,
                    Warnings = report.Diagnostics?.Warnings ?? []
                }
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status408RequestTimeout, new ProblemDetails
            {
                Title = "Request Timeout",
                Detail = "Strategy generation was cancelled or timed out.",
                Status = StatusCodes.Status408RequestTimeout
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new StrategyResultResponse
            {
                Ok = false,
                Error = new StrategyErrorResponse
                {
                    Code = "SYNTHESIS_FAILED",
                    Message = "Strategy generation failed due to an internal error.",
                    Retryable = true
                }
            });
        }
    }

    // =====================================================================
    // Phase 8 — Deterministic strategy layer (no AI, no trading decisions)
    // =====================================================================

    /// <summary>
    /// Classifies the deterministic market regime (trend / volatility / momentum)
    /// for an instrument and source over the enriched historical dataset.
    /// Regimes DESCRIBE the market — they are evidence, never trading decisions.
    /// </summary>
    /// <param name="instrument">Instrument query (canonical ID or resolvable symbol).</param>
    /// <param name="source">Provider source (required; sources are never merged).</param>
    /// <param name="from">Start date (inclusive, Gregorian). Defaults to one year ago.</param>
    /// <param name="to">End date (inclusive, Gregorian). Defaults to today.</param>
    /// <param name="take">Maximum regime snapshots returned (most recent first), 1–100. Default 30.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Regime classification returned (check Ok in the body).</response>
    [HttpGet("regime")]
    [ProducesResponseType(typeof(RegimeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegime(
        [FromQuery] string? instrument,
        [FromQuery] SourceAdapterType source,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int take = 30,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);

        var response = await _strategyAnalysisService.GetRegimeAsync(
            new RegimeQuery
            {
                Instrument = instrument,
                Source = source,
                From = from,
                To = to,
                Take = take
            },
            ct);

        return Ok(response);
    }

    /// <summary>
    /// Evaluates deterministic strategy rules over the enriched historical dataset
    /// and returns forward-return statistics plus structured evidence for matched
    /// observations. Look-ahead-safe: a rule at date D uses only information
    /// available at D; future observations are used solely to measure outcomes.
    /// These statistics describe historical behavior under the chosen evaluation
    /// methodology — they do NOT prove profitability or predictive accuracy.
    /// </summary>
    /// <param name="request">Evaluation request (instrument, source, range, rules, horizons).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Evaluation completed (check Ok in the body).</response>
    /// <response code="400">Invalid request parameters.</response>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(StrategyEvaluateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Evaluate(
        [FromBody] StrategyEvaluateRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Instrument))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "Instrument parameter is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!request.Source.HasValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "Source parameter is required (sources are never merged).",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var response = await _strategyAnalysisService.EvaluateAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Computes the chronological Research / Validation / Holdout split of the
    /// enriched observations. Splitting is strictly positional (past → future);
    /// time-series data is never shuffled.
    /// </summary>
    /// <param name="instrument">Instrument query (canonical ID or resolvable symbol).</param>
    /// <param name="source">Provider source (required; sources are never merged).</param>
    /// <param name="from">Start date (inclusive, Gregorian).</param>
    /// <param name="to">End date (inclusive, Gregorian).</param>
    /// <param name="researchFraction">Research fraction (0–1). Default 0.6.</param>
    /// <param name="validationFraction">Validation fraction (0–1). Default 0.2; remainder is holdout.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Split returned (check Ok in the body).</response>
    [HttpGet("splits")]
    [ProducesResponseType(typeof(SplitResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSplits(
        [FromQuery] string? instrument,
        [FromQuery] SourceAdapterType source,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] decimal? researchFraction,
        [FromQuery] decimal? validationFraction,
        CancellationToken ct = default)
    {
        var response = await _strategyAnalysisService.SplitAsync(
            new SplitQuery
            {
                Instrument = instrument,
                Source = source,
                From = from,
                To = to,
                ResearchFraction = researchFraction,
                ValidationFraction = validationFraction
            },
            ct);

        return Ok(response);
    }
}
