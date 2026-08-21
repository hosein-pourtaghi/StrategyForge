using Microsoft.AspNetCore.Mvc;
using StrategyForge.Api.Contracts;
using StrategyForge.Api.Services;

namespace StrategyForge.Api.Controllers;

/// <summary>
/// API for instrument resolution and canonical identity lookup.
/// Resolves user-facing identifiers (Persian symbols, Latin names, numeric IDs)
/// to canonical StrategyForge instrument mappings.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InstrumentsController : ControllerBase
{
    private readonly InstrumentService _service;

    public InstrumentsController(InstrumentService service)
    {
        _service = service;
    }

    /// <summary>
    /// Resolve a user-provided query to a canonical instrument.
    /// </summary>
    /// <param name="query">Persian symbol, Latin symbol, numeric ID, or canonical instrument ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The canonical instrument mapping, or 404 if not found.</returns>
    /// <response code="200">Instrument resolved successfully.</response>
    /// <response code="400">Empty or invalid query.</response>
    /// <response code="404">No instrument found matching the query.</response>
    [HttpGet("resolve")]
    [ProducesResponseType(typeof(InstrumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(
        [FromQuery] string? query,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Query",
                Detail = "Query parameter is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _service.ResolveAsync(query.Trim(), ct);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Instrument Not Found",
                Detail = $"No instrument found matching '{query}'.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Search for instruments matching a partial query.
    /// </summary>
    /// <param name="query">Partial symbol or name to search for.</param>
    /// <param name="maxResults">Maximum number of results (default 10).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Matching instruments ordered by relevance.</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<InstrumentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] int maxResults = 10,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Ok(Array.Empty<InstrumentResponse>());

        var results = await _service.SearchAsync(query.Trim(), maxResults, ct);
        return Ok(results);
    }

    /// <summary>
    /// Get a canonical instrument by its StrategyForge instrument ID.
    /// </summary>
    /// <param name="instrumentId">The canonical StrategyForge instrument ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The instrument mapping, or 404 if not found.</returns>
    [HttpGet("{instrumentId}")]
    [ProducesResponseType(typeof(InstrumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        string instrumentId,
        CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(instrumentId, ct);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Instrument Not Found",
                Detail = $"No instrument found with ID '{instrumentId}'.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result);
    }
}
