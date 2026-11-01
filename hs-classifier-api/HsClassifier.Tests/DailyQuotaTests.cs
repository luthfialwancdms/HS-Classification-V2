using HsClassifier.Domain.Entities;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HsClassifier.Tests;

public class DailyQuotaTests
{
    // 2026-11-02 10:00 WIB = 03:00 UTC. WIB midnight is 17:00 UTC.
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 3, 0, 0, TimeSpan.Zero);

    private static DailyQuotaService Service(string dbName, ManualClock clock, string? limit = "3")
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(
            limit is null ? [] : new Dictionary<string, string?> { ["Quota:DailyClassifications"] = limit }).Build();
        return new DailyQuotaService(new HsClassifierDbContext(AiTest.NewOptions(dbName)), cfg, clock);
    }

    [Fact]
    public async Task Limit_is_enforced_and_the_blocked_try_is_not_counted()
    {
        var db = Guid.NewGuid().ToString();
        var q = Service(db, new ManualClock(T0));
        for (var i = 1; i <= 3; i++)
        {
            var ok = await q.TryConsumeAsync(7, "Classifier");
            Assert.True(ok.Allowed);
            Assert.Equal(i, ok.Used);
        }
        var blocked = await q.TryConsumeAsync(7, "Classifier");
        Assert.False(blocked.Allowed);
        Assert.Equal(3, blocked.Used);
        Assert.Equal(3, blocked.Limit);
        Assert.Equal(3, (await q.GetAsync(7, "Classifier")).Used);
    }

    [Fact]
    public async Task Counts_are_per_user()
    {
        var db = Guid.NewGuid().ToString();
        var q = Service(db, new ManualClock(T0), "1");
        Assert.True((await q.TryConsumeAsync(1, "Classifier")).Allowed);
        Assert.False((await q.TryConsumeAsync(1, "Classifier")).Allowed);
        Assert.True((await q.TryConsumeAsync(2, "Classifier")).Allowed);
    }

    [Fact]
    public async Task New_day_in_wib_resets_the_count()
    {
        var db = Guid.NewGuid().ToString();
        var clock = new ManualClock(T0);
        var q = Service(db, clock, "1");
        Assert.True((await q.TryConsumeAsync(1, "Classifier")).Allowed);
        clock.Advance(TimeSpan.FromHours(13));            // 23:00 WIB, same day
        Assert.False((await q.TryConsumeAsync(1, "Classifier")).Allowed);
        clock.Advance(TimeSpan.FromHours(2));             // 01:00 WIB next day
        Assert.True((await q.TryConsumeAsync(1, "Classifier")).Allowed);
    }

    [Fact]
    public async Task Reset_time_is_the_next_wib_midnight()
    {
        var q = Service(Guid.NewGuid().ToString(), new ManualClock(T0), "1");
        var r = await q.TryConsumeAsync(1, "Classifier");
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 17, 0, 0, TimeSpan.Zero), r.ResetAt);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("ExpertReviewer")]
    public async Task Admin_and_expert_are_not_limited_and_not_counted(string role)
    {
        var db = Guid.NewGuid().ToString();
        var q = Service(db, new ManualClock(T0), "1");
        for (var i = 0; i < 5; i++) Assert.True((await q.TryConsumeAsync(1, role)).Allowed);
        Assert.Equal(0, await new HsClassifierDbContext(AiTest.NewOptions(db)).DailyUsages.CountAsync());
    }

    [Fact]
    public async Task Zero_limit_means_unlimited_and_default_is_10()
    {
        var unlimited = Service(Guid.NewGuid().ToString(), new ManualClock(T0), "0");
        for (var i = 0; i < 20; i++) Assert.True((await unlimited.TryConsumeAsync(1, "Classifier")).Allowed);

        var def = Service(Guid.NewGuid().ToString(), new ManualClock(T0), null);
        Assert.Equal(10, (await def.GetAsync(1, "Classifier")).Limit);
    }

    [Fact]
    public async Task Two_requests_racing_on_the_same_row_cannot_both_pass_the_limit()
    {
        var db = Guid.NewGuid().ToString();
        // limit 1: both contexts read Count=0, only one write may win; the loser retries and is refused
        var a = Service(db, new ManualClock(T0), "1");
        var b = Service(db, new ManualClock(T0), "1");
        var results = await Task.WhenAll(a.TryConsumeAsync(1, "Classifier"), b.TryConsumeAsync(1, "Classifier"));
        Assert.Equal(1, results.Count(r => r.Allowed));
    }
}
