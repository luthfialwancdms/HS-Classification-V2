using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;

namespace HsClassifier.Infrastructure.Services;

/// <summary>Case visibility + history filters, shared by the history list and single-case reads.</summary>
public static class CaseQueries
{
    /// <summary>Admin and ExpertReviewer see every case; everyone else only their own.</summary>
    public static bool SeesAll(IEnumerable<string> roles) =>
        roles.Any(r => r is nameof(UserRole.Admin) or nameof(UserRole.ExpertReviewer));

    public static IQueryable<ClassificationCase> Visible(this IQueryable<ClassificationCase> q, int userId, bool seesAll) =>
        seesAll ? q : q.Where(c => c.UserId == userId);

    /// <summary>History filters. q matches final code or the stored facts (goods names live in FactsJson).</summary>
    public static IQueryable<ClassificationCase> Filter(this IQueryable<ClassificationCase> q,
        CaseStatus? status, string? text, DateTime? from, DateTime? to)
    {
        if (status is not null) q = q.Where(c => c.Status == status);
        if (from is not null) q = q.Where(c => c.CreatedAt >= from);
        if (to is not null) q = q.Where(c => c.CreatedAt < to.Value.Date.AddDays(1)); // inclusive end date
        if (!string.IsNullOrWhiteSpace(text))
        {
            var t = text.Trim();
            q = q.Where(c => (c.FinalCode != null && c.FinalCode.Contains(t)) || c.FactsJson.Contains(t));
        }
        return q;
    }
}
