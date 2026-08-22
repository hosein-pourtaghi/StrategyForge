using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Authentication;
using StrategyForge.Infrastructure.DataAdapters;
using StrategyForge.Infrastructure.Services;

namespace StrategyForge.Infrastructure.Tests.Adapters;

/// <summary>
/// Mock HttpMessageHandler for testing adapter HTTP requests without real network calls.
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
    public List<HttpRequestMessage> Requests { get; } = [];

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return await _handler(request, cancellationToken);
    }
}

/// <summary>
/// Test helpers for creating adapters with mocked dependencies.
/// </summary>
public static class TestInfrastructure
{
    public static DataSourceSettings CreateDefaultSettings()
    {
        return new DataSourceSettings
        {
            HttpTimeoutSeconds = 10,
            RetryAttempts = 0, // No retries for fast tests
            RetryBaseDelayMs = 10,
            RetryMaxDelayMs = 100,
            UserAgent = "StrategyForge-Test/1.0",
            DefaultRateLimit = new RateLimitSettings { MaxRequests = 1000, Window = TimeSpan.FromSeconds(1) },
            Sources = new Dictionary<string, SourceAdapterConfig>
            {
                ["tsetmc"] = new()
                {
                    Name = "TSETMC",
                    SourceType = SourceAdapterType.Tsetmc,
                    Enabled = true,
                    BaseUrl = "https://cdn.tsetmc.com",
                    CacheMinutes = 5,
                    MaxRetries = 0,
                    Authentication = new AuthenticationSettings { Mode = AuthenticationMode.None }
                },
                ["tgju"] = new()
                {
                    Name = "TGJU",
                    SourceType = SourceAdapterType.Tgju,
                    Enabled = true,
                    BaseUrl = "https://tgju.org",
                    CacheMinutes = 5,
                    MaxRetries = 0,
                    Authentication = new AuthenticationSettings { Mode = AuthenticationMode.None }
                },
                ["cbi"] = new()
                {
                    Name = "CentralBankOfIran",
                    SourceType = SourceAdapterType.Cbi,
                    Enabled = true,
                    BaseUrl = "https://cbi.ir",
                    CacheMinutes = 5,
                    MaxRetries = 0,
                    Authentication = new AuthenticationSettings { Mode = AuthenticationMode.None }
                },
                ["tsewebgateway"] = new()
                {
                    Name = "TSEWebGateway",
                    SourceType = SourceAdapterType.TseWebGateway,
                    Enabled = true,
                    BaseUrl = "https://cdn.tsetmc.com",
                    CacheMinutes = 5,
                    MaxRetries = 0,
                    Authentication = new AuthenticationSettings { Mode = AuthenticationMode.None }
                },
                ["brsapi"] = new()
                {
                    Name = "BRSAPI",
                    SourceType = SourceAdapterType.BrsApi,
                    Enabled = true,
                    BaseUrl = "https://Api.BrsApi.ir",
                    CacheMinutes = 5,
                    MaxRetries = 0,
                    Authentication = new AuthenticationSettings
                    {
                        Mode = AuthenticationMode.ApiKey,
                        CredentialReference = "TestBrsApiKey"
                    }
                },
                ["nobitex"] = new()
                {
                    Name = "Nobitex",
                    SourceType = SourceAdapterType.Nobitex,
                    Enabled = true,
                    BaseUrl = "https://apiv2.nobitex.ir",
                    CacheMinutes = 5,
                    MaxRetries = 0,
                    Authentication = new AuthenticationSettings { Mode = AuthenticationMode.None }
                }
            }
        };
    }

    public static RateLimiter CreateTestRateLimiter()
    {
        var settings = Options.Create(new DataSourceSettings
        {
            DefaultRateLimit = new RateLimitSettings { MaxRequests = 1000, Window = TimeSpan.FromSeconds(1) }
        });
        return new RateLimiter(settings);
    }

