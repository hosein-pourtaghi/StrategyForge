using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Infrastructure.Authentication;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Authentication;

public class DataSourceAuthenticatorTests
{
    private CompositeDataSourceAuthenticator CreateAuthenticator(IConfiguration? config = null)
    {
        config ??= new ConfigurationBuilder().Build();
        var resolver = new CredentialResolver(config);
        return new CompositeDataSourceAuthenticator(resolver, Mock.Of<ILogger<CompositeDataSourceAuthenticator>>());
    }

    // --- None Mode ---

    [Fact]
    public async Task AuthenticateAsync_NoneMode_ReturnsSuccess()
    {
        var authenticator = CreateAuthenticator();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings { Mode = AuthenticationMode.None };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.True(result.Success);
        Assert.Equal(AuthenticationMode.None, result.Mode);
        Assert.Null(request.Headers.Authorization);
    }

    // --- API Key Mode ---

    [Fact]
    public async Task AuthenticateAsync_ApiKey_AttachesHeader()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestApiKey"] = "test-key-12345"
            })
            .Build();

        var authenticator = CreateAuthenticator(config);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings
        {
            Mode = AuthenticationMode.ApiKey,
            CredentialReference = "TestApiKey"
        };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.True(result.Success);
        Assert.Equal(AuthenticationMode.ApiKey, result.Mode);
        Assert.True(request.Headers.Contains("X-API-Key"));
        Assert.Equal("test-key-12345", request.Headers.GetValues("X-API-Key").First());
    }

    [Fact]
    public async Task AuthenticateAsync_ApiKey_MissingCredential_ReturnsFailure()
    {
        var authenticator = CreateAuthenticator();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings
        {
            Mode = AuthenticationMode.ApiKey,
            CredentialReference = "NonExistentKey"
        };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.False(result.Success);
        Assert.Equal("AUTHENTICATION_REQUIRED", result.ErrorCode);
        Assert.False(result.Retryable);
    }

    [Fact]
    public async Task AuthenticateAsync_ApiKey_NoCredentialReference_ReturnsFailure()
    {
        var authenticator = CreateAuthenticator();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings { Mode = AuthenticationMode.ApiKey };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.False(result.Success);
        Assert.Equal("AUTHENTICATION_REQUIRED", result.ErrorCode);
    }

    // --- Bearer Token Mode ---

    [Fact]
    public async Task AuthenticateAsync_BearerToken_AttachesAuthorization()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BearerToken"] = "my-bearer-token"
            })
            .Build();

        var authenticator = CreateAuthenticator(config);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings
        {
            Mode = AuthenticationMode.BearerToken,
            CredentialReference = "BearerToken"
        };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.True(result.Success);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("my-bearer-token", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task AuthenticateAsync_BearerToken_MissingCredential_ReturnsFailure()
    {
        var authenticator = CreateAuthenticator();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings
        {
            Mode = AuthenticationMode.BearerToken,
            CredentialReference = "NonExistent"
        };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.False(result.Success);
        Assert.Equal("AUTHENTICATION_REQUIRED", result.ErrorCode);
    }

    // --- Basic Auth Mode ---

    [Fact]
    public async Task AuthenticateAsync_Basic_AttachesAuthorizationHeader()
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
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret123"));
        Assert.Equal(expectedBase64, request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task AuthenticateAsync_Basic_MissingUsername_ReturnsFailure()
    {
        var authenticator = CreateAuthenticator();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings
        {
            Mode = AuthenticationMode.Basic,
            PasswordReference = "SomePass"
        };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.False(result.Success);
        Assert.Equal("AUTHENTICATION_REQUIRED", result.ErrorCode);
    }

    // --- Secret Safety Tests ---

    [Fact]
    public async Task AuthenticateAsync_Credentials_NotInHttpRequestUri()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecretKey"] = "top-secret-value"
            })
            .Build();

        var authenticator = CreateAuthenticator(config);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/data");
        var settings = new AuthenticationSettings
        {
            Mode = AuthenticationMode.ApiKey,
            CredentialReference = "SecretKey"
        };

        await authenticator.AuthenticateAsync(request, settings);

        Assert.DoesNotContain("top-secret-value", request.RequestUri!.ToString());
    }

    // --- Unsupported Mode ---

    [Fact]
    public async Task AuthenticateAsync_UnsupportedMode_ReturnsFailure()
    {
        var authenticator = CreateAuthenticator();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var settings = new AuthenticationSettings
        {
            Mode = AuthenticationMode.Hmac,
            CredentialReference = "SomeKey"
        };

        var result = await authenticator.AuthenticateAsync(request, settings);

        Assert.False(result.Success);
        Assert.Equal("UNSUPPORTED_AUTHENTICATION", result.ErrorCode);
    }
}
