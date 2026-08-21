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
            [SourceAdapterType.Tsetmc] = new SourceIdentifier { Id = "4439113430858354" }
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
}
