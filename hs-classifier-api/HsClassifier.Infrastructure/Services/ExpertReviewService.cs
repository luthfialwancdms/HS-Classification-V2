using System.Text.Json;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Services;

public record ReviewOutcome(bool Ok, string? Error, string? Action = null, string? FinalCode = null, string? FinalDescription = null)
{
    public static ReviewOutcome Fail(string error) => new(false, error);
}

/// <summary>
/// Expert decisions on a case. Every decision is recorded twice: an ExpertCorrection row
/// (before/after, reason, reviewer) and an append-only EXPERT_REVIEW reasoning event.
/// An approved code must still pass the DB validator — experts choose, the BTKI database authorises.
/// </summary>
public class ExpertReviewService(HsClassifierDbContext db, ClassificationValidator validator)
{
    public const string Approved = "EXPERT_APPROVED";
    public const string Override = "EXPERT_OVERRIDE";
    public const string Rejected = "EXPERT_REJECTED";
    public const string Requested = "REVIEW_REQUESTED";
    private const int MinReasonLength = 5;

    /// <summary>
    /// A code is "from the candidates" when it equals a candidate or falls under a candidate
    /// heading/subheading (candidates may be 4-digit headings when KUM HS 6 stopped early).
    /// Anything else is an override.
    /// </summary>
    public static bool IsCandidate(string code, IEnumerable<string> candidates) =>
        candidates.Any(c => !string.IsNullOrWhiteSpace(c) && code.StartsWith(c.Trim(), StringComparison.Ordinal));

    public async Task<ReviewOutcome> ApproveAsync(long caseId, string? code, string? reason, int reviewerId, CancellationToken ct = default)
    {
        if (ReasonError(reason) is { } re) return ReviewOutcome.Fail(re);
        code = code?.Trim();
        if (string.IsNullOrEmpty(code)) return ReviewOutcome.Fail("Kode HS wajib diisi.");

        var c = await db.ClassificationCases.FindAsync([caseId], ct);
        if (c is null) return ReviewOutcome.Fail("Case tidak ditemukan.");
        if (c.Status != CaseStatus.NeedExpertReview)
            return ReviewOutcome.Fail($"Hanya case berstatus NeedExpertReview yang bisa diputuskan (status sekarang: {c.Status}).");

        var (isValid, failure, officialDesc) = await validator.ValidateAsync(code, ct);
        if (!isValid) return ReviewOutcome.Fail($"Kode ditolak validator: {failure}");

        var action = IsCandidate(code, Candidates(c)) ? Approved : Override;
        await RecordAsync(c, action, code, officialDesc, CaseStatus.Determined, reason!, reviewerId, "Retained", ct);
        return new(true, null, action, code, officialDesc);
    }

    public async Task<ReviewOutcome> RejectAsync(long caseId, string? reason, int reviewerId, CancellationToken ct = default)
    {
        if (ReasonError(reason) is { } re) return ReviewOutcome.Fail(re);
        var c = await db.ClassificationCases.FindAsync([caseId], ct);
        if (c is null) return ReviewOutcome.Fail("Case tidak ditemukan.");
        if (c.Status != CaseStatus.NeedExpertReview)
            return ReviewOutcome.Fail($"Hanya case berstatus NeedExpertReview yang bisa ditolak (status sekarang: {c.Status}).");

        await RecordAsync(c, Rejected, null, null, CaseStatus.Rejected, reason!, reviewerId, "Eliminated", ct);
        return new(true, null, Rejected);
    }

    /// <summary>
    /// Owner sends a determined case to the expert queue. The current code is kept as a candidate
    /// (so the expert can confirm it in one click) and cleared as final until the expert decides.
    /// </summary>
    public async Task<ReviewOutcome> RequestReviewAsync(long caseId, string? reason, int userId, CancellationToken ct = default)
    {
        if (ReasonError(reason) is { } re) return ReviewOutcome.Fail(re);
        var c = await db.ClassificationCases.FindAsync([caseId], ct);
        if (c is null) return ReviewOutcome.Fail("Case tidak ditemukan.");
        if (c.Status != CaseStatus.Determined)
            return ReviewOutcome.Fail($"Hanya case berstatus Determined yang bisa diajukan ke ahli (status sekarang: {c.Status}).");

        var candidates = Candidates(c);
        if (c.FinalCode is not null && !candidates.Contains(c.FinalCode)) candidates.Insert(0, c.FinalCode);
        c.CandidateCodesJson = JsonSerializer.Serialize(candidates);

        await RecordAsync(c, Requested, null, null, CaseStatus.NeedExpertReview, reason!, userId, "Unresolved", ct);
        return new(true, null, Requested);
    }

    private static string? ReasonError(string? reason) =>
        (reason?.Trim().Length ?? 0) < MinReasonLength ? $"Alasan wajib diisi (minimal {MinReasonLength} karakter)." : null;

    private static List<string> Candidates(ClassificationCase c) =>
        c.CandidateCodesJson is null ? [] : JsonSerializer.Deserialize<List<string>>(c.CandidateCodesJson) ?? [];

    // ponytail: status check + save is not atomic; two reviewers deciding the same case at the same
    //           instant could both succeed. Upgrade: rowversion concurrency token on ClassificationCase.
    private async Task RecordAsync(ClassificationCase c, string action, string? newCode, string? newDesc,
        CaseStatus newStatus, string reason, int actorId, string decision, CancellationToken ct)
    {
        var before = (Code: c.FinalCode, Status: c.Status);

        c.FinalCode = newCode;
        c.FinalDescription = newDesc;
        c.Status = newStatus;
        c.Confidence = newStatus == CaseStatus.Determined ? ConfidenceLevel.High : null;
        // The engine's score described the engine's answer; after an expert decision it no longer applies
        c.ConfidenceScore = null;
        c.ConfidenceJson = action == Override
            ? JsonSerializer.Serialize(new { components = Array.Empty<object>(), caps = Array.Empty<string>(), riskFlags = new[] { "EXPERT_OVERRIDE" } })
            : null;
        c.ClassificationDate = DateTime.UtcNow;

        db.ExpertCorrections.Add(new ExpertCorrection
        {
            CaseId = c.Id, Action = action, ReviewerId = actorId, Reason = reason.Trim(),
            BeforeCode = before.Code, AfterCode = newCode,
            BeforeStatus = before.Status.ToString(), AfterStatus = newStatus.ToString(),
        });

        var nextOrder = (await db.ReasoningEvents.Where(e => e.CaseId == c.Id)
            .MaxAsync(e => (int?)e.EventOrder, ct) ?? 0) + 1;
        db.ReasoningEvents.Add(new ReasoningEvent
        {
            CaseId = c.Id, EventOrder = nextOrder, RuleApplied = "EXPERT_REVIEW", Decision = decision,
            Reasoning = $"[{action}] {(newCode is null ? "" : newCode + " — ")}{reason.Trim()}",
            SourceIdsJson = JsonSerializer.Serialize(new[] { $"user:{actorId}" }),
        });

        await db.SaveChangesAsync(ct);
    }
}
