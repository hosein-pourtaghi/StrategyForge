using Microsoft.AspNetCore.Mvc;
using StrategyForge.Api.Contracts;
using StrategyForge.Api.Services;
using StrategyForge.Domain.Enums;

namespace StrategyForge.Api.Controllers;

/// <summary>
/// API for market data acquisition.
/// Returns normalized candle data, snapshots, and evidence metadata.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MarketDataController : ControllerBase
{
    private readonly MarketDataService _service;

    public MarketDataController(MarketDataService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get historical OHLCV candle data for an instrument.
    /// </summary>
    /// <param name="instrument">Instrument query (Persian symbol, Latin symbol, numeric ID, or canonical ID).</param>
    /// <param name="from">Start date (inclusive, Gregorian).</param>
    /// <param name="to">End date (inclusive, Gregorian).</param>
    /// <param name="source">Preferred data source adapter type (optional). When specified, only this source is used (no fallback).</param>
    /// <param name="resolution">Candle resolution/interval (optional). Defaults to Daily. Supported: Minute1, Minute5, Minute15, Minute30, Hour1, Hour4, Daily, Weekly, Monthly. Sub-daily only available from Nobitex.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Historical candle data with provenance, freshness, and quality metadata.</returns>
    /// <response code="200">Candle data returned successfully.</response>
    /// <response code="400">Invalid request parameters.</response>
    /// <response code="404">Instrument not found.</response>
    [HttpGet("candles")]
    [ProducesResponseType(typeof(DataResultResponse<IReadOnlyList<CandleResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCandles(
        [FromQuery] string? instrument,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] SourceAdapterType? source,
        [FromQuery] CandleResolution? resolution,
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

                var selectionMode = source.HasValue ? SourceSelectionMode.PreferredOnly : SourceSelectionMode.BestAvailable;
        var result = await _service.GetCandlesAsync(instrument.Trim(), fromDate, toDate, source, selectionMode, resolution, ct);

        if (!result.Ok && result.Error?.Code == "INSTRUMENT_NOT_FOUND")
        {
            return NotFound(new ProblemDetails
            {
                Title = "Instrument Not Found",
                Detail = result.Error.Message,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get the latest market snapshot for an instrument.
    /// </summary>
    /// <param name="instrument">Instrument query (Persian symbol, Latin symbol, numeric ID, or canonical ID).</param>
    /// <param name="source">Preferred data source adapter type (optional). When specified, only this source is used (no fallback).</param>
    /// <param name="resolution">Candle resolution/interval (optional). Defaults to Daily. Supported: Minute1, Minute5, Minute15, Minute30, Hour1, Hour4, Daily, Weekly, Monthly. Sub-daily only available from Nobitex.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Latest candle/snapshot with provenance, freshness, and quality metadata.</returns>
    [HttpGet("snapshot")]
    [ProducesResponseType(typeof(DataResultResponse<CandleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSnapshot(
        [FromQuery] string? instrument,
        [FromQuery] SourceAdapterType? source,
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

        var result = await _service.GetSnapshotAsync(instrument.Trim(), source, SourceSelectionMode.BestAvailable, ct);

        if (!result.Ok && result.Error?.Code == "INSTRUMENT_NOT_FOUND")
        {
            return NotFound(new ProblemDetails
            {
                Title = "Instrument Not Found",
                Detail = result.Error.Message,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result);
    }


    /// <summary>
    /// Get order book (depth-of-market) data for an instrument.
    /// </summary>
    /// <param name="instrument">Instrument query (Persian symbol, Latin symbol, numeric ID, or canonical ID).</param>
    /// <param name="source">Preferred data source adapter type (optional). When specified, only this source is used (no fallback).</param>
    /// <param name="resolution">Candle resolution/interval (optional). Defaults to Daily. Supported: Minute1, Minute5, Minute15, Minute30, Hour1, Hour4, Daily, Weekly, Monthly. Sub-daily only available from Nobitex.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Order book with bid/ask levels, provenance, and quality metadata.</returns>
    [HttpGet("order-book")]
    [ProducesResponseType(typeof(DataResultResponse<OrderBookResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderBook(
        [FromQuery] string? instrument,
        [FromQuery] SourceAdapterType? source,
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

        var result = await _service.GetOrderBookAsync(instrument.Trim(), source, SourceSelectionMode.BestAvailable, ct);

        if (!result.Ok && result.Error?.Code == "INSTRUMENT_NOT_FOUND")
        {
            return NotFound(new ProblemDetails
            {
                Title = "Instrument Not Found",
                Detail = result.Error.Message,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result);
    }
}
