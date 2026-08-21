namespace StrategyForge.Domain.Configuration;

/// <summary>
/// Authentication mode for a data source adapter.
/// Determines what type of credentials, if any, are needed.
/// </summary>
public enum AuthenticationMode
{
    /// <summary>No authentication required. Public endpoint.</summary>
    None,

    /// <summary>API key passed via header or query parameter.</summary>
    ApiKey,

    /// <summary>Bearer token (e.g., OAuth2).</summary>
    BearerToken,

    /// <summary>HTTP Basic authentication (username + password).</summary>
    Basic,

    /// <summary>Username and password credentials.</summary>
    UsernamePassword,

    /// <summary>HMAC signature-based authentication.</summary>
    Hmac,

    /// <summary>Session/cookie-based authentication.</summary>
    Session,

    /// <summary>Custom authentication mechanism.</summary>
    Custom
}

/// <summary>
/// Configuration for source-specific authentication.
/// Credentials are loaded from environment variables or secure configuration.
/// </summary>
public sealed record AuthenticationSettings
{
    /// <summary>The authentication mode required by this source/capability.</summary>
    public AuthenticationMode Mode { get; init; } = AuthenticationMode.None;

    /// <summary>
    /// Configuration key or environment variable name for the primary credential.
    /// Examples:
    ///   "STRATEGYFORGE_TGJU_API_KEY"
    ///   "StrategyForge:Tgju:ApiKey"
    /// </summary>
    public string? CredentialReference { get; init; }

    /// <summary>
    /// Configuration key for a secondary credential (e.g., API secret for HMAC).
    /// </summary>
    public string? SecondaryCredentialReference { get; init; }

    /// <summary>
    /// Configuration key for username (for Basic or UsernamePassword modes).
    /// </summary>
    public string? UsernameReference { get; init; }

    /// <summary>
    /// Configuration key for password (for Basic or UsernamePassword modes).
    /// </summary>
    public string? PasswordReference { get; init; }

    /// <summary>Whether credentials are required for this source.</summary>
    public bool RequiresCredentials => Mode != AuthenticationMode.None;
}
