using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HsClassifier.Infrastructure.Services;

/// <summary>Writes AI usage rows. Implementations must never throw: logging can't be allowed to break a request.</summary>
public interface IAiUsageRecorder
{
    Task RecordAsync(AiUsageLog entry);
}

/// <summary>
/// Uses its own scope and DbContext. The caller's DbContext may hold half-finished changes of a
/// classification; saving through it would persist them early, so it is never touched here.
/// </summary>
public class AiUsageRecorder(IServiceScopeFactory scopes, ILogger<AiUsageRecorder> logger) : IAiUsageRecorder
{
    public async Task RecordAsync(AiUsageLog entry)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HsClassifierDbContext>();
            db.AiUsageLogs.Add(entry);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Type name only: exception text can carry connection details.
            logger.LogWarning("AI usage log could not be written: {Error}", ex.GetType().Name);
        }
    }

    /// <summary>Builds a row from call metadata. Only metadata goes in; the goods terms are deliberately not stored.</summary>
    public static AiUsageLog Build(AiCallKind kind, string fallbackProvider, string model, AiCallInfo? call,
        bool success, int latencyMs, DateTime utcNow, string? errorCodeOverride = null) => new()
    {
        CreatedAt = utcNow,
        Kind = kind,
        Provider = call?.Provider ?? fallbackProvider,
        Host = call?.Host,
        Model = model,
        Status = success ? AiCallStatus.Success : AiCallStatus.Failed,
        ErrorCode = success ? null : errorCodeOverride ?? call?.ErrorCode ?? AiCall.Error,
        HttpStatus = call?.HttpStatus,
        LatencyMs = Math.Max(0, latencyMs),
        PromptTokens = call?.PromptTokens,
        CompletionTokens = call?.CompletionTokens,
        TotalTokens = call?.TotalTokens,
    };
}
