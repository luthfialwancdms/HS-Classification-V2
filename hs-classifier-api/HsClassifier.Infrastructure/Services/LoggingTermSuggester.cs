using System.Diagnostics;
using HsClassifier.Domain.Classification;
using HsClassifier.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// The single point where every classification-time AI request is logged. It returns exactly what the
/// provider returned, rethrows exactly what the provider threw, and swallows any logging failure, so
/// classification behaves as if it were not here.
/// </summary>
public class LoggingTermSuggester(IAiProvider inner, IAiUsageRecorder recorder, TimeProvider clock,
    ILogger<LoggingTermSuggester> logger) : ITermSuggester
{
    public string Model => inner.Model;

    public async Task<SuggestionResult> SuggestAsync(FactSet facts, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        SuggestionResult result;
        try
        {
            result = await inner.SuggestAsync(facts, ct);
        }
        catch (OperationCanceledException)
        {
            await SafeRecordAsync(null, success: false, sw, AiCall.Cancelled);
            throw;
        }
        catch (Exception)
        {
            await SafeRecordAsync(null, success: false, sw, AiCall.Error);
            throw;
        }

        // No key means no request was made; it is not a usage event (the status card shows it instead).
        if (result.Call?.ErrorCode != AiCall.NoKey)
            await SafeRecordAsync(result.Call, success: !result.Unavailable && result.Call?.ErrorCode is null, sw);
        return result;
    }

    private async Task SafeRecordAsync(AiCallInfo? call, bool success, Stopwatch sw, string? errorCode = null)
    {
        try
        {
            sw.Stop();
            var entry = AiUsageRecorder.Build(AiCallKind.TermSuggest, inner.ProviderName, inner.Model, call, success,
                (int)sw.ElapsedMilliseconds, clock.GetUtcNow().UtcDateTime, errorCode);
            await recorder.RecordAsync(entry);
        }
        catch (Exception ex)
        {
            logger.LogWarning("AI usage logging failed: {Error}", ex.GetType().Name);
        }
    }
}
