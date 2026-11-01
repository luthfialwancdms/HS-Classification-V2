using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HsClassifier.Tests;

public class AiConnectionCheckerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 3, 0, 0, TimeSpan.Zero);

    private static (AiConnectionChecker Checker, FakeProvider Provider, ManualClock Clock, string Db) Setup(
        FakeProvider? provider = null, Dictionary<string, string?>? cfg = null)
    {
        provider ??= new FakeProvider();
        var db = Guid.NewGuid().ToString();
        var clock = new ManualClock(T0);
        var sp = AiTest.Services(db, more: s => s.AddScoped<IAiProvider>(_ => provider));
        var config = new ConfigurationBuilder().AddInMemoryCollection(cfg ?? []).Build();
        var recorder = new AiUsageRecorder(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<AiUsageRecorder>.Instance);
        var checker = new AiConnectionChecker(sp.GetRequiredService<IServiceScopeFactory>(), recorder, clock, config,
            NullLogger<AiConnectionChecker>.Instance);
        return (checker, provider, clock, db);
    }

    private static List<HsClassifier.Domain.Entities.AiUsageLog> Logs(string db) =>
        new HsClassifierDbContext(AiTest.NewOptions(db)).AiUsageLogs.ToList();

    [Fact]
    public async Task Nothing_is_known_before_the_first_check()
    {
        var (checker, _, _, _) = Setup();
        Assert.Null(checker.Last);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task First_check_runs_a_real_probe_and_is_logged_as_a_health_check()
    {
        var (checker, provider, _, db) = Setup();

        var o = await checker.CheckAsync();

        Assert.False(o.FromCache);
        Assert.Equal(1, provider.ProbeCalls);
        Assert.True(o.Status.Connected);
        Assert.Equal((120, "test-model", null), (o.Status.LatencyMs, o.Status.Model, o.Status.ErrorMessage));
        Assert.Same(o.Status, checker.Last);
        var log = Assert.Single(Logs(db));
        Assert.Equal((AiCallKind.HealthCheck, AiCallStatus.Success, 10), (log.Kind, log.Status, log.TotalTokens));
    }

    [Fact]
    public async Task A_second_check_inside_ten_seconds_returns_the_cached_result_without_calling_the_ai()
    {
        var (checker, provider, clock, db) = Setup();
        var first = await checker.CheckAsync();

        clock.Advance(TimeSpan.FromSeconds(4));
        var second = await checker.CheckAsync();

        Assert.True(second.FromCache);
        Assert.Same(first.Status, second.Status);
        Assert.InRange(second.RetryAfterSeconds, 1, 6);
        Assert.Equal(1, provider.ProbeCalls);
        Assert.Single(Logs(db)); // a throttled click is not an AI request
    }

    [Fact]
    public async Task After_ten_seconds_a_new_probe_is_allowed()
    {
        var (checker, provider, clock, _) = Setup();
        await checker.CheckAsync();

        clock.Advance(TimeSpan.FromSeconds(10));
        var again = await checker.CheckAsync();

        Assert.False(again.FromCache);
        Assert.Equal(2, provider.ProbeCalls);
    }

    [Fact]
    public async Task The_minimum_gap_is_configurable()
    {
        var (checker, provider, clock, _) = Setup(cfg: new() { ["Ai:CheckMinIntervalSeconds"] = "30" });
        await checker.CheckAsync();

        clock.Advance(TimeSpan.FromSeconds(20));
        Assert.True((await checker.CheckAsync()).FromCache);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.False((await checker.CheckAsync()).FromCache);
        Assert.Equal(2, provider.ProbeCalls);
    }

    [Fact]
    public async Task A_burst_of_simultaneous_clicks_makes_one_request()
    {
        var provider = new FakeProvider
        {
            OnProbe = async () =>
            {
                await Task.Delay(150);
                return new AiProbeResult(true, 150, new("openai-compatible", "api.groq.com", 200, null, 9, 1, 10));
            },
        };
        var (checker, _, _, _) = Setup(provider);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => checker.CheckAsync()));

        Assert.Equal(1, provider.ProbeCalls);
        Assert.Equal(1, outcomes.Count(o => !o.FromCache));
    }

    [Fact]
    public async Task A_failed_probe_reports_a_readable_message_and_is_logged_as_failed()
    {
        var provider = new FakeProvider
        {
            OnProbe = () => Task.FromResult(new AiProbeResult(false, 300, new("openai-compatible", "api.groq.com", 401, "HTTP_401", null, null, null))),
        };
        var (checker, _, _, db) = Setup(provider);

        var o = await checker.CheckAsync();

        Assert.False(o.Status.Connected);
        Assert.Equal("HTTP_401", o.Status.ErrorCode);
        Assert.Contains("API key", o.Status.ErrorMessage);
        var log = Assert.Single(Logs(db));
        Assert.Equal((AiCallStatus.Failed, "HTTP_401", 401), (log.Status, log.ErrorCode, log.HttpStatus));
    }

    [Fact]
    public async Task A_missing_key_is_reported_but_not_counted_as_a_request()
    {
        var provider = new FakeProvider
        {
            IsConfigured = false,
            OnProbe = () => Task.FromResult(new AiProbeResult(false, 0, new("openai-compatible", null, null, AiCall.NoKey, null, null, null))),
        };
        var (checker, _, _, db) = Setup(provider);

        var o = await checker.CheckAsync();

        Assert.False(o.Status.Connected);
        Assert.Null(o.Status.LatencyMs);
        Assert.Contains("belum dikonfigurasi", o.Status.ErrorMessage);
        Assert.Empty(Logs(db));
    }
}
