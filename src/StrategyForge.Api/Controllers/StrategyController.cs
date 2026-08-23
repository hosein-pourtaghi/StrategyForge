using Microsoft.AspNetCore.Mvc;
using StrategyForge.Api.Contracts;
using StrategyForge.Api.Services;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Orchestration;

namespace StrategyForge.Api.Controllers;

/// <summary>
/// API for strategy generation and synthesis.
/// Generates structured, evidence-driven investment strategy proposals.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StrategyController : ControllerBase
{
    private readonly IStrategyOrchestrator _orchestrator;
    private readonly InstrumentService _instrumentService;

    public StrategyController(
        IStrategyOrchestrator orchestrator,
        InstrumentService instrumentService)
    {
        _orchestrator = orchestrator;
        _instrumentService = instrumentService;
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
}
