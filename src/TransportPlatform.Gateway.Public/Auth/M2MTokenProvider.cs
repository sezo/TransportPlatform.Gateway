using Microsoft.Extensions.Caching.Distributed;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace TransportPlatform.Gateway.Public.Auth;

public interface IM2MTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct = default);
}

/// <summary>
/// DEMO STUB — returns a hardcoded token.
/// Not suitable for production.
///
/// For production, replace with KeycloakM2MTokenProvider below.
/// Register in Program.cs:
///   builder.Services.AddSingleton{IM2MTokenProvider, KeycloakM2MTokenProvider}();
/// </summary>
public class StubM2MTokenProvider : IM2MTokenProvider
{
    public Task<string> GetTokenAsync(CancellationToken ct = default)
        => Task.FromResult("dev-m2m-token-replace-in-production");
}

/// <summary>
/// PRODUCTION implementation.
/// Fetches a gateway service account token from Keycloak using client_credentials flow.
/// Token is cached in Redis until 30 seconds before expiry.
/// Shared across all gateway instances via Redis — single token fetch per expiry window.
/// </summary>
public class KeycloakM2MTokenProvider(
    IHttpClientFactory httpClientFactory,
    IDistributedCache cache,
    IConfiguration config,
    ILogger<KeycloakM2MTokenProvider> logger) : IM2MTokenProvider
{
    private const string CacheKey = "gateway-public:m2m-token";

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        // Try Redis cache first
        var cached = await cache.GetStringAsync(CacheKey, ct);
        if (cached is not null)
            return cached;

        // Cache miss — fetch from Keycloak
        logger.LogInformation("Fetching new M2M token from Keycloak");

        var client = httpClientFactory.CreateClient();
        var response = await client.PostAsync(
            config["Keycloak:TokenEndpoint"],
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = config["Keycloak:GatewayClientId"]!,
                ["client_secret"] = config["Keycloak:GatewayClientSecret"]!
            }), ct);

        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content
            .ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty token response from Keycloak");

        // Cache with 30s buffer before actual expiry
        var expiry = TimeSpan.FromSeconds(tokenResponse.ExpiresIn - 30);
        await cache.SetStringAsync(CacheKey, tokenResponse.AccessToken,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            }, ct);

        logger.LogInformation(
            "M2M token cached for {Seconds}s", expiry.TotalSeconds);

        return tokenResponse.AccessToken;
    }
}

internal record KeycloakTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
