using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HsClassifier.Domain.Classification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HsClassifier.Infrastructure.Services;

/// <summary>AI proposal: generic BTKI-style terms + 4-digit headings. Never a final code.</summary>
public record TermSuggestion(List<string> Terms, List<string> Headings);

/// <summary>Suggestion is null when the AI answered but gave nothing usable; Unavailable = no key / error / timeout.</summary>
public record SuggestionResult(TermSuggestion? Suggestion, bool Unavailable);

/// <summary>
/// Fallback when the DB search finds no heading for market/brand names ("handphone samsung").
/// Only translates wording to BTKI terminology; candidates are validated against the DB and the
/// final code still comes from KUM HS 1-6 (Req 7.2, 17.1). Provider (Groq/Gemini) is config-selectable.
/// </summary>
public interface ITermSuggester
{
    string Model { get; }
    Task<SuggestionResult> SuggestAsync(FactSet facts, CancellationToken ct = default);
}

/// <summary>Shared prompt, fact selection, and tolerant output parsing for every AI provider.</summary>
public static class TermSuggesterPrompt
{
    public const string System =
        "You map goods descriptions to official Indonesian BTKI / HS 2022 terminology. " +
        "Ignore brand names and model numbers; classify by what the goods are and do. " +
        "Return ONLY a JSON object: {\"terms\": [up to 5 generic terms as worded in BTKI, Indonesian and/or English], " +
        "\"headings\": [up to 3 four-digit HS headings, e.g. \"8517\"]}. " +
        "Never return 6- or 8-digit codes. If unsure, return fewer items.";

    private static readonly string[] GoodsFacts =
        ["commercial_name", "generic_name", "primary_function", "material", "primary_use", "power_source", "form"];

    /// <summary>Only goods facts leave the server — no user or case data.</summary>
    public static string UserMessage(FactSet facts) => string.Join("\n", GoodsFacts
        .Select(f => (f, v: facts.GetValue(f)))
        .Where(x => !string.IsNullOrWhiteSpace(x.v))
        .Select(x => $"{x.f}: {x.v}"));

    private static readonly Regex HeadingFormat = new(@"^(\d{2})\.?(\d{2})$");

    /// <summary>Tolerant parse of model output; anything that is not a 4-digit heading is rejected.</summary>
    public static TermSuggestion? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        try
        {
            using var doc = JsonDocument.Parse(content);
            List<string> Strings(string name) =>
                doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array
                    ? arr.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!.Trim()).ToList()
                    : [];

            var terms = Strings("terms").Where(t => t.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();
            var headings = Strings("headings")
                .Select(h => HeadingFormat.Match(h)).Where(m => m.Success)
                .Select(m => m.Groups[1].Value + m.Groups[2].Value)
                .Distinct().Take(3).ToList();

            return terms.Count == 0 && headings.Count == 0 ? null : new TermSuggestion(terms, headings);
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>
/// Any OpenAI-compatible chat-completions endpoint. Defaults to Groq, but BaseUrl/Model/ApiKey are
/// config-driven so an OpenAI-compatible proxy (e.g. Dattio) works by changing config only.
/// </summary>
public class GroqTermSuggester(IConfiguration config, ILogger<GroqTermSuggester> logger) : ITermSuggester
{
    // ponytail: one static HttpClient instead of IHttpClientFactory (avoids a new package);
    //           upgrade path: services.AddHttpClient<GroqTermSuggester>() for handler rotation/policies.
    // 30s: proxy models (Dattio) can take ~13s; this call only runs when the DB search already failed.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    // Trailing-slash tolerant: accepts ".../v1" or ".../v1/"
    private string BaseUrl => (config["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1").TrimEnd('/');
    private string Endpoint => $"{BaseUrl}/chat/completions";

    public string Model => config["Groq:Model"] ?? "openai/gpt-oss-120b";

    public async Task<SuggestionResult> SuggestAsync(FactSet facts, CancellationToken ct = default)
    {
        var key = config["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogWarning("Groq:ApiKey not configured — AI term fallback unavailable.");
            return new(null, Unavailable: true);
        }

        var messages = new object[]
        {
            new { role = "system", content = TermSuggesterPrompt.System },
            new { role = "user", content = TermSuggesterPrompt.UserMessage(facts) },
        };
        // response_format is a real-Groq feature; some proxies reject it. The prompt already forces JSON.
        object payload = BaseUrl.Contains("api.groq.com", StringComparison.OrdinalIgnoreCase)
            ? new { model = Model, temperature = 0, response_format = new { type = "json_object" }, messages }
            : new { model = Model, temperature = 0, messages };
        var body = JsonSerializer.Serialize(payload);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var res = await Http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning("AI call ({BaseUrl}) failed with HTTP {Status}.", BaseUrl, (int)res.StatusCode);
                return new(null, Unavailable: true);
            }

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return new(Log(logger, "OpenAI-compatible", TermSuggesterPrompt.Parse(content)), Unavailable: false);
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            if (ct.IsCancellationRequested) throw;
            logger.LogWarning("AI call ({BaseUrl}) failed: {Error}", BaseUrl, ex.GetType().Name);
            return new(null, Unavailable: true);
        }
    }

    internal static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or JsonException
           or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException;

    internal static TermSuggestion? Log(ILogger logger, string provider, TermSuggestion? s)
    {
        logger.LogInformation("{Provider} term suggestion: {Terms} / {Headings}", provider,
            s is null ? "-" : string.Join(", ", s.Terms), s is null ? "-" : string.Join(", ", s.Headings));
        return s;
    }

    /// <summary>Kept for the existing test; delegates to the shared parser.</summary>
    public static TermSuggestion? Parse(string? content) => TermSuggesterPrompt.Parse(content);
}
