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
public class GeminiTermSuggester(IConfiguration config, ILogger<GeminiTermSuggester> logger) : ITermSuggester
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public string Model => config["Gemini:Model"] ?? "gemini-flash-lite-latest";

    public async Task<SuggestionResult> SuggestAsync(FactSet facts, CancellationToken ct = default)
    {
        var key = config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogWarning("Gemini:ApiKey not configured — AI term fallback unavailable.");
            return new(null, Unavailable: true);
        }

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent";
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
                return new(null, Unavailable: true);
            }

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var content = doc.RootElement.GetProperty("candidates")[0]
                .GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            return new(GroqTermSuggester.Log(logger, "Gemini", TermSuggesterPrompt.Parse(content)), Unavailable: false);
        }
        catch (Exception ex) when (GroqTermSuggester.IsTransient(ex))
        {
            if (ct.IsCancellationRequested) throw;
            logger.LogWarning("Gemini call failed: {Error}", ex.GetType().Name);
            return new(null, Unavailable: true);
        }
    }
}
