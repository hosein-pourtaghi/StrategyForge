using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.InstrumentResolution;

/// <summary>
/// In-memory implementation of IInstrumentResolver.
/// Provides canonical instrument resolution from user-facing identifiers.
/// Structured so a future persistent repository can replace or extend it.
/// </summary>
public sealed class InMemoryInstrumentResolver : IInstrumentResolver
{
    private readonly ILogger<InMemoryInstrumentResolver> _logger;

    // Primary lookup: canonical InstrumentId → InstrumentMapping
    private readonly Dictionary<string, InstrumentMapping> _byId = new(StringComparer.OrdinalIgnoreCase);

    // Secondary lookups for fast resolution
    private readonly Dictionary<string, List<InstrumentMapping>> _byPersianSymbol = new();
    private readonly Dictionary<string, List<InstrumentMapping>> _byLatinSymbol = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<InstrumentMapping>> _byTsetmcInsCode = new();
    private readonly Dictionary<string, List<InstrumentMapping>> _bySourceIdentifier = new();

    public InMemoryInstrumentResolver(ILogger<InMemoryInstrumentResolver> logger)
    {
        _logger = logger;
        LoadSeedData();
    }

    public Task<InstrumentMapping?> ResolveAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return Task.FromResult<InstrumentMapping?>(null);

        var query = identifier.Trim();

        // 1. Try canonical InstrumentId
        if (_byId.TryGetValue(query, out var byId))
            return Task.FromResult<InstrumentMapping?>(byId);

        // 2. Try TSETMC InsCode (numeric)
        if (_byTsetmcInsCode.TryGetValue(query, out var byInsCode))
            return Task.FromResult(byInsCode.Count == 1 ? byInsCode[0] : null);

        // 3. Try Persian symbol (exact match)
        if (_byPersianSymbol.TryGetValue(query, out var byPersian) && byPersian.Count == 1)
            return Task.FromResult<InstrumentMapping?>(byPersian[0]);

        // 4. Try Latin symbol (case-insensitive)
        if (_byLatinSymbol.TryGetValue(query, out var byLatin) && byLatin.Count == 1)
            return Task.FromResult<InstrumentMapping?>(byLatin[0]);

        // 5. Try any source identifier
        if (_bySourceIdentifier.TryGetValue(query, out var bySource) && bySource.Count == 1)
            return Task.FromResult<InstrumentMapping?>(bySource[0]);

