using HsClassifier.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HsClassifier.Infrastructure.Services;

public record AiStatusSnapshot(bool Connected, string Provider, string Model, int? LatencyMs,
    DateTime CheckedAtUtc, string? ErrorCode, string? ErrorMessage);

public record AiCheckOutcome(AiStatusSnapshot Status, bool FromCache, int RetryAfterSeconds);

/// <summary>
/// Active connection test with a minimum gap between real requests (default 10 s, <c>Ai:CheckMinIntervalSeconds</c>).
/// Calls inside the gap get the last result from memory. One check runs at a time, so a burst of clicks
/// makes one request. The gap is per server instance.
/// </summary>
public class AiConnectionChecker(IServiceScopeFactory scopes, IAiUsageRecorder recorder, TimeProvider clock,
    IConfiguration config, ILogger<AiConnectionChecker> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile AiStatusSnapshot? _last;
    private DateTimeOffset _lastAt;

    private TimeSpan MinInterval =>
        TimeSpan.FromSeconds(int.TryParse(config["Ai:CheckMinIntervalSeconds"], out var s) && s >= 1 ? s : 10);

    /// <summary>Result of the last real check since the server started; null if none yet.</summary>
    public AiStatusSnapshot? Last => _last;

    public async Task<AiCheckOutcome> CheckAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var gap = MinInterval;
            var elapsed = clock.GetUtcNow() - _lastAt;
            if (_last is not null && elapsed < gap)
                return new(_last, FromCache: true, (int)Math.Ceiling((gap - elapsed).TotalSeconds));

            using var scope = scopes.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IAiProvider>();
            var probe = await provider.ProbeAsync(ct);
            var done = clock.GetUtcNow();
            var call = probe.Call;

            // A missing key makes no request, so there is nothing to count; the snapshot still tells the admin why.
            if (call.ErrorCode != AiCall.NoKey)
            {
                try
                {
                    await recorder.RecordAsync(AiUsageRecorder.Build(AiCallKind.HealthCheck, provider.ProviderName,
                        provider.Model, call, probe.Ok, probe.LatencyMs, done.UtcDateTime));
                }
                catch (Exception ex) { logger.LogWarning("AI check log failed: {Error}", ex.GetType().Name); }
            }

            var snapshot = new AiStatusSnapshot(probe.Ok, provider.ProviderName, provider.Model,
                call.ErrorCode == AiCall.NoKey ? null : probe.LatencyMs, done.UtcDateTime,
                probe.Ok ? null : call.ErrorCode, probe.Ok ? null : AiErrorMessages.For(call.ErrorCode));
            _last = snapshot;
            _lastAt = done;
            return new(snapshot, FromCache: false, (int)gap.TotalSeconds);
        }
        finally { _gate.Release(); }
    }
}
