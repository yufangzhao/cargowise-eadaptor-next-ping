using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EadaptorNextPing.Services;

public sealed record TokenResult(string AccessToken, DateTimeOffset ObtainedAt, DateTimeOffset ExpiresAt)
{
    public string ExpiresInText
    {
        get
        {
            var remaining = ExpiresAt - DateTimeOffset.Now;
            if (remaining <= TimeSpan.Zero) return "expired";
            return remaining.TotalMinutes >= 1
                ? $"{(int)remaining.TotalMinutes} min"
                : $"{(int)remaining.TotalSeconds} s";
        }
    }
}

public sealed class TokenProvider
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(100),
        DefaultRequestHeaders = { }
    };

    private readonly object _lock = new();
    private TokenResult? _cached;
    private string _cachedKey = string.Empty;

    public TokenResult? CachedToken
    {
        get
        {
            lock (_lock)
            {
                return _cached is not null && _cached.ExpiresAt > DateTimeOffset.Now.AddSeconds(30) ? _cached : null;
            }
        }
    }

    public async Task<TokenResult> GetTokenAsync(
        string tokenEndpoint, string clientId, string scope, X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenEndpoint))
            throw new ArgumentException("The token endpoint is required.");
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("The client ID is required.");

        var cacheKey = string.Join("|", tokenEndpoint, clientId, scope, certificate.Thumbprint);
        var cached = CachedToken;
        if (cached is not null && string.Equals(_cachedKey, cacheKey, StringComparison.Ordinal))
            return cached;

        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa is null)
            throw new ArgumentException(
                "The certificate must contain an RSA private key - the JWT client assertion is signed with RS256.");

        var assertion = BuildClientAssertion(certificate, rsa, tokenEndpoint.Trim(), clientId.Trim());

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId.Trim(),
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"] = assertion
        };
        if (!string.IsNullOrWhiteSpace(scope))
            form["scope"] = scope.Trim();

        using var content = new FormUrlEncodedContent(form);
        using var response = await Http.PostAsync(tokenEndpoint.Trim(), content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Token request failed with HTTP {(int)response.StatusCode}: {Truncate(body, 500)}");

        string accessToken;
        int expiresIn;
        try
        {
            using var doc = JsonDocument.Parse(body);
            accessToken = doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
            expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var e) ? e : 3600;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("The token response is not valid JSON: " + Truncate(body, 500));
        }

        if (accessToken.Length == 0)
            throw new InvalidOperationException("The token response did not contain an access_token.");

        var result = new TokenResult(
            accessToken,
            DateTimeOffset.Now,
            DateTimeOffset.Now.AddSeconds(Math.Max(expiresIn - 60, 60)));

        lock (_lock)
        {
            _cached = result;
            _cachedKey = cacheKey;
        }
        return result;
    }

    public void ClearCache()
    {
        lock (_lock)
        {
            _cached = null;
            _cachedKey = string.Empty;
        }
    }

    public static string NormalizeTokenEndpoint(string url)
    {
        var trimmed = (url ?? string.Empty).Trim().TrimEnd('/');
        if (trimmed.Length == 0) return trimmed;

        if (trimmed.EndsWith("/token", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (trimmed.EndsWith("/v2.0", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith("/v1.0", StringComparison.OrdinalIgnoreCase))
        {
            var index = Math.Max(
                trimmed.LastIndexOf("/v2.0", StringComparison.OrdinalIgnoreCase),
                trimmed.LastIndexOf("/v1.0", StringComparison.OrdinalIgnoreCase));
            return trimmed.Substring(0, index) + "/oauth2" + trimmed.Substring(index) + "/token";
        }

        if (trimmed.EndsWith("/oauth2/v2.0", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith("/oauth2/v1.0", StringComparison.OrdinalIgnoreCase))
            return trimmed + "/token";

        if (trimmed.EndsWith("/oauth2", StringComparison.OrdinalIgnoreCase))
            return trimmed + "/v2.0/token";

        return trimmed;
    }

    public static bool NeedsNormalization(string url)
    {
        return !string.Equals(
            NormalizeTokenEndpoint(url), (url ?? string.Empty).Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildClientAssertion(X509Certificate2 certificate, RSA rsa, string audience, string clientId)
    {
        var now = DateTimeOffset.UtcNow;

        var header = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["x5t"] = EncodeBase64Url(SHA1.HashData(certificate.RawData))
        });

        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["aud"] = audience,
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["nbf"] = now.AddSeconds(-60).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["exp"] = now.AddMinutes(10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
        });

        var unsigned = EncodeSegment(header) + "." + EncodeSegment(payload);
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return unsigned + "." + EncodeBase64Url(signature);
    }

    private static string EncodeSegment(string json)
        => EncodeBase64Url(Encoding.UTF8.GetBytes(json));

    private static string EncodeBase64Url(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value.Substring(0, max) + "...";
    }
}
