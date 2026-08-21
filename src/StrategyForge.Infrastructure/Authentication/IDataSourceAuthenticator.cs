using StrategyForge.Domain.Configuration;

namespace StrategyForge.Infrastructure.Authentication;

/// <summary>
/// Authenticator for data source HTTP requests.
/// Each source/capability can have a different authentication mode.
/// Adapters call this before making requests to attach credentials.
/// </summary>
public interface IDataSourceAuthenticator
{
    /// <summary>
    /// Applies authentication to the HTTP request message.
    /// Only modifies the request if authentication is required and configured.
    /// </summary>
    /// <param name="request">The HTTP request to authenticate.</param>
    /// <param name="settings">The authentication configuration for this source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if authentication was successfully applied (or not required); false if credentials are missing/invalid.</returns>
    Task<AuthenticationResult> AuthenticateAsync(
        HttpRequestMessage request,
        AuthenticationSettings settings,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an authentication attempt.
/// </summary>
public sealed record AuthenticationResult
{
    /// <summary>Whether authentication was successfully applied.</summary>
    public bool Success { get; init; }

    /// <summary>Error code if authentication failed.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable error message.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The authentication mode that was attempted.</summary>
    public AuthenticationMode Mode { get; init; }

    /// <summary>Whether the failure is retryable (e.g., transient network issue vs. permanent invalid credentials).</summary>
    public bool Retryable { get; init; }

    public static AuthenticationResult Succeeded(AuthenticationMode mode) => new()
    {
        Success = true,
        Mode = mode
    };

    public static AuthenticationResult Failed(AuthenticationMode mode, string errorCode, string errorMessage, bool retryable = false) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        Mode = mode,
        Retryable = retryable
    };
}
