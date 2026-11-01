using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HsClassifier.Infrastructure.Services;

public record AiUsageCounts(int Requests, int Success, int Failed, long PromptTokens, long CompletionTokens, long TotalTokens);

/// <summary>Totals include connection checks; <c>Classification</c> and <c>Check</c> split them so clicks don't inflate classification use.</summary>
public record AiUsageBlock(AiUsageCounts All, AiUsageCounts Classification, AiUsageCounts Check);

public record AiUsageDay(DateOnly Date, AiUsageBlock Usage);

public record AiUsageSummary(int OffsetMinutes, AiUsageBlock Today, AiUsageBlock Last7Days, AiUsageBlock Last30Days,
    AiUsageBlock AllTime, List<AiUsageDay> PerDay, int SuccessWithoutTokenData);

public record AiRequestItem(long Id, DateTime CreatedAtUtc, AiCallKind Kind, string Provider, string? Host, string Model,
    long? CaseId, AiCallStatus Status, string? ErrorCode, int? HttpStatus, int LatencyMs,
    int? PromptTokens, int? CompletionTokens, int? TotalTokens);

public record AiRequestPage(int Total, int Page, int PageSize, List<AiRequestItem> Items);

/// <summary>
/// Reads the usage log. "Today" and day buckets follow a fixed UTC offset
/// (<c>Ai:ReportUtcOffsetMinutes</c>, default 420 = WIB). Windows are calendar days including today.
/// </summary>
public class AiUsageReportService(HsClassifierDbContext db, IConfiguration config, TimeProvider clock)
{
    public const int MaxDays = 90;

    public int OffsetMinutes =>
        int.TryParse(config["Ai:ReportUtcOffsetMinutes"], out var m) && m is >= -720 and <= 840 ? m : 420;

    private record Agg(AiCallKind Kind, AiCallStatus Status, int Count, long Prompt, long Completion, long Total);

    public async Task<AiUsageSummary> SummaryAsync(int days = 30, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, MaxDays);
        var offset = TimeSpan.FromMinutes(OffsetMinutes);
        var today = DateOnly.FromDateTime((clock.GetUtcNow().UtcDateTime + offset));
        var fetchDays = Math.Max(30, days);
        DateTime StartUtc(DateOnly d) => d.ToDateTime(TimeOnly.MinValue) - offset;

        // Recent window: a few thousand narrow rows at most, bucketed here (robust across providers).
        var from = StartUtc(today.AddDays(-(fetchDays - 1)));
        var rows = await db.AiUsageLogs.AsNoTracking().Where(e => e.CreatedAt >= from)
            .Select(e => new { e.CreatedAt, e.Kind, e.Status, e.PromptTokens, e.CompletionTokens, e.TotalTokens })
            .ToListAsync(ct);
        var local = rows.Select(r => (Day: DateOnly.FromDateTime(r.CreatedAt + offset), Row: r)).ToList();

        AiUsageBlock Window(int span) => Fold(local.Where(x => x.Day > today.AddDays(-span))
            .GroupBy(x => (x.Row.Kind, x.Row.Status))
            .Select(g => new Agg(g.Key.Kind, g.Key.Status, g.Count(),
                g.Sum(x => (long)(x.Row.PromptTokens ?? 0)), g.Sum(x => (long)(x.Row.CompletionTokens ?? 0)),
                g.Sum(x => (long)(x.Row.TotalTokens ?? 0)))));

        var perDay = Enumerable.Range(0, days).Select(i => today.AddDays(-i)).Select(d =>
            new AiUsageDay(d, Fold(local.Where(x => x.Day == d).GroupBy(x => (x.Row.Kind, x.Row.Status))
                .Select(g => new Agg(g.Key.Kind, g.Key.Status, g.Count(),
                    g.Sum(x => (long)(x.Row.PromptTokens ?? 0)), g.Sum(x => (long)(x.Row.CompletionTokens ?? 0)),
                    g.Sum(x => (long)(x.Row.TotalTokens ?? 0))))))).ToList();

        // All time: aggregated in the database so the log can grow without growing this call.
        var all = await db.AiUsageLogs.AsNoTracking().GroupBy(e => new { e.Kind, e.Status })
            .Select(g => new
            {
                g.Key.Kind, g.Key.Status, Count = g.Count(),
                Prompt = g.Sum(e => (long?)e.PromptTokens) ?? 0,
                Completion = g.Sum(e => (long?)e.CompletionTokens) ?? 0,
                Total = g.Sum(e => (long?)e.TotalTokens) ?? 0,
            }).ToListAsync(ct);
        var noTokens = await db.AiUsageLogs.CountAsync(e => e.Status == AiCallStatus.Success && e.TotalTokens == null, ct);

        return new(OffsetMinutes, Window(1), Window(7), Window(30),
            Fold(all.Select(a => new Agg(a.Kind, a.Status, a.Count, a.Prompt, a.Completion, a.Total))),
            perDay, noTokens);
    }

    public async Task<AiRequestPage> ListAsync(AiCallKind? kind, AiCallStatus? status, DateTime? from, DateTime? to,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = TimeSpan.FromMinutes(OffsetMinutes);

        var q = db.AiUsageLogs.AsNoTracking().AsQueryable();
        if (kind is not null) q = q.Where(e => e.Kind == kind);
        if (status is not null) q = q.Where(e => e.Status == status);
        // from/to are calendar days in the report time zone; "to" is inclusive.
        if (from is not null) { var f = from.Value.Date - offset; q = q.Where(e => e.CreatedAt >= f); }
        if (to is not null) { var t = to.Value.Date.AddDays(1) - offset; q = q.Where(e => e.CreatedAt < t); }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new AiRequestItem(e.Id, e.CreatedAt, e.Kind, e.Provider, e.Host, e.Model, e.CaseId, e.Status,
                e.ErrorCode, e.HttpStatus, e.LatencyMs, e.PromptTokens, e.CompletionTokens, e.TotalTokens))
            .ToListAsync(ct);
        return new(total, page, pageSize, items);
    }

    private static AiUsageBlock Fold(IEnumerable<Agg> aggs)
    {
        var list = aggs.ToList();
        return new(Sum(list), Sum(list.Where(a => a.Kind == AiCallKind.TermSuggest)), Sum(list.Where(a => a.Kind == AiCallKind.HealthCheck)));
    }

    private static AiUsageCounts Sum(IEnumerable<Agg> aggs)
    {
        var l = aggs.ToList();
        return new(l.Sum(a => a.Count), l.Where(a => a.Status == AiCallStatus.Success).Sum(a => a.Count),
            l.Where(a => a.Status == AiCallStatus.Failed).Sum(a => a.Count),
            l.Sum(a => a.Prompt), l.Sum(a => a.Completion), l.Sum(a => a.Total));
    }
}
