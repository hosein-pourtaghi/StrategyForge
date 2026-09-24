using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StrategyForge.Analysis;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Infrastructure;
using StrategyForge.Infrastructure.Services;

// ============================================================
// StrategyForge Phase 9 dataset verification harness.
//
// Executes one end-to-end dataset preparation run over a real
// provider (defaults: TGJU / USD-IRR free market) using the exact
// production DI registrations:
//
//   Provider → HistoricalIngestionService → HistoricalDatasetStore   (Phase 6)
//     → HistoricalProcessingService → EnrichedDatasetStore           (Phase 7)
//     → DatasetReportsService (coverage + quality)                   (Phase 9)
//     → DatasetPreparationService → JSONL + manifest                 (Phase 9)
//
// Usage:
//   dotnet run --project tools/DatasetPreparation.Cli -- <instrument> <source> <from> <to> <outputDir>
//   e.g.  ... -- "USD/IRR" Tgju 2024-01-01 2026-09-22 ./dataset-output
// ============================================================

var instrument = args.Length > 0 ? args[0] : "USD/IRR";
var source = args.Length > 1 ? Enum.Parse<SourceAdapterType>(args[1], ignoreCase: true) : SourceAdapterType.Tgju;
var from = args.Length > 2 ? DateOnly.Parse(args[2]) : new DateOnly(2024, 1, 1);
var to = args.Length > 3 ? DateOnly.Parse(args[3]) : DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
var outputDir = args.Length > 4 ? args[4] : "./dataset-output";

Directory.CreateDirectory(outputDir);

var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "src", "StrategyForge.Api", "appsettings.json"), optional: false)
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddLogging();
services.AddStrategyForgeInfrastructure(configuration);
services.AddStrategyForgeAnalysis();

await using var provider = services.BuildServiceProvider();

var runner = provider.GetRequiredService<DatasetPreparationRunner>();

Console.WriteLine($"=== StrategyForge Phase 9 dataset verification ===");
Console.WriteLine($"Instrument: {instrument}");
Console.WriteLine($"Source:     {source}");
Console.WriteLine($"Range:      {from:yyyy-MM-dd} .. {to:yyyy-MM-dd}");
Console.WriteLine($"Output:     {Path.GetFullPath(outputDir)}");
Console.WriteLine();

var jsonlPath = Path.Combine(outputDir, $"ai-ready-{instrument.Replace('/', '-')}-{source}-{from:yyyyMMdd}-{to:yyyyMMdd}.jsonl");
var manifestPath = Path.Combine(outputDir, "manifest.json");

await using (var jsonl = File.Create(jsonlPath))
await using (var manifest = File.Create(manifestPath))
{
    var result = await runner.RunAsync(
        instrument,
        source,
        from,
        to,
        jsonl,
        manifestOutput: manifest,
        skipIngestion: args.Contains("--skip-ingestion"),
        cancellationToken: default);
}

// --- Read back the manifest and report actual numbers ---
var manifestJson = await File.ReadAllTextAsync(manifestPath);
using var doc = System.Text.Json.JsonDocument.Parse(manifestJson);
var root = doc.RootElement;

Console.WriteLine("--- Dataset ---");
Console.WriteLine($"DatasetId:     {root.GetProperty("datasetId").GetString()}");
Console.WriteLine($"Version:       {root.GetProperty("datasetVersion").GetString()}");
Console.WriteLine($"RowCount:      {root.GetProperty("rowCount").GetInt32()}");
Console.WriteLine($"Features:      {root.GetProperty("featureSet").GetArrayLength()}");
Console.WriteLine($"ContextWindow: {root.GetProperty("contextWindowObservations").GetInt32()}");
Console.WriteLine($"Horizons:      {string.Join(",", root.GetProperty("forwardHorizons").EnumerateArray().Select(e => e.GetInt32()))}");
Console.WriteLine();

Console.WriteLine("--- Coverage ---");
foreach (var entry in root.GetProperty("coverage").EnumerateArray())
{
    Console.WriteLine($"  {entry.GetProperty("instrumentId")} / {entry.GetProperty("source")}: " +
        $"{entry.GetProperty("observationCount")} observations " +
        $"({entry.GetProperty("firstObservation").GetString()} .. {entry.GetProperty("lastObservation").GetString()}), " +
        $"gaps={entry.GetProperty("gapCount")}, largestGapDays={entry.GetProperty("largestGapDays")}, " +
        $"avg/yr={entry.GetProperty("averageObservationsPerYear").GetDecimal()}");
}

Console.WriteLine();
Console.WriteLine("--- Quality ---");
var qualityEntries = root.GetProperty("qualitySummary").GetProperty("entries");
foreach (var quality in qualityEntries.EnumerateArray())
{
    Console.WriteLine($"  Total: {quality.GetProperty("totalRows").GetInt32()}, " +
        $"Valid: {quality.GetProperty("validRows").GetInt32()}, " +
        $"Warning: {quality.GetProperty("warningRows").GetInt32()}, " +
        $"Invalid: {quality.GetProperty("invalidRows").GetInt32()}");
    Console.WriteLine($"  ZeroVolume: {quality.GetProperty("zeroVolumeRows").GetInt32()}, " +
        $"DateGapWarnings: {quality.GetProperty("dateGapWarnings").GetInt32()}, " +
        $"IncompleteProvenance: {quality.GetProperty("incompleteProvenanceWarnings").GetInt32()}");
    var reasons = quality.GetProperty("reasonCounts").EnumerateObject();
    foreach (var reason in reasons)
    {
        Console.WriteLine($"    {reason.Name}: {reason.Value.GetInt32()}");
    }
}

Console.WriteLine();
Console.WriteLine("--- Splits ---");
var segmentNames = new[] { "Research", "Validation", "Holdout" };
foreach (var segment in root.GetProperty("splitSegments").EnumerateArray())
{
    var segmentIndex = segment.GetProperty("segment").GetInt32();
    var segmentName = segmentIndex >= 0 && segmentIndex < segmentNames.Length
        ? segmentNames[segmentIndex]
        : segmentIndex.ToString();
    Console.WriteLine($"  {segmentName}: " +
        $"{segment.GetProperty("firstObservation").GetString()} .. {segment.GetProperty("lastObservation").GetString()} " +
        $"({segment.GetProperty("observations")} observations)");
}

var jsonlInfo = new FileInfo(jsonlPath);
Console.WriteLine();
Console.WriteLine("--- Export ---");
Console.WriteLine($"  JSONL: {jsonlInfo.FullName}");
Console.WriteLine($"  Records: {File.ReadLines(jsonlPath).Count()}");
Console.WriteLine($"  Size: {jsonlInfo.Length:N0} bytes ({jsonlInfo.Length / 1024.0:N1} KiB)");
Console.WriteLine($"  Manifest: {manifestPath} ({new FileInfo(manifestPath).Length:N0} bytes)");

Console.WriteLine();
Console.WriteLine("--- First JSONL record ---");
Console.WriteLine(File.ReadLines(jsonlPath).First());

var process = System.Diagnostics.Process.GetCurrentProcess();
Console.WriteLine();
Console.WriteLine($"PeakWorkingSet64: {process.PeakWorkingSet64:N0} bytes ({process.PeakWorkingSet64 / 1024.0 / 1024.0:N1} MiB)");
return 0;
