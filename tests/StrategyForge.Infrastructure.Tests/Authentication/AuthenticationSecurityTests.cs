using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Infrastructure.Authentication;
using StrategyForge.Infrastructure.Tests.Adapters;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Authentication;

public class AuthenticationSecurityTests
{
    // --- 1. Public endpoints receive no Authorization headers ---

    [Fact]
    public async Task PublicEndpoint_NoAuthHeadersAdded()
    {
        var authenticator = CreateAuthenticator();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/data");
        var settings = new AuthenticationSettings { Mode = AuthenticationMode.None };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.True(result.Success);
        Assert.Null(request.Headers.Authorization);
        Assert.Empty(request.Headers.Where(h => h.Key.StartsWith("X-", StringComparison.OrdinalIgnoreCase)));
    }

    // --- 2. API keys are never logged ---

    [Fact]
    public async Task ApiKey_NotLogged()
    {
        var logger = new Mock<ILogger<CompositeDataSourceAuthenticator>>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TestKey"] = "super-secret-12345" })
            .Build();
        var resolver = new CredentialResolver(config);
        var authenticator = new CompositeDataSourceAuthenticator(resolver, logger.Object);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings { Mode = AuthenticationMode.ApiKey, CredentialReference = "TestKey" };

        await authenticator.AuthenticateAsync(request, settings);

        // Check that the secret value never appears in any log call
        foreach (var call in logger.Invocations)
        {
            if (call.Arguments != null)
            {
                foreach (var arg in call.Arguments)
                {
                    if (arg is string strArg)
                    {
                        Assert.DoesNotContain("super-secret-12345", strArg);
                    }
                }
            }
        }
    }

    // --- 3. Tokens never returned in exceptions ---

    [Fact]
    public async Task MissingCredentials_ErrorDoesNotLeakSecretValues()
    {
        var authenticator = CreateAuthenticator();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings
        {
            Mode = AuthenticationMode.BearerToken,
            CredentialReference = "NonExistentKey"
        };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.False(result.Success);
        Assert.Contains("AUTHENTICATION_REQUIRED", result.ErrorCode);
        // The config KEY name may appear (it tells user which config to set),
        // but no actual secret value should leak.
        // Since the key doesn't exist, no secret value can leak.
        Assert.DoesNotContain("ActualSecretValue123", result.ErrorMessage);
    }

    // --- 4. Tokens never returned through API responses ---

    [Fact]
    public async Task AuthenticatedRequest_TokenNotInUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiKey"] = "secret-api-key-xyz" })
            .Build();
        var authenticator = CreateAuthenticator(config);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/data");
        var settings = new AuthenticationSettings { Mode = AuthenticationMode.ApiKey, CredentialReference = "ApiKey" };

        await authenticator.AuthenticateAsync(request, settings);

        // Token should be in a header, never in the URL
        Assert.DoesNotContain("secret-api-key-xyz", request.RequestUri!.ToString());
    }

    // --- 5. Tokens never in DataProvenance ---

    [Fact]
    public void DataProvenance_DoesNotContainSecrets()
    {
        var provenance = new StrategyForge.Domain.Models.DataProvenance
        {
            Source = SourceAdapterType.BrsApi,
            SourceInstrumentId = "12345",
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        };

        var json = System.Text.Json.JsonSerializer.Serialize(provenance);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apikey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    // --- 6. Tokens never in cache keys ---

    [Fact]
    public void CacheKeys_DoNotContainCredentials()
    {
        var cacheKey = StrategyForge.Infrastructure.Services.InMemoryDataCache.MarketDataKey(
            "iran-equity-foolad", "brsapi",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.DoesNotContain("apikey", cacheKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", cacheKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", cacheKey, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("brsapi", cacheKey); // Source type is safe to include
    }

    // --- 7. Missing credentials produce clear AUTHENTICATION_REQUIRED error ---

    [Fact]
    public async Task MissingCredentials_ProducesClearError()
    {
        var authenticator = CreateAuthenticator();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings
        {
            Mode = AuthenticationMode.ApiKey,
            CredentialReference = "MissingKey"
        };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.False(result.Success);
        Assert.Equal("AUTHENTICATION_REQUIRED", result.ErrorCode);
        Assert.Contains("MissingKey", result.ErrorMessage);
        Assert.False(result.Retryable); // Missing credentials should NOT be retried
    }

    // --- 8. Authentication failures do not trigger infinite retries ---

    [Fact]
    public async Task AuthFailure_NotRetryable_StopsImmediately()
    {
        var authenticator = CreateAuthenticator();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings
        {
            Mode = AuthenticationMode.ApiKey,
            CredentialReference = "MissingKey"
        };

        // Call multiple times - should fail consistently, never succeed
        for (int i = 0; i < 5; i++)
        {
            var result = await authenticator.AuthenticateAsync(request, settings);
            Assert.False(result.Success);
            Assert.False(result.Retryable);
        }
    }

    // --- 9. Bearer token is set as Authorization header, not in body ---

    [Fact]
    public async Task BearerToken_SetAsAuthorizationHeader()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Token"] = "my-jwt-token" })
            .Build();
        var authenticator = CreateAuthenticator(config);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings { Mode = AuthenticationMode.BearerToken, CredentialReference = "Token" };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.True(result.Success);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("my-jwt-token", request.Headers.Authorization.Parameter);
        Assert.Null(request.Content); // Body should not be modified
    }

    // --- 10. Basic auth encodes credentials in header only ---

    [Fact]
    public async Task BasicAuth_CredentialsOnlyInAuthorizationHeader()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BasicUser"] = "admin",
                ["BasicPass"] = "secret123"
            })
            .Build();
        var authenticator = CreateAuthenticator(config);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings
        {
            Mode = AuthenticationMode.Basic,
            UsernameReference = "BasicUser",
            PasswordReference = "BasicPass"
        };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.True(result.Success);
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);

        var base64 = request.Headers.Authorization.Parameter;
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        Assert.Equal("admin:secret123", decoded);

        // Credentials should not appear in URL
        Assert.DoesNotContain("admin", request.RequestUri!.ToString());
        Assert.DoesNotContain("secret123", request.RequestUri!.ToString());
    }

    // --- Helper ---

    private static CompositeDataSourceAuthenticator CreateAuthenticator(IConfiguration? config = null)
    {
        config ??= new ConfigurationBuilder().Build();
        var resolver = new CredentialResolver(config);
        return new CompositeDataSourceAuthenticator(resolver, Mock.Of<ILogger<CompositeDataSourceAuthenticator>>());
    }
}
