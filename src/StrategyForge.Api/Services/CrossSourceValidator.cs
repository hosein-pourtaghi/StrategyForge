using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Services;

/// <summary>
/// Optional cross-source validation. When enabled via configuration, compares
/// compatible secondary sources against the primary source for data quality assurance.
/// Never merges or averages prices — the primary source always remains canonical.
/// </summary>
public sealed class CrossSourceValidator
{
    private readonly IDataSourceRegistry _registry;
    private readonly ILogger<CrossSourceValidator> _logger;
    private readonly CrossValidationSettings _settings;

    public CrossSourceValidator(
        IDataSourceRegistry registry,
        IOptions<DataSourceSettings> settings,
        ILogger<CrossSourceValidator> logger)
    {
        _registry = registry;
        _logger = logger;
        _settings = settings.Value.CrossValidation;
    }

    /// <summary>
    /// Validates snapshot data against a secondary source if cross-validation is enabled.
    /// Returns the primary result unchanged, possibly with an added quality flag/warning.
    /// </summary>
    public DataResult<Candle> ValidateSnapshot(
        DataResult<Candle> primaryResult,
        InstrumentMapping instrument)
    {
        if (!_settings.Enabled || !primaryResult.Ok || primaryResult.Data == null)
            return primaryResult;

        if (_settings.EnabledDataTypes.Count > 0 &&
            !_settings.EnabledDataTypes.Contains("Snapshot"))
            return primaryResult;

        // Find a secondary source that supports this instrument's snapshot
        var primarySource = primaryResult.Data.Provenance?.Source;
        var compatibleAdapters = _registry.GetAdaptersForCapability(instrument, MarketDataType.Snapshot)
            .Where(a => a.SourceType != primarySource)
            .ToList();

        if (compatibleAdapters.Count == 0)
        {
            _logger.LogDebug("No secondary source available for cross-validation of {Symbol}", instrument.Symbol);
            return primaryResult;
        }

        // Try the first compatible secondary source
        var secondaryAdapter = compatibleAdapters[0];
        _logger.LogDebug(
            "Cross-validating {Symbol} snapshot: primary={Primary}, secondary={Secondary}",
            instrument.Symbol, primarySource, secondaryAdapter.SourceType);

        // Synchronous fetch for secondary is not ideal, but we can use the registry's method
        // Note: In production, this could be async. For now, we just flag it as attempted.
        _logger.LogInformation(
            "Cross-validation configured but secondary fetch not yet executed for {Symbol}. " +
            "Cross-validation will be fully implemented when async secondary fetch is added.",
            instrument.Symbol);

        return primaryResult;
    }

    /// <summary>
    /// Validates candle data against a secondary source if cross-validation is enabled.
    /// </summary>
    public DataResult<IReadOnlyList<Candle>> ValidateCandles(
        DataResult<IReadOnlyList<Candle>> primaryResult,
        InstrumentMapping instrument)
    {
        if (!_settings.Enabled || !primaryResult.Ok || primaryResult.Data == null || primaryResult.Data.Count == 0)
            return primaryResult;

        if (_settings.EnabledDataTypes.Count > 0 &&
            !_settings.EnabledDataTypes.Contains("HistoricalCandles"))
            return primaryResult;

        var primarySource = primaryResult.Data[0].Provenance?.Source;
        var compatibleAdapters = _registry.GetAdaptersForCapability(instrument, MarketDataType.HistoricalCandles)
            .Where(a => a.SourceType != primarySource)
            .ToList();

        if (compatibleAdapters.Count == 0)
        {
            _logger.LogDebug("No secondary source for cross-validation of {Symbol} candles", instrument.Symbol);
            return primaryResult;
        }

        _logger.LogInformation(
            "Cross-validation configured for {Symbol} candles from {Secondary}. " +
            "Secondary comparison pending async implementation.",
            instrument.Symbol, compatibleAdapters[0].Name);

        return primaryResult;
    }

    /// <summary>
    /// Whether cross-validation is enabled for the given data type.
    /// </summary>
    public bool IsEnabledFor(MarketDataType dataType)
    {
        if (!_settings.Enabled) return false;
        if (_settings.EnabledDataTypes.Count == 0) return true;
        return _settings.EnabledDataTypes.Contains(dataType.ToString());
    }
}
