using Microsoft.AspNetCore.Mvc;
using StrategyForge.Api.Contracts;
using StrategyForge.Api.Services;

namespace StrategyForge.Api.Controllers;

/// <summary>
/// API for querying available data sources and their capabilities.
/// Returns public metadata about registered source adapters.
/// </summary>
[ApiController]
[Route("api/data-sources")]
[Produces("application/json")]
public class DataSourcesController : ControllerBase
{
    private readonly DataSourceService _service;

    public DataSourcesController(DataSourceService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get all registered data sources with their capabilities and health status.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of available data sources with metadata.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DataSourceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var sources = await _service.GetSourcesAsync(ct);
        return Ok(sources);
    }
}
