using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// Google Gemini (generativelanguage API). Same prompt and output contract as Groq; only the
/// transport differs — system prompt as systemInstruction, key via X-goog-api-key header,
/// JSON forced with responseMimeType.
/// </summary>
public class GeminiTermSuggester(IConfiguration config, ILogger<GeminiTermSuggester> logger) : IAiProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private const string Host = "generativelanguage.googleapis.com";

    public string Model => config["Gemini:Model"] ?? "gemini-flash-lite-latest";
    public string ProviderName => "gemini";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(config["Gemini:ApiKey"]);
    private string Endpoint => $"https://{Host}/v1beta/models/{Model}:generateContent";

    /// <summary>Tiny generateContent request: checks key, model, and endpoint exactly as real use does.</summary>
    public async Task<AiProbeResult> ProbeAsync(CancellationToken ct = default)
    {
        var key = config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            return new(false, 0, new(ProviderName, Host, null, AiCall.NoKey, null, null, null));

        var body = JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = "ping" } } } },
            generationConfig = new { temperature = 0, maxOutputTokens = 16 },
        });
        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-goog-api-key", key);
        return await AiCall.ProbeAsync(Http, req, ProviderName, Host, AiCall.GeminiUsage, ct);
    }

    public async Task<SuggestionResult> SuggestAsync(FactSet facts, CancellationToken ct = default)
    {
        var key = config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogWarning("Gemini:ApiKey not configured — AI term fallback unavailable.");
            return new(null, Unavailable: true, Call: new(ProviderName, Host, null, AiCall.NoKey, null, null, null));
        }

        var endpoint = Endpoint;
        var body = JsonSerializer.Serialize(new
        {
            systemInstruction = new { parts = new[] { new { text = TermSuggesterPrompt.System } } },
            contents = new[] { new { parts = new[] { new { text = TermSuggesterPrompt.UserMessage(facts) } } } },
            generationConfig = new { temperature = 0, responseMimeType = "application/json" },
        });

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("X-goog-api-key", key);
            using var res = await Http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini call failed with HTTP {Status}.", (int)res.StatusCode);
                return new(null, Unavailable: true,
                    Call: new(ProviderName, Host, (int)res.StatusCode, AiCall.HttpError((int)res.StatusCode), null, null, null));
            }

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var content = doc.RootElement.GetProperty("candidates")[0]
                .GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            var usage = AiCall.GeminiUsage(doc.RootElement);
            return new(GroqTermSuggester.Log(logger, "Gemini", TermSuggesterPrompt.Parse(content)), Unavailable: false,
                Call: new(ProviderName, Host, (int)res.StatusCode, null, usage.Prompt, usage.Completion, usage.Total));
        }
        catch (Exception ex) when (GroqTermSuggester.IsTransient(ex))
        {
            if (ct.IsCancellationRequested) throw;
            logger.LogWarning("Gemini call failed: {Error}", ex.GetType().Name);
            return new(null, Unavailable: true, Call: new(ProviderName, Host, null, AiCall.Classify(ex), null, null, null));
        }
    }
}
