using HsClassifier.Domain.Classification;
using HsClassifier.Domain.Entities;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HsClassifier.Tests;

/// <summary>Test doubles for the AI usage tests. No network, no real database.</summary>
internal sealed class FakeProvider : IAiProvider
{
    public string Model { get; set; } = "test-model";
    public string ProviderName { get; set; } = "openai-compatible";
    public bool IsConfigured { get; set; } = true;
    public int ProbeCalls;

    public Func<Task<SuggestionResult>> OnSuggest { get; set; } = () => Task.FromResult(Ok());
    public Func<Task<AiProbeResult>> OnProbe { get; set; } = () => Task.FromResult(
        new AiProbeResult(true, 120, new("openai-compatible", "api.groq.com", 200, null, 9, 1, 10)));

    public static SuggestionResult Ok() => new(new TermSuggestion(["telepon"], ["8517"]), false,
        new AiCallInfo("openai-compatible", "api.groq.com", 200, null, 40, 12, 52));

    public Task<SuggestionResult> SuggestAsync(FactSet facts, CancellationToken ct = default) => OnSuggest();
    public int ChatCalls;
    public string? LastChatUser;
    public Func<Task<AiChatResult>> OnChat { get; set; } = () => Task.FromResult(
        new AiChatResult("{}", false, new("openai-compatible", "api.groq.com", 200, null, 30, 10, 40)));
    public Task<AiChatResult> ChatJsonAsync(string system, string user, CancellationToken ct = default)
    {
        Interlocked.Increment(ref ChatCalls);
        LastChatUser = user;
        return OnChat();
    }
    public Task<AiProbeResult> ProbeAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref ProbeCalls);
        return OnProbe();
    }
}

internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

internal sealed class CapturingRecorder : IAiUsageRecorder
{
    public List<AiUsageLog> Entries { get; } = [];
    public bool Throw { get; set; }
    public Task RecordAsync(AiUsageLog entry)
    {
        if (Throw) throw new InvalidOperationException("log store is down");
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}

/// <summary>A DbContext whose writes always fail, to prove log failures stay contained.</summary>
internal sealed class FailingDbContext(DbContextOptions<HsClassifierDbContext> options) : HsClassifierDbContext(options)
{
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new DbUpdateException("write failed");
}

internal static class AiTest
{
    public static DbContextOptions<HsClassifierDbContext> NewOptions(string? name = null) =>
        new DbContextOptionsBuilder<HsClassifierDbContext>().UseInMemoryDatabase(name ?? Guid.NewGuid().ToString()).Options;

    /// <summary>A service provider whose DbContext points at the named in-memory database.</summary>
    public static ServiceProvider Services(string dbName, bool failingWrites = false, Action<IServiceCollection>? more = null)
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddScoped<HsClassifierDbContext>(_ => failingWrites
            ? new FailingDbContext(NewOptions(dbName))
            : new HsClassifierDbContext(NewOptions(dbName)));
        more?.Invoke(s);
        return s.BuildServiceProvider();
    }

    public static LoggingTermSuggester Decorator(FakeProvider inner, IAiUsageRecorder rec, TimeProvider? clock = null) =>
        new(inner, rec, clock ?? TimeProvider.System, NullLogger<LoggingTermSuggester>.Instance);
}
