using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Configuration;

namespace StrategyForge.Infrastructure.Authentication;

/// <summary>
/// Composite authenticator that handles all supported authentication modes.
/// Resolves credentials from configuration and applies them to HTTP requests.
/// </summary>
public sealed class CompositeDataSourceAuthenticator : IDataSourceAuthenticator
{
    private readonly CredentialResolver _credentialResolver;
    private readonly ILogger<CompositeDataSourceAuthenticator> _logger;

    public CompositeDataSourceAuthenticator(
        CredentialResolver credentialResolver,
        ILogger<CompositeDataSourceAuthenticator> logger)
    {
        _credentialResolver = credentialResolver;
        _logger = logger;
    }

    public Task<AuthenticationResult> AuthenticateAsync(
        HttpRequestMessage request,
        AuthenticationSettings settings,
        CancellationToken cancellationToken = default)
    {
        // No authentication required
        if (settings.Mode == AuthenticationMode.None)
        {
            return Task.FromResult(AuthenticationResult.Succeeded(AuthenticationMode.None));
        }

        // For modes that use CredentialReference, validate it's configured
        bool requiresCredentialRef = settings.Mode is
            AuthenticationMode.ApiKey or
            AuthenticationMode.BearerToken or
            AuthenticationMode.Session;

        if (requiresCredentialRef)
        {
            if (string.IsNullOrWhiteSpace(settings.CredentialReference))
            {
                return Task.FromResult(AuthenticationResult.Failed(
                    settings.Mode,
                    "AUTHENTICATION_REQUIRED",
                    $"Authentication mode '{settings.Mode}' requires a credential reference, but none was configured."));
            }

            var (_, isConfigured) = _credentialResolver.ResolveWithStatus(settings.CredentialReference);
            if (!isConfigured)
            {
                _logger.LogWarning(
                    "Authentication required for mode {Mode} but credential reference '{Reference}' is not configured.",
                    settings.Mode, settings.CredentialReference);

                return Task.FromResult(AuthenticationResult.Failed(
                    settings.Mode,
                    "AUTHENTICATION_REQUIRED",
                    $"Authentication credentials for mode '{settings.Mode}' are not configured. " +
                    $"Expected credential at configuration key or environment variable '{settings.CredentialReference}'."));
            }
        }

        // Apply authentication based on mode
        return Task.FromResult(settings.Mode switch
        {
            AuthenticationMode.ApiKey => ApplyApiKey(request, settings),
            AuthenticationMode.BearerToken => ApplyBearerToken(request, settings),
            AuthenticationMode.Basic => ApplyBasicAuth(request, settings),
            AuthenticationMode.UsernamePassword => ApplyUsernamePassword(request, settings),
            _ => AuthenticationResult.Failed(
                settings.Mode,
                "UNSUPPORTED_AUTHENTICATION",
                $"Authentication mode '{settings.Mode}' is not yet implemented.")
        });
    }

    private AuthenticationResult ApplyApiKey(HttpRequestMessage request, AuthenticationSettings settings)
    {
        var apiKey = _credentialResolver.Resolve(settings.CredentialReference);
        request.Headers.Add("X-API-Key", apiKey);

        _logger.LogDebug("Applied API key authentication (mode={Mode})", AuthenticationMode.ApiKey);
        return AuthenticationResult.Succeeded(AuthenticationMode.ApiKey);
    }

    private AuthenticationResult ApplyBearerToken(HttpRequestMessage request, AuthenticationSettings settings)
    {
        var token = _credentialResolver.Resolve(settings.CredentialReference);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _logger.LogDebug("Applied Bearer token authentication");
        return AuthenticationResult.Succeeded(AuthenticationMode.BearerToken);
    }

    private AuthenticationResult ApplyBasicAuth(HttpRequestMessage request, AuthenticationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.UsernameReference) || string.IsNullOrWhiteSpace(settings.PasswordReference))
        {
            return AuthenticationResult.Failed(
                AuthenticationMode.Basic,
                "AUTHENTICATION_REQUIRED",
                "Basic authentication requires both username and password credential references.");
        }

        var username = _credentialResolver.Resolve(settings.UsernameReference);
        var password = _credentialResolver.Resolve(settings.PasswordReference);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return AuthenticationResult.Failed(
                AuthenticationMode.Basic,
                "AUTHENTICATION_REQUIRED",
                "Basic authentication credentials (username or password) are not configured.");
        }

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        _logger.LogDebug("Applied Basic authentication");
        return AuthenticationResult.Succeeded(AuthenticationMode.Basic);
    }

    private AuthenticationResult ApplyUsernamePassword(HttpRequestMessage request, AuthenticationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.UsernameReference) || string.IsNullOrWhiteSpace(settings.PasswordReference))
        {
            return AuthenticationResult.Failed(
                AuthenticationMode.UsernamePassword,
                "AUTHENTICATION_REQUIRED",
                "UsernamePassword authentication requires both username and password credential references.");
        }

        var username = _credentialResolver.Resolve(settings.UsernameReference);
        var password = _credentialResolver.Resolve(settings.PasswordReference);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return AuthenticationResult.Failed(
                AuthenticationMode.UsernamePassword,
                "AUTHENTICATION_REQUIRED",
                "Username/password credentials are not configured.");
        }

        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password)
        });

        request.Content = formContent;

        _logger.LogDebug("Applied UsernamePassword authentication");
        return AuthenticationResult.Succeeded(AuthenticationMode.UsernamePassword);
    }
}
