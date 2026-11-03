using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using EadaptorNextPing.Models;

namespace EadaptorNextPing.Services;

public sealed record SendResult(
    int StatusCode,
    double DurationMs,
    string Body,
    string HeadersText,
    string UniversalStatus,
    string ProcessingLog,
    string Error)
{
    public bool HasError => !string.IsNullOrEmpty(Error);
    public string StatusText => HasError ? "ERR" : StatusCode > 0 ? StatusCode.ToString() : "-";
    public bool Ok => !HasError && StatusCode is >= 200 and < 300
        && !string.Equals(UniversalStatus, "ERR", StringComparison.OrdinalIgnoreCase);
    public string DurationText => DurationMs <= 0 ? "-" : $"{DurationMs:F0} ms";
}

public static class CargoClient
{
    public static readonly TokenProvider Tokens = new();

    public static async Task<TokenResult> TestAuthAsync(AppSettings settings)
    {
        using var certificate = CertLoader.LoadForAuth(settings);
        var endpoint = ResolveTokenEndpoint(settings);
        return await Tokens.GetTokenAsync(endpoint, settings.ClientId, ResolveScope(settings), certificate);
    }

    public static async Task<SendResult> SendAsync(AppSettings settings, string body)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("The request body is empty - paste or load a Universal XML file first.");

            var endpointUrl = ResolveEndpoint(settings);

            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds))
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
            request.Headers.Accept.ParseAdd("text/xml");

            if (settings.AuthMode == AuthMode.OAuth)
            {
                var token = Tokens.CachedToken;
                if (token is null)
                {
                    using var certificate = CertLoader.LoadForAuth(settings);
                    token = await Tokens.GetTokenAsync(ResolveTokenEndpoint(settings), settings.ClientId, ResolveScope(settings), certificate);
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            }
            else if (settings.AuthMode == AuthMode.Basic)
            {
                if (string.IsNullOrEmpty(settings.BasicUser))
                    throw new ArgumentException("Basic authentication requires a username.");
                var credentials = Convert.ToBase64String(Encoding.Latin1.GetBytes($"{settings.BasicUser}:{settings.BasicPassword}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
            content.Headers.TryAddWithoutValidation("Content-Type", "text/xml; charset=utf-8");
            request.Content = content;

            using var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            var headersText = DescribeHeaders(response);

            return new SendResult(
                (int)response.StatusCode,
                sw.Elapsed.TotalMilliseconds,
                responseBody,
                headersText,
                XmlHelper.ParseUniversalStatus(responseBody) ?? string.Empty,
                XmlHelper.ParseProcessingLog(responseBody) ?? string.Empty,
                string.Empty);
        }
        catch (Exception ex)
        {
            return new SendResult(
                0,
                sw.Elapsed.TotalMilliseconds,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                DescribeError(ex));
        }
    }

    private static string ResolveEndpoint(AppSettings settings)
    {
        var url = settings.Url?.Trim() ?? string.Empty;
        if (url.Length == 0)
            throw new ArgumentException("The endpoint URL is required.");

        var enterprise = settings.EnterpriseCode?.Trim() ?? string.Empty;
        var server = settings.ServerCode?.Trim() ?? string.Empty;
        if (enterprise.Length > 0)
        {
            url = url.Replace("{enterprisecode}", enterprise, StringComparison.OrdinalIgnoreCase);
            url = url.Replace("{Enterprise Code}", enterprise, StringComparison.OrdinalIgnoreCase);
        }
        if (server.Length > 0)
        {
            url = url.Replace("{servercode}", server, StringComparison.OrdinalIgnoreCase);
            url = url.Replace("{Server Code}", server, StringComparison.OrdinalIgnoreCase);
        }

        if (url.Contains("{enterprisecode}", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("{servercode}", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("{Enterprise Code}", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("{Server Code}", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "The endpoint URL still contains {enterprisecode} or {servercode} - fill in both codes so they can be resolved.");

        if (settings.UseAsync && !url.EndsWith("/Async", StringComparison.OrdinalIgnoreCase))
            url = url.TrimEnd('/') + "/Async";
        return url;
    }

    private static string ResolveScope(AppSettings settings)
    {
        var scope = settings.Scope?.Trim() ?? string.Empty;
        var clientId = settings.ClientId?.Trim() ?? string.Empty;
        if (clientId.Length > 0)
            scope = scope.Replace("{clientid}", clientId, StringComparison.OrdinalIgnoreCase);
        return scope;
    }

    private static string ResolveTokenEndpoint(AppSettings settings)
    {
        var endpoint = settings.AutoFixTokenUrl
            ? TokenProvider.NormalizeTokenEndpoint(settings.TokenEndpoint)
            : settings.TokenEndpoint.Trim();
        var clientId = settings.ClientId?.Trim() ?? string.Empty;
        if (clientId.Length > 0)
            endpoint = endpoint.Replace("{clientid}", clientId, StringComparison.OrdinalIgnoreCase);
        if (endpoint.Contains("{clientid}", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "The token endpoint still contains {clientid} - enter the Client ID so it can be resolved.");
        return endpoint;
    }

    private static string DescribeHeaders(HttpResponseMessage response)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"HTTP/1.1 {(int)response.StatusCode} {response.StatusCode}");
        foreach (var header in response.Headers)
            sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        foreach (var header in response.Content.Headers)
            sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        return sb.ToString().TrimEnd();
    }

    private static string DescribeError(Exception ex) => ex switch
    {
        TaskCanceledException => "The request timed out or was cancelled.",
        HttpRequestException http => http.Message,
        System.Security.Authentication.AuthenticationException tls => "TLS handshake failed: " + tls.Message,
        InvalidOperationException invalid => invalid.Message,
        _ => ex.Message
    };
}
