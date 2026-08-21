using StrategyForge.Domain.Enums;

namespace StrategyForge.Api.Contracts;

public sealed record DataSourceResponse
{
    public required string Name { get; init; }
    public required SourceAdapterType SourceType { get; init; }
    public required bool IsEnabled { get; init; }
    public IReadOnlyList<string> SupportedAssetClasses { get; init; } = [];
    public HealthResponse? Health { get; init; }
}

public sealed record HealthResponse
{
    public bool IsHealthy { get; init; }
    public DateTimeOffset? LastSuccessfulRequest { get; init; }
    public string? LastError { get; init; }
    public int ConsecutiveFailures { get; init; }
}
