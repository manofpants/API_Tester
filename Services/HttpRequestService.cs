using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace ApiTester.Services;

public enum AuthMode
{
    None,
    Bearer,
    ApiKey
}

public enum ApiKeyLocation
{
    Header,
    Query
}

public sealed class ApiRequest
{
    public required string Method { get; init; }
    public required string Url { get; init; }
    public required IReadOnlyList<(string Key, string Value)> Headers { get; init; }
    public string? Body { get; init; }
    public AuthMode AuthMode { get; init; }
    public string? BearerToken { get; init; }
    public string? ApiKey { get; init; }
    public string? ApiSecret { get; init; }
    public ApiKeyLocation ApiKeyLocation { get; init; }
}

public sealed class ApiResponse
{
    public bool IsError { get; init; }
    public int? StatusCode { get; init; }
    public string? ReasonPhrase { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public long ElapsedMilliseconds { get; init; }
    public IReadOnlyList<(string Key, string Value)> Headers { get; init; } = Array.Empty<(string, string)>();
    public string Body { get; init; } = string.Empty;
    public string? ContentType { get; init; }
}

public sealed class HttpRequestService : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(100)
    };

    public async Task<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return new ApiResponse
            {
                IsError = true,
                StatusText = "Error",
                Body = "URL is required."
            };
        }

        if (!Uri.TryCreate(request.Url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new ApiResponse
            {
                IsError = true,
                StatusText = "Error",
                Body = "Enter a valid absolute http:// or https:// URL."
            };
        }

        try
        {
            uri = ApplyApiKeyQuery(uri, request);

            using var message = new HttpRequestMessage(new HttpMethod(request.Method), uri);
            ApplyHeaders(message, request);
            ApplyAuth(message, request);
            ApplyBody(message, request);

            var stopwatch = Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            stopwatch.Stop();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString();
            var headers = CollectHeaders(response);

            return new ApiResponse
            {
                IsError = false,
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase,
                StatusText = $"{(int)response.StatusCode} {response.ReasonPhrase} · {stopwatch.ElapsedMilliseconds} ms",
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Headers = headers,
                Body = body,
                ContentType = contentType
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ApiResponse
            {
                IsError = true,
                StatusText = "Error",
                Body = "The request timed out."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse
            {
                IsError = true,
                StatusText = "Error",
                Body = ex.Message
            };
        }
    }

    private const string ApiKeyHeaderName = "X-API-Key";
    private const string ApiSecretHeaderName = "X-API-Secret";
    private const string ApiKeyQueryName = "api_key";
    private const string ApiSecretQueryName = "api_secret";

    private static Uri ApplyApiKeyQuery(Uri uri, ApiRequest request)
    {
        if (request.AuthMode != AuthMode.ApiKey ||
            request.ApiKeyLocation != ApiKeyLocation.Query)
        {
            return uri;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            parts.Add($"{ApiKeyQueryName}={Uri.EscapeDataString(request.ApiKey.Trim())}");
        if (!string.IsNullOrWhiteSpace(request.ApiSecret))
            parts.Add($"{ApiSecretQueryName}={Uri.EscapeDataString(request.ApiSecret)}");

        if (parts.Count == 0)
            return uri;

        var builder = new UriBuilder(uri);
        var existing = builder.Query.TrimStart('?');
        var added = string.Join("&", parts);
        builder.Query = string.IsNullOrEmpty(existing) ? added : $"{existing}&{added}";
        return builder.Uri;
    }

    private static void ApplyHeaders(HttpRequestMessage message, ApiRequest request)
    {
        var skipAuthorization = request.AuthMode == AuthMode.Bearer;
        var skipApiKeyHeaders = request.AuthMode == AuthMode.ApiKey &&
                                request.ApiKeyLocation == ApiKeyLocation.Header;

        foreach (var (key, value) in request.Headers)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var headerName = key.Trim();

            if (skipAuthorization &&
                headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (skipApiKeyHeaders &&
                (headerName.Equals(ApiKeyHeaderName, StringComparison.OrdinalIgnoreCase) ||
                 headerName.Equals(ApiSecretHeaderName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (IsContentHeader(headerName))
            {
                // Content headers are applied with the body.
                continue;
            }

            if (!message.Headers.TryAddWithoutValidation(headerName, value))
            {
                // Ignore invalid header names rather than failing the whole request.
            }
        }
    }

    private static void ApplyAuth(HttpRequestMessage message, ApiRequest request)
    {
        switch (request.AuthMode)
        {
            case AuthMode.Bearer when !string.IsNullOrWhiteSpace(request.BearerToken):
                message.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", request.BearerToken.Trim());
                break;

            case AuthMode.ApiKey when request.ApiKeyLocation == ApiKeyLocation.Header:
                if (!string.IsNullOrWhiteSpace(request.ApiKey))
                    message.Headers.TryAddWithoutValidation(ApiKeyHeaderName, request.ApiKey.Trim());
                if (!string.IsNullOrWhiteSpace(request.ApiSecret))
                    message.Headers.TryAddWithoutValidation(ApiSecretHeaderName, request.ApiSecret);
                break;
        }
    }

    private static void ApplyBody(HttpRequestMessage message, ApiRequest request)
    {
        var method = request.Method.ToUpperInvariant();
        var allowsBody = method is "POST" or "PUT" or "PATCH" or "DELETE";
        if (!allowsBody)
            return;

        var contentType = request.Headers
            .FirstOrDefault(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            .Value;

        if (string.IsNullOrWhiteSpace(contentType))
            contentType = "application/json";

        var body = request.Body ?? string.Empty;
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        message.Content = content;
    }

    private static bool IsContentHeader(string headerName) =>
        headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
        headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
        headerName.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) ||
        headerName.Equals("Content-Language", StringComparison.OrdinalIgnoreCase) ||
        headerName.Equals("Content-Location", StringComparison.OrdinalIgnoreCase) ||
        headerName.Equals("Content-MD5", StringComparison.OrdinalIgnoreCase) ||
        headerName.Equals("Content-Range", StringComparison.OrdinalIgnoreCase) ||
        headerName.Equals("Expires", StringComparison.OrdinalIgnoreCase) ||
        headerName.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<(string Key, string Value)> CollectHeaders(HttpResponseMessage response)
    {
        var headers = new List<(string, string)>();

        foreach (var header in response.Headers)
        {
            headers.Add((header.Key, string.Join(", ", header.Value)));
        }

        foreach (var header in response.Content.Headers)
        {
            headers.Add((header.Key, string.Join(", ", header.Value)));
        }

        return headers;
    }

    public void Dispose() => _httpClient.Dispose();
}
