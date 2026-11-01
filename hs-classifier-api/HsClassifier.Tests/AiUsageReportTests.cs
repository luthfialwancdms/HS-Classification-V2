using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace HsClassifier.Tests;

public class AiUsageReportTests
{
    // 2026-11-02 10:00 WIB (UTC+7)
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 3, 0, 0, TimeSpan.Zero);

    private static AiUsageLog Row(string utc, AiCallKind kind = AiCallKind.TermSuggest, AiCallStatus status = AiCallStatus.Success,
        int? p = 10, int? c = 5, int? t = 15, string? error = null) => new()
    {
        CreatedAt = DateTime.SpecifyKind(DateTime.Parse(utc), DateTimeKind.Utc), Kind = kind, Status = status,
        Provider = "openai-compatible", Model = "m", PromptTokens = p, CompletionTokens = c, TotalTokens = t,
        ErrorCode = error, LatencyMs = 100,
    };

    private static (AiUsageReportService Svc, HsClassifierDbContext Db) Setup(Dictionary<string, string?>? cfg = null, params AiUsageLog[] rows)
    {
        var db = new HsClassifierDbContext(AiTest.NewOptions());
        db.AiUsageLogs.AddRange(rows);
        db.SaveChanges();
        var config = new ConfigurationBuilder().AddInMemoryCollection(cfg ?? []).Build();
        return (new AiUsageReportService(db, config, new ManualClock(Now)), db);
    }

    [Fact]
    public async Task Today_follows_wib_not_utc_and_splits_checks_from_classification()
    {
        var (svc, _) = Setup(null,
            Row("2026-11-01T16:59:00"),                                   // 23:59 WIB on 1 Nov → yesterday
            Row("2026-11-01T17:00:00"),                                   // 00:00 WIB on 2 Nov → today
            Row("2026-11-02T01:00:00", AiCallKind.HealthCheck),           // today, a connection check
            Row("2026-11-02T02:00:00", status: AiCallStatus.Failed, p: null, c: null, t: null, error: "HTTP_503"));

        var s = await svc.SummaryAsync();

        Assert.Equal(420, s.OffsetMinutes);
        Assert.Equal(3, s.Today.All.Requests);
        Assert.Equal((2, 1), (s.Today.All.Success, s.Today.All.Failed));
        Assert.Equal(2, s.Today.Classification.Requests);
        Assert.Equal(1, s.Today.Check.Requests);
        Assert.Equal((30, 10, 20), (s.Today.All.TotalTokens, s.Today.All.CompletionTokens, s.Today.All.PromptTokens));
        Assert.Equal(4, s.Last7Days.All.Requests); // yesterday is inside 7 days
    }

    [Fact]
    public async Task Windows_cover_7_30_and_all_time()
    {
        var (svc, _) = Setup(null,
            Row("2026-11-02T01:00:00"),   // today
            Row("2026-10-27T01:00:00"),   // 6 days ago → inside 7
            Row("2026-10-26T01:00:00"),   // 7 days ago → outside 7, inside 30
            Row("2026-10-03T01:00:00"),   // 30 days ago → outside 30
            Row("2026-01-01T01:00:00"));  // long ago → all time only

        var s = await svc.SummaryAsync();

        Assert.Equal(1, s.Today.All.Requests);
        Assert.Equal(2, s.Last7Days.All.Requests);
        Assert.Equal(3, s.Last30Days.All.Requests);
        Assert.Equal(5, s.AllTime.All.Requests);
    }

    [Fact]
    public async Task Per_day_rows_are_newest_first_and_zero_filled()
    {
        var (svc, _) = Setup(null, Row("2026-11-02T01:00:00"), Row("2026-10-31T05:00:00"));

        var s = await svc.SummaryAsync(days: 4);

        Assert.Equal(new[] { "2026-11-02", "2026-11-01", "2026-10-31", "2026-10-30" },
            s.PerDay.Select(d => d.Date.ToString("yyyy-MM-dd")).ToArray());
        Assert.Equal(new[] { 1, 0, 1, 0 }, s.PerDay.Select(d => d.Usage.All.Requests).ToArray());
    }

    [Fact]
    public async Task Successful_requests_without_token_data_are_counted_apart()
    {
        var (svc, _) = Setup(null, Row("2026-11-02T01:00:00"), Row("2026-11-02T01:30:00", p: null, c: null, t: null),
            Row("2026-11-02T02:00:00", status: AiCallStatus.Failed, p: null, c: null, t: null));

        Assert.Equal(1, (await svc.SummaryAsync()).SuccessWithoutTokenData);
    }

    [Fact]
    public async Task The_day_boundary_offset_is_configurable_and_days_are_clamped()
    {
        var (svc, _) = Setup(new() { ["Ai:ReportUtcOffsetMinutes"] = "0" }, Row("2026-11-01T23:00:00"));

        var s = await svc.SummaryAsync(days: 9999);

        Assert.Equal(0, s.OffsetMinutes);
        Assert.Equal(0, s.Today.All.Requests); // 23:00 UTC on 1 Nov is yesterday at offset 0
        Assert.Equal(AiUsageReportService.MaxDays, s.PerDay.Count);
    }

    [Fact]
    public async Task An_invalid_offset_falls_back_to_wib()
    {
        var (svc, _) = Setup(new() { ["Ai:ReportUtcOffsetMinutes"] = "99999" });
        Assert.Equal(420, svc.OffsetMinutes);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task List_is_newest_first_paged_and_filterable()
    {
        var rows = Enumerable.Range(0, 25).Select(i => Row($"2026-11-01T{i % 24:00}:00:00",
            status: i % 5 == 0 ? AiCallStatus.Failed : AiCallStatus.Success,
            kind: i % 7 == 0 ? AiCallKind.HealthCheck : AiCallKind.TermSuggest)).ToArray();
        var (svc, _) = Setup(null, rows);

        var p1 = await svc.ListAsync(null, null, null, null, page: 1, pageSize: 20);
        var p2 = await svc.ListAsync(null, null, null, null, page: 2, pageSize: 20);
        Assert.Equal((25, 20, 5), (p1.Total, p1.Items.Count, p2.Items.Count));
        Assert.True(p1.Items.Zip(p1.Items.Skip(1)).All(x => x.First.CreatedAtUtc >= x.Second.CreatedAtUtc));

        var failed = await svc.ListAsync(null, AiCallStatus.Failed, null, null);
        Assert.Equal(5, failed.Total);
        Assert.All(failed.Items, i => Assert.Equal(AiCallStatus.Failed, i.Status));

        var checks = await svc.ListAsync(AiCallKind.HealthCheck, null, null, null);
        Assert.Equal(4, checks.Total);
    }

    [Fact]
    public async Task List_date_filter_uses_report_days_with_an_inclusive_end()
    {
        var (svc, _) = Setup(null,
            Row("2026-11-01T16:00:00"),   // 23:00 WIB 1 Nov
            Row("2026-11-01T17:00:00"),   // 00:00 WIB 2 Nov
            Row("2026-11-02T16:59:00"),   // 23:59 WIB 2 Nov
            Row("2026-11-02T17:00:00"));  // 00:00 WIB 3 Nov

        var day2 = await svc.ListAsync(null, null, new DateTime(2026, 11, 2), new DateTime(2026, 11, 2));

        Assert.Equal(2, day2.Total);
    }

    [Fact]
    public async Task Page_size_is_clamped()
    {
        var (svc, _) = Setup();
        Assert.Equal(100, (await svc.ListAsync(null, null, null, null, pageSize: 5000)).PageSize);
        Assert.Equal(1, (await svc.ListAsync(null, null, null, null, page: -3)).Page);
    }
}
