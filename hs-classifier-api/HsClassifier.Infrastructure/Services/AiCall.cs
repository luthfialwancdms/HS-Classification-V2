using System.Diagnostics;
using System.Text.Json;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// Metadata of one AI HTTP exchange. Deliberately holds no key, prompt, or response text,
/// so nothing sensitive can reach the usage log through it.
/// </summary>
public record AiCallInfo(string Provider, string? Host, int? HttpStatus, string? ErrorCode,
    int? PromptTokens, int? CompletionTokens, int? TotalTokens);

public record AiProbeResult(bool Ok, int LatencyMs, AiCallInfo Call);

/// <summary>An <see cref="ITermSuggester"/> that can also report its identity and run a tiny connection test.</summary>
public interface IAiProvider : ITermSuggester
{
    /// <summary>"openai-compatible" (Groq or a proxy) or "gemini".</summary>
    string ProviderName { get; }
    /// <summary>True when an API key is set. Never exposes the key itself.</summary>
    bool IsConfigured { get; }
    /// <summary>One minimal request (a few tokens). Success means an HTTP 2xx answer.</summary>
    Task<AiProbeResult> ProbeAsync(CancellationToken ct = default);
}

public static class AiCall
{
    public const string NoKey = "NO_KEY";
    public const string Timeout = "TIMEOUT";
    public const string Network = "NETWORK";
    public const string Parse = "PARSE";
    public const string Cancelled = "CANCELLED";
    public const string Error = "ERROR";

    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    public static string HttpError(int status) => $"HTTP_{status}";

    /// <summary>Host only; path and query (which could carry credentials on some proxies) are dropped.</summary>
    public static string? HostOf(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : null;

    public static (int? Prompt, int? Completion, int? Total) OpenAiUsage(JsonElement root) =>
        Usage(root, "usage", "prompt_tokens", "completion_tokens", "total_tokens");

    public static (int? Prompt, int? Completion, int? Total) GeminiUsage(JsonElement root) =>
        Usage(root, "usageMetadata", "promptTokenCount", "candidatesTokenCount", "totalTokenCount");

    private static (int?, int?, int?) Usage(JsonElement root, string obj, string prompt, string completion, string total)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(obj, out var u) || u.ValueKind != JsonValueKind.Object)
            return (null, null, null);
        int? Get(string name) =>
            u.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) && i >= 0 ? i : null;
        var (p, c, t) = (Get(prompt), Get(completion), Get(total));
        t ??= p is not null && c is not null ? p + c : null;
        return (p, c, t);
    }

    /// <summary>Short error code for an exception (the caller already rethrows its own cancellation).</summary>
    public static string Classify(Exception ex) => ex switch
    {
        OperationCanceledException => Timeout,
        HttpRequestException => Network,
        JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException => Parse,
        _ => Error,
    };

    /// <summary>Shared by both providers: send one small request with a hard timeout and keep metadata only.</summary>
    public static async Task<AiProbeResult> ProbeAsync(HttpClient http, HttpRequestMessage req, string provider, string? host,
        Func<JsonElement, (int? Prompt, int? Completion, int? Total)> usage, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ProbeTimeout);
        try
        {
            using var res = await http.SendAsync(req, cts.Token);
            var body = await res.Content.ReadAsStringAsync(cts.Token);
            sw.Stop();
            var status = (int)res.StatusCode;
            if (!res.IsSuccessStatusCode)
                return new(false, (int)sw.ElapsedMilliseconds, new(provider, host, status, HttpError(status), null, null, null));

            (int? P, int? C, int? T) tokens = (null, null, null);
            try { using var doc = JsonDocument.Parse(body); tokens = usage(doc.RootElement); }
            catch (JsonException) { /* 2xx is success; usage is optional */ }
            return new(true, (int)sw.ElapsedMilliseconds, new(provider, host, status, null, tokens.P, tokens.C, tokens.T));
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            if (ct.IsCancellationRequested) throw;
            sw.Stop();
            return new(false, (int)sw.ElapsedMilliseconds, new(provider, host, null, Classify(ex), null, null, null));
        }
    }
}

/// <summary>Admin-facing text for a short error code. Never echoes a response body or any credential.</summary>
public static class AiErrorMessages
{
    public static string? For(string? code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        if (code.StartsWith("HTTP_", StringComparison.Ordinal) && int.TryParse(code.AsSpan(5), out var http))
            return http switch
            {
                400 => "Permintaan ditolak server AI (HTTP 400). Periksa nama model dan parameter.",
                401 or 403 => $"Server AI menolak kredensial (HTTP {http}). Periksa API key.",
                404 => "Endpoint atau model tidak ditemukan (HTTP 404). Periksa BaseUrl dan nama model.",
                429 => "Batas permintaan AI tercapai (HTTP 429). Coba lagi nanti.",
                >= 500 => $"Server AI bermasalah (HTTP {http}).",
                _ => $"Server AI menjawab dengan kesalahan (HTTP {http}).",
            };
        return code switch
        {
            AiCall.NoKey => "API key belum dikonfigurasi di server.",
            AiCall.Timeout => "Server AI tidak menjawab dalam 10 detik.",
            AiCall.Network => "Tidak dapat terhubung ke server AI.",
            AiCall.Parse => "Jawaban server AI tidak dapat dibaca.",
            AiCall.Cancelled => "Permintaan dibatalkan.",
            _ => "Gagal memeriksa koneksi AI.",
        };
    }
}
