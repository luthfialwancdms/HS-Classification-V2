using HsClassifier.Domain.Enums;
namespace HsClassifier.Domain.Entities;

/// <summary>
/// One request to the AI provider. Metadata only: never the prompt, the response body, a key, or the goods
/// terms that were translated. Tokens are null when the provider did not report usage.
/// </summary>
public class AiUsageLog
{
    public long Id { get; set; }
    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public AiCallKind Kind { get; set; }
    /// <summary>"openai-compatible" (Groq or a proxy) or "gemini".</summary>
    public string Provider { get; set; } = string.Empty;
    /// <summary>Host of the endpoint only (no path, no credentials), to tell Groq from a proxy.</summary>
    public string? Host { get; set; }
    public string Model { get; set; } = string.Empty;
    /// <summary>Usually null: a new case is not saved yet when the AI is called. No foreign key.</summary>
    public long? CaseId { get; set; }
    public AiCallStatus Status { get; set; }
    /// <summary>Short code: HTTP_503, TIMEOUT, NETWORK, PARSE, CANCELLED, ERROR.</summary>
    public string? ErrorCode { get; set; }
    public int? HttpStatus { get; set; }
    public int LatencyMs { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
}
