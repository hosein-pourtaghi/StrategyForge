using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Models;

public class DataResultTests
{
    // --- Success ---

    [Fact]
    public void Success_SetsOkTrue()
    {
        var result = DataResult<string>.Success("hello");

        Assert.True(result.Ok);
        Assert.Equal("hello", result.Data);
    }

    [Fact]
    public void Success_DefaultsFreshness()
    {
        var result = DataResult<string>.Success("test");

        Assert.NotNull(result.Freshness);
        Assert.False(result.Freshness!.IsCached);
    }

    [Fact]
    public void Success_DefaultsQuality()
    {
        var result = DataResult<string>.Success("test");

        Assert.NotNull(result.Quality);
        Assert.Equal(100, result.Quality!.Score);
    }

    [Fact]
    public void Success_DefaultsWarningsEmpty()
    {
        var result = DataResult<string>.Success("test");

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Success_WithExplicitFreshness()
    {
        var freshness = DataFreshness.Fresh(5000);
        var result = DataResult<string>.Success("test", freshness: freshness);

        Assert.Equal(5000, result.Freshness!.MaxAllowedAgeMs);
    }

    [Fact]
    public void Success_WithExplicitQuality()
    {
        var quality = DataQuality.WithFlags(75, true, QualityFlag.Stale);
        var result = DataResult<string>.Success("test", quality: quality);

        Assert.Equal(75, result.Quality!.Score);
        Assert.True(result.Quality!.Flags.HasFlag(QualityFlag.Stale));
    }

    [Fact]
    public void Success_WithWarnings()
    {
        var warnings = new List<DataWarning>
        {
            new() { Code = "STALE", Message = "Data is 5 minutes old" }
        };

        var result = DataResult<string>.Success("test", warnings: warnings);

        Assert.Single(result.Warnings);
        Assert.Equal("STALE", result.Warnings[0].Code);
    }

    [Fact]
    public void Success_WithErrorIsNull()
    {
        var result = DataResult<string>.Success("test");

        Assert.Null(result.Error);
    }

    [Fact]
    public void Success_WithSummary()
    {
        var summary = new DataSummary { Count = 100, Description = "Test" };
        var result = DataResult<IReadOnlyList<string>>.Success(new[] { "a", "b" }.AsReadOnly(), summary: summary);

        Assert.NotNull(result.Summary);
        Assert.Equal(100, result.Summary!.Count);
    }

    [Fact]
    public void Success_WithMetadata()
    {
        var meta = new AcquisitionMetadata { Elapsed = TimeSpan.FromSeconds(1), CacheHit = false };
        var result = DataResult<string>.Success("test", metadata: meta);

        Assert.NotNull(result.Metadata);
        Assert.Equal(TimeSpan.FromSeconds(1), result.Metadata!.Elapsed);
    }

    // --- Failure ---

    [Fact]
    public void Failure_SetsOkFalse()
    {
        var error = new DataCollectionError2
        {
            Code = "SOURCE_UNAVAILABLE",
            Message = "Source is down",
            Retryable = true
        };

        var result = DataResult<string>.Failure(error);

        Assert.False(result.Ok);
        Assert.Null(result.Data);
    }

    [Fact]
    public void Failure_DataIsNull()
    {
        var error = new DataCollectionError2
        {
            Code = "TIMEOUT",
            Message = "Request timed out",
            Retryable = true
        };

        var result = DataResult<string>.Failure(error);

        Assert.Null(result.Data);
    }

    [Fact]
    public void Failure_PreservesError()
    {
        var error = new DataCollectionError2
        {
            Code = "INSTRUMENT_NOT_FOUND",
            Message = "Symbol not found",
            Retryable = false,
            SourceHttpStatus = 404
        };

        var result = DataResult<string>.Failure(error);

        Assert.NotNull(result.Error);
        Assert.Equal("INSTRUMENT_NOT_FOUND", result.Error!.Code);
        Assert.Equal("Symbol not found", result.Error!.Message);
        Assert.False(result.Error!.Retryable);
        Assert.Equal(404, result.Error!.SourceHttpStatus);
    }

    [Fact]
    public void Failure_WithWarnings()
    {
        var error = new DataCollectionError2
        {
            Code = "SOURCE_UNAVAILABLE",
            Message = "Source is down",
            Retryable = true
        };
        var warnings = new List<DataWarning>
        {
            new() { Code = "FALLBACK", Message = "Falling back to cached data" }
        };

        var result = DataResult<string>.Failure(error, warnings);

        Assert.Single(result.Warnings);
        Assert.Equal("FALLBACK", result.Warnings[0].Code);
    }

    [Fact]
    public void Failure_CannotAppearSuccessful()
    {
        var error = new DataCollectionError2
        {
            Code = "ERROR",
            Message = "Failed",
            Retryable = false
        };

        var result = DataResult<string>.Failure(error);

        Assert.False(result.Ok);
        Assert.Null(result.Data);
        Assert.NotNull(result.Error);
    }

    // --- Request Info ---

    [Fact]
    public void RequestInfo_CanBeNull()
    {
        var result = DataResult<string>.Success("test");

        Assert.Null(result.Request);
    }

    [Fact]
    public void RequestInfo_CanBeSet()
    {
        var request = new DataRequestInfo
        {
            InstrumentId = "test-id",
            RequestedSymbol = "فولاد",
            DataType = "daily_ohlc",
            From = new DateOnly(2024, 1, 1),
            To = new DateOnly(2024, 12, 31)
        };

        var result = new DataResult<string>
        {
            Ok = true,
            Data = "test",
            Request = request
        };

        Assert.NotNull(result.Request);
        Assert.Equal("فولاد", result.Request!.RequestedSymbol);
        Assert.Equal("daily_ohlc", result.Request!.DataType);
    }

    // --- MarketContext ---

    [Fact]
    public void MarketContext_CanBeNull()
    {
        var result = DataResult<string>.Success("test");

        Assert.Null(result.MarketContext);
    }

    [Fact]
    public void MarketContext_CanBeSet()
    {
        var ctx = new MarketContext2
        {
            AssetClass = "equity",
            Exchange = "TSE",
            RateType = null,
            IsProxy = false
        };

        var result = DataResult<string>.Success("test", marketContext: ctx);

        Assert.NotNull(result.MarketContext);
        Assert.Equal("equity", result.MarketContext!.AssetClass);
        Assert.Equal("TSE", result.MarketContext!.Exchange);
    }

    // --- Generic Type Handling ---

    [Fact]
    public void GenericList_CanBeUsedAsPayload()
    {
        var candles = new List<Candle>
        {
            new() { Date = new DateOnly(2024, 1, 1), Open = 100, High = 110, Low = 90, Close = 105, Volume = 1000 },
            new() { Date = new DateOnly(2024, 1, 2), Open = 105, High = 115, Low = 100, Close = 110, Volume = 1200 }
        };

        var result = DataResult<IReadOnlyList<Candle>>.Success(candles.AsReadOnly());

        Assert.True(result.Ok);
        Assert.Equal(2, result.Data!.Count);
    }
}
