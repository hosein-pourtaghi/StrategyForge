namespace StrategyForge.Domain.Enums;

/// <summary>
/// Known source adapter types supported by StrategyForge.
/// Each adapter type corresponds to an external data provider.
/// </summary>
public enum SourceAdapterType
{
    /// <summary>Tehran Stock Exchange / TSETMC.</summary>
    Tsetmc,

    /// <summary>TGJU — free-market FX and gold rates.</summary>
    Tgju,

    /// <summary>Rahavard 365 — equity market data.</summary>
    Rahavard365,

    /// <summary>Central Bank of Iran — official FX rates.</summary>
    Cbi,

    /// <summary>Bonbast — alternative FX/gold snapshots.</summary>
    Bonbast,

    /// <summary>Servat Mandi — futures data.</summary>
    ServatMandi,

    /// <summary>TSE Web Gateway — TSETMC instrument info and order book.</summary>
    TseWebGateway,

    /// <summary>BRSAPI — third-party TSETMC data wrapper with API key auth.</summary>
    BrsApi,

    /// <summary>Nobitex — USDT/IRR crypto exchange.</summary>
    Nobitex,

    /// <summary>Wallex — USDT/IRR exchange.</summary>
    Wallex,

    /// <summary>A custom or third-party adapter.</summary>
    Custom
}
