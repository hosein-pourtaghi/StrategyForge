using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis;

/// <summary>
/// Orchestrates computation of all registered indicators.
/// Maintains a registry and runs them against candle data.
/// </summary>
public sealed class IndicatorEngine : IIndicatorEngine
{
    private readonly IReadOnlyList<IIndicator> _indicators;

    public IndicatorEngine(IEnumerable<IIndicator> indicators)
    {
        _indicators = indicators.ToList().AsReadOnly();
    }

    public IReadOnlyList<IIndicator> RegisteredIndicators => _indicators;

    public IndicatorEngineResult ComputeAll(
        IReadOnlyList<Candle> candles,
        IndicatorConfiguration? configuration = null)
    {
        if (candles.Count == 0)
        {
            return new IndicatorEngineResult
            {
                DataStartDate = DateOnly.MinValue,
                DataEndDate = DateOnly.MinValue,
                CandleCount = 0
            };
        }

        var enabledIndicators = GetEnabledIndicators(configuration);
        var results = new Dictionary<string, IReadOnlyList<IndicatorResult>>();
        var successful = new List<string>();
        var failed = new List<string>();
        var errors = new List<IndicatorError>();

        foreach (var indicator in enabledIndicators)
        {
            try
            {
                var parameters = configuration?.IndicatorParameters
                    ?.GetValueOrDefault(indicator.Name);

                var indicatorResults = indicator.Compute(candles, parameters);

                if (indicatorResults.Count > 0)
                {
                    results[indicator.Name] = indicatorResults;
                    successful.Add(indicator.Name);
                }
            }
            catch (Exception ex)
            {
                failed.Add(indicator.Name);
                errors.Add(new IndicatorError
                {
                    IndicatorName = indicator.Name,
                    ErrorMessage = ex.Message,
                    ExceptionMessage = ex.InnerException?.Message
                });
            }
        }

        return new IndicatorEngineResult
        {
            DataStartDate = candles[0].Date,
            DataEndDate = candles[^1].Date,
            CandleCount = candles.Count,
            Results = results,
            SuccessfulIndicators = successful,
            FailedIndicators = failed,
            Errors = errors
        };
    }

    private IReadOnlyList<IIndicator> GetEnabledIndicators(IndicatorConfiguration? configuration)
    {
        if (configuration is null)
            return _indicators;

        var indicators = _indicators.AsEnumerable();

        if (configuration.EnabledIndicators is { Count: > 0 })
        {
            indicators = indicators.Where(i =>
                configuration.EnabledIndicators.Contains(i.Name));
        }

        if (configuration.DisabledIndicators is { Count: > 0 })
        {
            indicators = indicators.Where(i =>
                !configuration.DisabledIndicators.Contains(i.Name));
        }

        return indicators.ToList().AsReadOnly();
    }
}
