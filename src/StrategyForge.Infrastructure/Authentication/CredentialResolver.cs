using Microsoft.Extensions.Configuration;
using StrategyForge.Domain.Configuration;

namespace StrategyForge.Infrastructure.Authentication;

/// <summary>
/// Resolves credentials from ASP.NET Core configuration (environment variables, appsettings, etc.).
/// Never stores secrets in memory longer than needed.
/// </summary>
public sealed class CredentialResolver
{
    private readonly IConfiguration _configuration;

    public CredentialResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Resolves a credential value from configuration using the reference key.
    /// Supports both environment variable names and configuration paths.
    /// </summary>
    public string? Resolve(string? credentialReference)
    {
        if (string.IsNullOrWhiteSpace(credentialReference))
            return null;

        // Try as a configuration path (e.g., "StrategyForge:Tgju:ApiKey")
        var value = _configuration[credentialReference];
        if (!string.IsNullOrEmpty(value))
            return value;

        // Try as environment variable
        var envValue = Environment.GetEnvironmentVariable(credentialReference);
        return envValue;
    }

    /// <summary>
    /// Resolves a credential value and returns whether it was found.
    /// </summary>
    public (string? Value, bool IsConfigured) ResolveWithStatus(string? credentialReference)
    {
        var value = Resolve(credentialReference);
        return (value, !string.IsNullOrEmpty(value));
    }
}
