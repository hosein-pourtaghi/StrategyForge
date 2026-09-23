using Microsoft.AspNetCore.Mvc;
using StrategyForge.Api.Contracts;
using StrategyForge.Api.Services;
using StrategyForge.Domain.Enums;

namespace StrategyForge.Api.Controllers;

/// <summary>
/// API for the processed (enriched) historical dataset: minimal read query
/// (instrument/source/range with pagination) and a processing trigger.
/// No charting, no dashboards — the processing pipeline's read surface only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HistoricalProcessingController : ControllerBase
{
    private readonly HistoricalProcessingApiService _service;

    public HistoricalProcessingController(HistoricalProcessingApiService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get processed (validated + indicator-enriched) historical observations.
    /// Results are ordered chronologically by observation date.
    /// </summary>
    /// <param name="instrument">Canonical instrument ID.</param>
    /// <param name="source">Provider source (optional; sources are never merged).</param>
    /// <param name="from">Start date (inclusive, Gregorian).</param>
    /// <param name="to">End date (inclusive, Gregorian).</param>
    /// <param name="skip">Rows to skip (pagination).</param>
    /// <param name="take">Rows to return (pagination; max 1000).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Processed observations returned.</response>
    /// <response code="400">Invalid request parameters.</response>
    [HttpGet("observations")]
    [ProducesResponseType(typeof(EnrichedObservationsPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetObservations(
        [FromQuery] string? instrument,
        [FromQuery] SourceAdapterType? source,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(instrument))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "Instrument parameter is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Date Range",
                Detail = "'from' date must not be after 'to' date.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 1000);

        var page = await _service.GetEnrichedObservationsAsync(
            instrument.Trim(), source, from, to, skip, take, ct);

        return Ok(page);
    }

    /// <summary>
    /// Trigger a processing run: reads the stored historical dataset for the requested
    /// instrument/source/range, validates it, and persists indicator-enriched derived
    /// observations. Idempotent — re-running updates existing derived rows in place.
    /// </summary>
    /// <param name="instrument">Instrument query (canonical ID or resolvable symbol).</param>
    /// <param name="source">Provider source to process (required; never merged).</param>
    /// <param name="from">Start of the output range (inclusive, Gregorian).</param>
    /// <param name="to">End of the output range (inclusive, Gregorian).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Processing completed (check Ok and accounting in the body).</response>
    /// <response code="400">Invalid request parameters.</response>
    /// <response code="404">Instrument not found.</response>
    [HttpPost("process")]
    [ProducesResponseType(typeof(HistoricalProcessingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Process(
        [FromQuery] string? instrument,
        [FromQuery] SourceAdapterType source,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(instrument))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "Instrument parameter is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var fromDate = from ?? DateOnly.FromDateTime(DateTime.Today.AddYears(-1));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.Today);

        if (fromDate > toDate)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Date Range",
                Detail = "'from' date must not be after 'to' date.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var (response, error) = await _service.ProcessAsync(
            instrument.Trim(), source, fromDate, toDate, ct);

        if (response is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Instrument Not Found",
                Detail = error,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(response);
    }
}
