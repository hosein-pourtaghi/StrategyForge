using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Streaming JSONL exporter for AI-ready datasets.
///
/// Contract (Steps 15–17):
/// - JSONL: one record per line, processed incrementally — records are written
///   batch by batch, never "load all then serialize".
/// - Bounded memory: the writer holds one batch of records at a time; the
///   <see cref="DatasetStatisticsAccumulator"/> holds counts plus the bounded
///   forward-return samples required for medians.
/// - Deterministic: field order, date formatting ("yyyy-MM-dd"), decimal
///   rounding (8 places), and record ordering are fixed by the serializer
///   configuration and the projector's chronological output.
/// - Structured, never flattened to text (Step 9): numeric fields serialize as
///   numbers; null means "not supplied" and is preserved as null.
/// </summary>
public sealed class AiReadyJsonlExporter
{
    /// <summary>JSON serializer options fixed for deterministic output.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters =
        {
            // Deterministic, culture-invariant decimal formatting.
            new DeterministicDecimalJsonConverter()
        }
    };

    /// <summary>
    /// Writes the given records as JSONL, one per line, then returns the
    /// deterministic statistics accumulated while writing.
    ///
    /// This overload materializes the caller's batch (bounded by the caller).
    /// For unbounded datasets use <see cref="WriteRecordsAsync"/> per batch.
    /// </summary>
    public async Task<long> WriteAsync(
        Stream output,
        IReadOnlyList<AiReadyObservation> records,
        DatasetStatisticsAccumulator statistics,
        IReadOnlyList<string> allRuleNames,
        CancellationToken cancellationToken = default)
    {
        long count = 0;

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteRecordAsync(output, record, statistics, allRuleNames, cancellationToken);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Writes one record as a single JSONL line and feeds the accumulator.
    /// </summary>
    public async Task WriteRecordAsync(
        Stream output,
        AiReadyObservation record,
        DatasetStatisticsAccumulator statistics,
        IReadOnlyList<string> allRuleNames,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(record, SerializerOptions);
        statistics.Add(record, allRuleNames);

        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await output.WriteAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// Serializes one record to JSON (used by inspection sampling and tests).
    /// </summary>
    public string SerializeRecord(AiReadyObservation record) =>
        JsonSerializer.Serialize(record, SerializerOptions);
}

/// <summary>
/// Culture-invariant, fixed-precision decimal JSON converter for deterministic
/// numeric formatting across runs and machines.
/// </summary>
public sealed class DeterministicDecimalJsonConverter : JsonConverter<decimal>
{
    public DeterministicDecimalJsonConverter()
    {
    }

    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        return decimal.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        // Fixed scale avoids culture-specific separators/exponent formatting.
        writer.WriteRawValue(
            value.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture),
            skipInputValidation: true);
    }
}