    public static IDataSourceAuthenticator CreateNoopAuthenticator()
    {
        var mock = new Mock<IDataSourceAuthenticator>();
        mock.Setup(a => a.AuthenticateAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<AuthenticationSettings>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthenticationResult.Succeeded(AuthenticationMode.None));
        return mock.Object;
    }

    public static TsetmcAdapter CreateTsetmcAdapter(
        MockHttpMessageHandler httpHandler,
        DataSourceSettings? settings = null,
        IDataSourceAuthenticator? authenticator = null)
    {
        settings ??= CreateDefaultSettings();
        authenticator ??= CreateNoopAuthenticator();

        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://cdn.tsetmc.com")
        };

        return new TsetmcAdapter(
            httpClient,
            Options.Create(settings),
            Mock.Of<ILogger<TsetmcAdapter>>(),
            CreateTestRateLimiter(),
            new InMemoryDataCache(),
            new DataQualityValidator(),
            authenticator,
            new JalaliCalendarService());
    }

    public static TgjuAdapter CreateTgjuAdapter(
        MockHttpMessageHandler httpHandler,
        DataSourceSettings? settings = null,
        IDataSourceAuthenticator? authenticator = null)
    {
        settings ??= CreateDefaultSettings();
        authenticator ??= CreateNoopAuthenticator();

        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://tgju.org")
        };

        return new TgjuAdapter(
            httpClient,
            Options.Create(settings),
            Mock.Of<ILogger<TgjuAdapter>>(),
            CreateTestRateLimiter(),
            new InMemoryDataCache(),
            new DataQualityValidator(),
            authenticator);
    }

    public static CbiAdapter CreateCbiAdapter(
        MockHttpMessageHandler httpHandler,
        DataSourceSettings? settings = null,
        IDataSourceAuthenticator? authenticator = null)
    {
        settings ??= CreateDefaultSettings();
        authenticator ??= CreateNoopAuthenticator();

        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://cbi.ir")
        };

        return new CbiAdapter(
            httpClient,
            Options.Create(settings),
            Mock.Of<ILogger<CbiAdapter>>(),
            CreateTestRateLimiter(),
            new InMemoryDataCache(),
            new DataQualityValidator(),
            authenticator);
    }

    public static InstrumentMapping CreateFooladInstrument() => new()
    {
        InstrumentId = "iran-equity-foolad-4439113430858354",
        Symbol = "\u0641\u0648\u0644\u0627\u062f",
        LatinSymbol = "Foolad",
        DisplayName = "Foolad Mobarakeh",
        AssetClass = AssetType.Stock,
        Exchange = "TSE",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Tsetmc] = new SourceIdentifier { Id = "4439113430858354" },
            [SourceAdapterType.TseWebGateway] = new SourceIdentifier { Id = "4439113430858354" },
            [SourceAdapterType.BrsApi] = new SourceIdentifier { Id = "4439113430858354" }
        }
    };

    public static InstrumentMapping CreateUsdIrrInstrument() => new()
    {
        InstrumentId = "iran-currency-usd-irr",
        Symbol = "\u062f\u0644\u0627\u0631",
        LatinSymbol = "USD/IRR",
        DisplayName = "US Dollar / Iranian Rial",
        AssetClass = AssetType.Currency,
        Exchange = "OTC",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Tgju] = new SourceIdentifier { Id = "price_dollar_rl" },
            [SourceAdapterType.Cbi] = new SourceIdentifier { Id = "USD" }
        }
    };

    public static string CreateTsetmcCandleJson(int dEven, decimal close, decimal open, decimal high, decimal low, long volume)
    {
        return $$"""
        {
          "closingPriceHistory": [
            {
              "dEven": {{dEven}},
              "pClosing": {{close}},
              "pDrCotVal": {{close}},
              "priceFirst": {{open}},
              "priceMax": {{high}},
              "priceMin": {{low}},
              "qTotTran5J": {{volume}},
              "qTotCap": {{(long)(close * volume)}},
              "zTotTran": 1000
            }
          ]
        }
        """;
    }

    public static string CreateTgjuLatestJson(decimal price, string symbol)
    {
        return $$"""
        {
          "p": {{price}},
          "h": {{price * 1.01m}},
          "l": {{price * 0.99m}},
          "d": "2026-08-21",
          "t": "{{symbol}}"
        }
        """;
    }

    public static string CreateCbiRateJson(string code, decimal rate, decimal? buyRate, decimal? sellRate)
    {
        var buy = buyRate.HasValue ? buyRate.Value.ToString() : "null";
        var sell = sellRate.HasValue ? sellRate.Value.ToString() : "null";
        return $$"""
        [
          {
            "CurrencyCode": "{{code}}",
            "Rate": {{rate}},
            "BuyRate": {{buy}},
            "SellRate": {{sell}}
          }
        ]
        """;
    }

    // --- New Adapter Helpers ---

    public static TseWebGatewayAdapter CreateTseWebGatewayAdapter(
        MockHttpMessageHandler httpHandler,
        DataSourceSettings? settings = null,
        IDataSourceAuthenticator? authenticator = null)
    {
        settings ??= CreateDefaultSettings();
        authenticator ??= CreateNoopAuthenticator();

        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://cdn.tsetmc.com")
        };

        return new TseWebGatewayAdapter(
            httpClient,
            Options.Create(settings),
            Mock.Of<ILogger<TseWebGatewayAdapter>>(),
            CreateTestRateLimiter(),
            new InMemoryDataCache(),
            new DataQualityValidator(),
            authenticator);
    }

    public static BrsApiAdapter CreateBrsApiAdapter(
        MockHttpMessageHandler httpHandler,
        DataSourceSettings? settings = null,
        IDataSourceAuthenticator? authenticator = null)
    {
        settings ??= CreateDefaultSettings();
        authenticator ??= CreateNoopAuthenticator();

        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://Api.BrsApi.ir")
        };

        return new BrsApiAdapter(
            httpClient,
            Options.Create(settings),
            Mock.Of<ILogger<BrsApiAdapter>>(),
            CreateTestRateLimiter(),
            new InMemoryDataCache(),
            new DataQualityValidator(),
            authenticator);
    }

    public static NobitexAdapter CreateNobitexAdapter(
        MockHttpMessageHandler httpHandler,
        DataSourceSettings? settings = null,
        IDataSourceAuthenticator? authenticator = null)
    {
        settings ??= CreateDefaultSettings();
        authenticator ??= CreateNoopAuthenticator();

        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://apiv2.nobitex.ir")
        };

        return new NobitexAdapter(
            httpClient,
            Options.Create(settings),
            Mock.Of<ILogger<NobitexAdapter>>(),
            CreateTestRateLimiter(),
            new InMemoryDataCache(),
            new DataQualityValidator(),
            authenticator);
    }

    public static InstrumentMapping CreateUsdtIrrInstrument() => new()
    {
        InstrumentId = "iran-crypto-usdt-irr",
        Symbol = "\u062a\u062a\u0631",
        LatinSymbol = "USDT/IRR",
        DisplayName = "Tether / IRR",
        AssetClass = AssetType.Crypto,
        Exchange = "free_market",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Nobitex] = new SourceIdentifier { Id = "USDTIRT" },
            [SourceAdapterType.Tgju] = new SourceIdentifier { Id = "price_tether" }
        }
    };

    public static InstrumentMapping CreateBrsApiFooladInstrument() => new()
    {
        InstrumentId = "iran-equity-foolad-4439113430858354",
        Symbol = "\u0641\u0648\u0644\u0627\u062f",
        LatinSymbol = "Foolad",
        DisplayName = "Foolad Mobarakeh",
        AssetClass = AssetType.Stock,
        Exchange = "TSE",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.BrsApi] = new SourceIdentifier { Id = "4439113430858354" }
        }
    };

    public static string CreateNobitexOhlcJson(long[] timestamps, decimal[] opens, decimal[] highs, decimal[] lows, decimal[] closes, decimal[] volumes)
    {
        var t = string.Join(",", timestamps);
        var o = string.Join(",", opens.Select(x => x.ToString()));
        var h = string.Join(",", highs.Select(x => x.ToString()));
        var l = string.Join(",", lows.Select(x => x.ToString()));
        var c = string.Join(",", closes.Select(x => x.ToString()));
        var v = string.Join(",", volumes.Select(x => x.ToString()));
        return $$"""
        {
          "s": "ok",
          "t": [{{t}}],
          "o": [{{o}}],
          "h": [{{h}}],
          "l": [{{l}}],
          "c": [{{c}}],
          "v": [{{v}}]
        }
        """;
    }

    public static string CreateNobitexStatsJson(string pairKey, decimal latest, decimal? dayOpen, decimal? dayHigh, decimal? dayLow, decimal? bestSell, decimal? bestBuy)
    {
        var obj = $"\"{pairKey}\": {{ \"isClosed\": false";
        obj += $", \"latest\": \"{latest}\"";
        if (dayOpen.HasValue) obj += $", \"dayOpen\": \"{dayOpen}\"";
        if (dayHigh.HasValue) obj += $", \"dayHigh\": \"{dayHigh}\"";
        if (dayLow.HasValue) obj += $", \"dayLow\": \"{dayLow}\"";
        if (bestSell.HasValue) obj += $", \"bestSell\": \"{bestSell}\"";
        if (bestBuy.HasValue) obj += $", \"bestBuy\": \"{bestBuy}\"";
        obj += " }";
        return $$"""
        {
          "status": "ok",
          "stats": { {{obj}} }
        }
        """;
    }

    public static string CreateBrsApiAllSymbolsJson(string insCode, string symbol, decimal open, decimal last, decimal close, decimal dayLow, decimal dayHigh, long volume, long tradeCount)
    {
        return $$"""
        [
          {
            "l18": "{{symbol}}",
            "l30": "Test Company",
            "isin": "IRO1TEST0001",
            "id": "{{insCode}}",
            "pf": {{open}},
            "pl": {{last}},
            "pc": {{close}},
            "pmin": {{dayLow}},
            "pmax": {{dayHigh}},
            "py": {{open}},
            "tno": {{tradeCount}},
            "tvol": {{volume}},
            "tval": {{(long)(close * volume)}}
          },
          {
            "l18": "OTHER",
            "l30": "Other Company",
            "id": "99999",
            "pf": 100,
            "pl": 110,
            "pc": 105,
            "pmin": 95,
            "pmax": 115,
            "py": 100,
            "tno": 500,
            "tvol": 500000,
            "tval": 52500000
          }
        ]
        """;
    }

    public static string CreateTseWebGatewayOrderBookJson()
    {
        return $$"""
        {
          "bestLimits": [
            { "number": 1, "zo": 5, "qo": 8000, "po": 4499, "zd": 6, "qd": 444690, "pd": 4497 },
            { "number": 2, "zo": 2, "qo": 7792, "po": 4500, "zd": 1, "qd": 2000, "pd": 4496 },
            { "number": 3, "zo": 1, "qo": 30000, "po": 4503, "zd": 1, "qd": 2000, "pd": 4492 },
            { "number": 4, "zo": 2, "qo": 120000, "po": 4512, "zd": 1, "qd": 2000, "pd": 4491 },
            { "number": 5, "zo": 1, "qo": 2000, "po": 4513, "zd": 12, "qd": 283030, "pd": 4490 }
          ]
        }
        """;
    }

    public static string CreateTseWebGatewayInstrumentInfoJson()
    {
        return $$"""
        {
          "instrumentInfo": {
            "isin": "IRO1FOLD0001",
            "sector": { "name": " metals" }
          }
        }
        """;
    }
}