        return Task.FromResult<InstrumentMapping?>(null);
    }

    public Task<IReadOnlyList<InstrumentMapping>> ResolveBatchAsync(
        IReadOnlyList<string> identifiers, CancellationToken cancellationToken = default)
    {
        var results = new List<InstrumentMapping>(identifiers.Count);
        foreach (var id in identifiers)
        {
            var resolved = ResolveAsync(id, cancellationToken).GetAwaiter().GetResult();
            if (resolved != null)
                results.Add(resolved);
        }
        return Task.FromResult<IReadOnlyList<InstrumentMapping>>(results.AsReadOnly());
    }

    public Task<IReadOnlyList<InstrumentMapping>> SearchAsync(
        string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IReadOnlyList<InstrumentMapping>>(Array.Empty<InstrumentMapping>());

        var q = query.Trim();
        var allInstruments = _byId.Values.Distinct().ToList();

        var matches = allInstruments
            .Where(i =>
                i.Symbol.Contains(q, StringComparison.Ordinal) ||
                i.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (i.LatinSymbol != null && i.LatinSymbol.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                i.InstrumentId.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Symbol)
            .Take(maxResults)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<InstrumentMapping>>(matches);
    }

    public SourceIdentifier? GetSourceIdentifier(InstrumentMapping instrument, SourceAdapterType sourceType)
    {
        return instrument.SourceIdentifiers.TryGetValue(sourceType, out var id) ? id : null;
    }

    private void Register(InstrumentMapping instrument)
    {
        _byId[instrument.InstrumentId] = instrument;

        if (!_byPersianSymbol.ContainsKey(instrument.Symbol))
            _byPersianSymbol[instrument.Symbol] = new List<InstrumentMapping>();
        _byPersianSymbol[instrument.Symbol].Add(instrument);

        if (instrument.LatinSymbol != null)
        {
            if (!_byLatinSymbol.ContainsKey(instrument.LatinSymbol))
                _byLatinSymbol[instrument.LatinSymbol] = new List<InstrumentMapping>();
            _byLatinSymbol[instrument.LatinSymbol].Add(instrument);
        }

        foreach (var (sourceType, sourceId) in instrument.SourceIdentifiers)
        {
            var key = $"{sourceType}:{sourceId.Id}";
            if (!_bySourceIdentifier.ContainsKey(key))
                _bySourceIdentifier[key] = new List<InstrumentMapping>();
            _bySourceIdentifier[key].Add(instrument);

            if (sourceType == SourceAdapterType.Tsetmc)
            {
                if (!_byTsetmcInsCode.ContainsKey(sourceId.Id))
                    _byTsetmcInsCode[sourceId.Id] = new List<InstrumentMapping>();
                _byTsetmcInsCode[sourceId.Id].Add(instrument);
            }
        }
    }

    private void LoadSeedData()
    {
        var now = DateTimeOffset.UtcNow;

        Register(new InstrumentMapping
        {
            InstrumentId = "iran-equity-foolad-4439113430858354",
            Symbol = "فولاد",
            LatinSymbol = "Foolad",
            DisplayName = "Foolad Mobarakeh Steel",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
            {
                [SourceAdapterType.Tsetmc] = new() { Id = "4439113430858354", SourceSymbol = "فولاد", LastVerified = now }
            }
        });

        Register(new InstrumentMapping
        {
            InstrumentId = "iran-equity-foolad-65915385444382438",
            Symbol = "فملی",
            LatinSymbol = "Mobarakeh",
            DisplayName = "Mobarakeh Steel",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
            {
                [SourceAdapterType.Tsetmc] = new() { Id = "65915385444382438", SourceSymbol = "فملی", LastVerified = now }
            }
        });

        Register(new InstrumentMapping
        {
            InstrumentId = "iran-equity-shamsaer-36178638763352870",
            Symbol = "شپدیس",
            LatinSymbol = "Sepid",
            DisplayName = "Sepid Petrochemical",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
            {
                [SourceAdapterType.Tsetmc] = new() { Id = "36178638763352870", SourceSymbol = "شپدیس", LastVerified = now }
            }
        });

        Register(new InstrumentMapping
        {
            InstrumentId = "iran-equity-fars-10523225570658595",
            Symbol = "فارس",
            LatinSymbol = "Petrochemical",
            DisplayName = "Persian Gulf Petrochemical",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
            {
                [SourceAdapterType.Tsetmc] = new() { Id = "10523225570658595", SourceSymbol = "فارس", LastVerified = now }
            }
        });

        Register(new InstrumentMapping
        {
            InstrumentId = "iran-equity-cbpo-62772852867228498",
            Symbol = "خودرو",
            LatinSymbol = "Khodro",
            DisplayName = "Iran Khodro",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
            {
                [SourceAdapterType.Tsetmc] = new() { Id = "62772852867228498", SourceSymbol = "خودرو", LastVerified = now }
            }
        });

        Register(new InstrumentMapping
        {
            InstrumentId = "iran-index-tedpix-0",
            Symbol = "شاخص کل",
            LatinSymbol = "TEDPIX",
            DisplayName = "Tehran Exchange Dividend and Price Index",
            AssetClass = AssetType.Index,
            Exchange = "TSE",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
            {
                [SourceAdapterType.Tsetmc] = new() { Id = "32097828799088150", SourceSymbol = "شاخص کل", LastVerified = now }
            }
        });

        Register(new InstrumentMapping
        {
            InstrumentId = "iran-fx-usd-irr-free",
            Symbol = "دلار",
            LatinSymbol = "USD/IRR",
            DisplayName = "USD/IRR Free Market Rate",
            AssetClass = AssetType.Currency,
            Exchange = "free_market",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
            {
                [SourceAdapterType.Tgju] = new() { Id = "price_dollar_rl", SourceSymbol = "price_dollar_rl", LastVerified = now }
            }
        });

        Register(new InstrumentMapping
        {
            InstrumentId = "iran-fx-eur-irr-free",
            Symbol = "یورو",
            LatinSymbol = "EUR/IRR",
            DisplayName = "EUR/IRR Free Market Rate",
            AssetClass = AssetType.Currency,
            Exchange = "free_market",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
            {
                [SourceAdapterType.Tgju] = new() { Id = "price_euro", SourceSymbol = "price_euro", LastVerified = now }
            }
        });

        Register(new InstrumentMapping
        {
            InstrumentId = "iran-commodity-gold-18k",
            Symbol = "سکه",
            LatinSymbol = "Gold18K",
            DisplayName = "18K Gold (Sekkeh)",
            AssetClass = AssetType.Commodity,
            Exchange = "free_market",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
            {
                [SourceAdapterType.Tgju] = new() { Id = "price_sekee", SourceSymbol = "price_sekee", LastVerified = now }
            }
        });

        Register(new InstrumentMapping
        {
            InstrumentId = "iran-commodity-gold-mesghal",
            Symbol = "مثقال",
            LatinSymbol = "Mesghal",
            DisplayName = "Mesghal Gold",
            AssetClass = AssetType.Commodity,
            Exchange = "free_market",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
            {
                [SourceAdapterType.Tgju] = new() { Id = "price_mesghal", SourceSymbol = "price_mesghal", LastVerified = now }
            }
        });

        Register(new InstrumentMapping
        {
            InstrumentId = "iran-crypto-usdt-irr",
            Symbol = "تتر",
            LatinSymbol = "USDT/IRR",
            DisplayName = "Tether / IRR",
            AssetClass = AssetType.Crypto,
            Exchange = "free_market",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
            {
                [SourceAdapterType.Tgju] = new() { Id = "price_tether", SourceSymbol = "price_tether", LastVerified = now }
            }
        });

        _logger.LogInformation("InstrumentResolver loaded {Count} seed instruments", _byId.Count);
    }
}
