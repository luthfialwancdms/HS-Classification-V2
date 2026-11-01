using HsClassifier.Domain.Entities;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HsClassifier.Infrastructure.Services;

public record QuotaStatus(bool Allowed, int Limit, int Used, DateTimeOffset ResetAt);

/// <summary>
/// Daily cap on new classifications per user (DATABASE_SCHEMA.md daily_usage; the 10 vs 50 per day figure is
/// still open, so it is configuration: <c>Quota:DailyClassifications</c>, default 10, 0 = unlimited).
/// Admin and ExpertReviewer are exempt. Only starting a case counts; answering clarification questions does not.
/// The day follows a fixed UTC offset (<c>Quota:UtcOffsetMinutes</c>, else <c>Ai:ReportUtcOffsetMinutes</c>, default 420 = WIB).
/// </summary>
public class DailyQuotaService(HsClassifierDbContext db, IConfiguration config, TimeProvider clock)
{
    private const int MaxAttempts = 5;

    public int Limit => int.TryParse(config["Quota:DailyClassifications"], out var n) && n >= 0 ? n : 10;

    private TimeSpan Offset => TimeSpan.FromMinutes(
        int.TryParse(config["Quota:UtcOffsetMinutes"] ?? config["Ai:ReportUtcOffsetMinutes"], out var m) && m is >= -720 and <= 840 ? m : 420);

    private static bool Exempt(string role) => role is "Admin" or "ExpertReviewer";

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime + Offset);

    private DateTimeOffset ResetAt() =>
        new(Today().AddDays(1).ToDateTime(TimeOnly.MinValue) - Offset, TimeSpan.Zero);

    /// <summary>Current usage without counting anything.</summary>
    public async Task<QuotaStatus> GetAsync(int userId, string role, CancellationToken ct = default)
    {
        if (Exempt(role) || Limit == 0) return new(true, 0, 0, ResetAt());
        var used = await db.DailyUsages.AsNoTracking()
            .Where(u => u.UserId == userId && u.Day == Today()).Select(u => u.Count).FirstOrDefaultAsync(ct);
        return new(used < Limit, Limit, used, ResetAt());
    }

    /// <summary>Counts one classification if the user is under the limit. A refused try is not counted.</summary>
    public async Task<QuotaStatus> TryConsumeAsync(int userId, string role, CancellationToken ct = default)
    {
        var limit = Limit;
        if (Exempt(role) || limit == 0) return new(true, 0, 0, ResetAt());

        var day = Today();
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var row = await db.DailyUsages.FindAsync([userId, day], ct);
            if (row is null) db.DailyUsages.Add(row = new DailyUsage { UserId = userId, Day = day, Count = 1 });
            else if (row.Count >= limit) return new(false, limit, row.Count, ResetAt());
            else row.Count++;

            try
            {
                await db.SaveChangesAsync(ct);
                return new(true, limit, row.Count, ResetAt());
            }
            catch (DbUpdateException) // lost a race (duplicate key or changed Count): reload and decide again
            {
                db.ChangeTracker.Clear();
            }
        }
        // ponytail: fail closed after repeated conflicts; a user retrying a moment later gets through.
        return new(false, limit, limit, ResetAt());
    }
}
