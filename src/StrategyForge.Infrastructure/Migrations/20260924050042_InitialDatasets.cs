using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialDatasets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnrichedObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ObservationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Open = table.Column<decimal>(type: "numeric", nullable: false),
                    High = table.Column<decimal>(type: "numeric", nullable: false),
                    Low = table.Column<decimal>(type: "numeric", nullable: false),
                    Close = table.Column<decimal>(type: "numeric", nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    Change = table.Column<decimal>(type: "numeric", nullable: true),
                    ChangePercent = table.Column<decimal>(type: "numeric", nullable: true),
                    IndicatorsJson = table.Column<string>(type: "text", nullable: false),
                    QualityStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WarningCodesJson = table.Column<string>(type: "text", nullable: false),
                    ProcessedBy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrichedObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetSymbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssetMarket = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssembledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssetJson = table.Column<string>(type: "text", nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    DataSources = table.Column<string>(type: "text", nullable: true),
                    IndicatorCount = table.Column<int>(type: "integer", nullable: false),
                    NewsItemCount = table.Column<int>(type: "integer", nullable: false),
                    DataQualityScore = table.Column<decimal>(type: "numeric", nullable: true),
                    ExecutionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalDataset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ObservationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Open = table.Column<decimal>(type: "numeric", nullable: false),
                    High = table.Column<decimal>(type: "numeric", nullable: false),
                    Low = table.Column<decimal>(type: "numeric", nullable: false),
                    Close = table.Column<decimal>(type: "numeric", nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: true),
                    TradeCount = table.Column<long>(type: "bigint", nullable: true),
                    LastPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    Change = table.Column<decimal>(type: "numeric", nullable: true),
                    ChangePercent = table.Column<decimal>(type: "numeric", nullable: true),
                    MarketTimezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceDate = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SourceCalendar = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AdjustmentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AdjustmentSource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceSymbol = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceInstrumentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FetchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ExtraPropertiesJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalDataset", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntelligenceRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetAssetsJson = table.Column<string>(type: "text", nullable: false),
                    SuccessfulAssets = table.Column<int>(type: "integer", nullable: false),
                    FailedAssets = table.Column<int>(type: "integer", nullable: false),
                    EvidenceIdsJson = table.Column<string>(type: "text", nullable: true),
                    StrategyIdsJson = table.Column<string>(type: "text", nullable: true),
                    GenerateStrategies = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TotalTokensUsed = table.Column<int>(type: "integer", nullable: false),
                    TotalDurationMs = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntelligenceRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Strategies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetSymbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssetMarket = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssetJson = table.Column<string>(type: "text", nullable: false),
                    ReportJson = table.Column<string>(type: "text", nullable: false),
                    OverallSentiment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OverallConfidence = table.Column<decimal>(type: "numeric", nullable: true),
                    PipelineState = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ContributingAgents = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LlmModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TokensUsed = table.Column<int>(type: "integer", nullable: true),
                    GenerationDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strategies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrichedObservations_Identity",
                table: "EnrichedObservations",
                columns: new[] { "InstrumentId", "Source", "ObservationDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnrichedObservations_ObservationDate",
                table: "EnrichedObservations",
                column: "ObservationDate");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_AssembledAt",
                table: "Evidence",
                column: "AssembledAt");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_AssetSymbol",
                table: "Evidence",
                column: "AssetSymbol");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_AssetSymbol_AssembledAt",
                table: "Evidence",
                columns: new[] { "AssetSymbol", "AssembledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalDataset_Identity",
                table: "HistoricalDataset",
                columns: new[] { "InstrumentId", "Source", "ObservationDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalDataset_ObservationDate",
                table: "HistoricalDataset",
                column: "ObservationDate");

            migrationBuilder.CreateIndex(
                name: "IX_IntelligenceRuns_ScheduledAt",
                table: "IntelligenceRuns",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_IntelligenceRuns_State",
                table: "IntelligenceRuns",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_AssetSymbol",
                table: "Strategies",
                column: "AssetSymbol");

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_AssetSymbol_GeneratedAt",
                table: "Strategies",
                columns: new[] { "AssetSymbol", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_GeneratedAt",
                table: "Strategies",
                column: "GeneratedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrichedObservations");

            migrationBuilder.DropTable(
                name: "Evidence");

            migrationBuilder.DropTable(
                name: "HistoricalDataset");

            migrationBuilder.DropTable(
                name: "IntelligenceRuns");

            migrationBuilder.DropTable(
                name: "Strategies");
        }
    }
}
