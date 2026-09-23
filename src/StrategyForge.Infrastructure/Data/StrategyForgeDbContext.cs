using Microsoft.EntityFrameworkCore;
using StrategyForge.Infrastructure.Data.Entities;

namespace StrategyForge.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for StrategyForge persistence.
/// 
/// Provides access to:
/// - Evidence: Persisted analysis evidence with full provenance
/// - Strategies: Persisted strategy reports for historical tracking
/// - IntelligenceRuns: Background intelligence run history
/// - HistoricalDataset: normalized, idempotent historical market observations
/// - EnrichedObservations: derived, idempotent analysis-ready observations
/// 
/// Configuration is loaded from DatabaseSettings (appsettings.json).
/// </summary>
public class StrategyForgeDbContext : DbContext
{
    public DbSet<EvidenceEntity> Evidence => Set<EvidenceEntity>();
    public DbSet<StrategyEntity> Strategies => Set<StrategyEntity>();
    public DbSet<IntelligenceRunEntity> IntelligenceRuns => Set<IntelligenceRunEntity>();
    public DbSet<HistoricalDatasetEntity> HistoricalDataset => Set<HistoricalDatasetEntity>();
    public DbSet<EnrichedObservationEntity> EnrichedObservations => Set<EnrichedObservationEntity>();

    public StrategyForgeDbContext(DbContextOptions<StrategyForgeDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Evidence ---
        modelBuilder.Entity<EvidenceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AssetSymbol);
            entity.HasIndex(e => e.AssembledAt);
            entity.HasIndex(e => new { e.AssetSymbol, e.AssembledAt });

            entity.Property(e => e.AssetSymbol).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AssetName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AssetMarket).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AssetJson).IsRequired();
            entity.Property(e => e.EvidenceJson).IsRequired();
            entity.Property(e => e.ExecutionId).HasMaxLength(50);
        });

        // --- Strategies ---
        modelBuilder.Entity<StrategyEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AssetSymbol);
            entity.HasIndex(e => e.GeneratedAt);
            entity.HasIndex(e => new { e.AssetSymbol, e.GeneratedAt });

            entity.Property(e => e.AssetSymbol).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AssetName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AssetMarket).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AssetJson).IsRequired();
            entity.Property(e => e.ReportJson).IsRequired();
            entity.Property(e => e.OverallSentiment).HasMaxLength(20);
            entity.Property(e => e.PipelineState).HasMaxLength(30);
            entity.Property(e => e.LlmModel).HasMaxLength(100);
            entity.Property(e => e.ContributingAgents).HasMaxLength(500);
        });

        // --- Intelligence Runs ---
        modelBuilder.Entity<IntelligenceRunEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ScheduledAt);
            entity.HasIndex(e => e.State);

            entity.Property(e => e.State).HasMaxLength(30).IsRequired();
            entity.Property(e => e.TargetAssetsJson).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
        });

        // --- Historical Dataset ---
        modelBuilder.Entity<HistoricalDatasetEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Idempotency boundary: one observation per (instrument, source, date).
            entity.HasIndex(e => new { e.InstrumentId, e.Source, e.ObservationDate })
                .IsUnique()
                .HasDatabaseName("IX_HistoricalDataset_Identity");

            entity.HasIndex(e => e.ObservationDate);

            entity.Property(e => e.InstrumentId).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(40).IsRequired();
            entity.Property(e => e.SourceSymbol).HasMaxLength(100);
            entity.Property(e => e.SourceInstrumentId).HasMaxLength(100);
            entity.Property(e => e.MarketTimezone).HasMaxLength(64);
            entity.Property(e => e.SourceDate).HasMaxLength(32);
            entity.Property(e => e.SourceCalendar).HasMaxLength(20);
            entity.Property(e => e.AdjustmentType).HasMaxLength(30);
            entity.Property(e => e.AdjustmentSource).HasMaxLength(100);
            entity.Property(e => e.Endpoint).HasMaxLength(300);
        });

        // --- Enriched (Derived) Observations ---
        modelBuilder.Entity<EnrichedObservationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Idempotency boundary mirrors the raw dataset: one derived observation
            // per (instrument, source, date). Re-processing updates in place.
            entity.HasIndex(e => new { e.InstrumentId, e.Source, e.ObservationDate })
                .IsUnique()
                .HasDatabaseName("IX_EnrichedObservations_Identity");

            entity.HasIndex(e => e.ObservationDate);

            entity.Property(e => e.InstrumentId).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(40).IsRequired();
            entity.Property(e => e.IndicatorsJson).IsRequired();
            entity.Property(e => e.QualityStatus).HasMaxLength(20).IsRequired();
            entity.Property(e => e.WarningCodesJson).IsRequired();
            entity.Property(e => e.ProcessedBy).HasMaxLength(40).IsRequired();
        });
    }
}
