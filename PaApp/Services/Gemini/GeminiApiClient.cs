using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PaApp.Services.Gemini;

public interface IGeminiApiClient
{
    Task<JsonDocument> GenerateContentAsync(JsonObject requestBody, CancellationToken cancellationToken = default);
}

public sealed class GeminiApiClient : IGeminiApiClient
{
    private const int MaxAttempts = 5;

    // Do not use camelCase naming here: JsonObject keys must match Gemini REST (e.g. google_search, functionDeclarations).
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiApiClient> _logger;

    public GeminiApiClient(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JsonDocument> GenerateContentAsync(JsonObject requestBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Gemini API key is not configured. Set GEMINI_API_KEY (or GOOGLE_API_KEY).");

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var req = CreateRequest(requestBody);
            using var res = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (res.IsSuccessStatusCode)
                return JsonDocument.Parse(json);

            var detail = ExtractErrorDetail(json);
            var canRetry = attempt < MaxAttempts && IsTransientStatusCode(res.StatusCode);
            if (canRetry)
            {
                var delay = ComputeBackoffBeforeRetry(attempt, res);
                _logger.LogWarning(
                    "Gemini generateContent returned {Status}; retry {Attempt}/{Max} after {DelayMs}ms. {Detail}",
                    (int)res.StatusCode,
                    attempt,
                    MaxAttempts,
                    (int)delay.TotalMilliseconds,
                    detail);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            throw new HttpRequestException($"Gemini API error {(int)res.StatusCode}: {detail}");
        }

        throw new InvalidOperationException("Gemini retry loop exited unexpectedly.");
    }

    private HttpRequestMessage CreateRequest(JsonObject requestBody)
    {
        var model = Uri.EscapeDataString(_options.Model.Trim());
        var req = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{model}:generateContent");
        req.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
        req.Content = new StringContent(
            requestBody.ToJsonString(SerializerOptions),
            System.Text.Encoding.UTF8,
            "application/json");
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return req;
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static TimeSpan ComputeBackoffBeforeRetry(int failedAttempt, HttpResponseMessage res)
    {
        if (res.Headers.RetryAfter?.Delta is { TotalMilliseconds: > 0 } delta &&
            delta <= TimeSpan.FromMinutes(2))
            return delta;

        var baseMs = 1000d * Math.Pow(2, failedAttempt - 1);
        var capped = Math.Min(baseMs, 30_000d);
        var jitterMs = Random.Shared.Next(0, 500);
        return TimeSpan.FromMilliseconds(capped + jitterMs);
    }

    private static string ExtractErrorDetail(string json)
    {
        try
        {
            using var err = JsonDocument.Parse(json);
            if (err.RootElement.TryGetProperty("error", out var e) &&
                e.TryGetProperty("message", out var m) &&
                m.ValueKind == JsonValueKind.String)
            {
                return m.GetString() ?? json;
            }
        }
        catch
        {
            // keep raw body
        }

        return json;
    }
}
