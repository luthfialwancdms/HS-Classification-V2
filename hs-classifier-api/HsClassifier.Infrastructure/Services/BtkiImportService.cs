using System.Text.Json;
using System.Text.RegularExpressions;
using HsClassifier.Application.Interfaces;
using HsClassifier.Application.Services;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// BTKI DOCX import: upload → persisted validation report (ImportBatch) + ambiguity queue →
/// admin decisions → commit gated on "no errors, nothing pending, same file" (Req 1.4, 1.9, 1.11).
/// </summary>
public class BtkiImportService(HsClassifierDbContext db, IVersionManager versionManager)
{
    public async Task<DryRunResult> RunDryRunAsync(List<ParsedTariffNode> nodes, int? expectedTotal = null)
        => await Task.FromResult(BtkiStructureService.DryRun(nodes, expectedTotal));

    /// <summary>Latest batch of a version — the one that governs commit and activation.</summary>
    public Task<ImportBatch?> LatestBatchAsync(Guid versionId, CancellationToken ct = default) =>
        db.ImportBatches.Include(b => b.Items)
            .Where(b => b.VersionId == versionId)
            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Persist the dry run of an upload against a version and walk the state machine:
    /// Uploaded→Parsing→Mapped→Validating (or DryRunFailed→Validating on re-upload) → DryRunPassed/Failed,
    /// then ReadyToCommit straight away when there is nothing to review.
    /// </summary>
    public async Task<(ImportBatch? Batch, string? Error)> RecordUploadAsync(
        Guid versionId, BtkiParseResult parse, string createdBy, CancellationToken ct = default)
    {
        var version = await db.NomenclatureVersions.FindAsync([versionId], ct);
        if (version is null) return (null, "Versi tidak ditemukan.");
        var path = version.State switch
        {
            ImportState.Uploaded     => new[] { ImportState.Parsing, ImportState.Mapped, ImportState.Validating },
            ImportState.DryRunFailed => new[] { ImportState.Validating },
            _ => null,
        };
        if (path is null)
            return (null, $"Upload hanya untuk versi berstatus Uploaded atau DryRunFailed (status sekarang: {version.State}). Buat versi baru untuk dataset lain.");

        var dry = BtkiStructureService.DryRun(parse.TariffNodes);
        var issues = parse.Issues.Concat(dry.Issues).ToList();
        var errorCount = issues.Count(i => i.Severity == "ERROR");

        var batch = new ImportBatch
        {
            VersionId = versionId, FileName = parse.SourceFileName, Checksum = parse.SourceChecksum,
            TotalNodes = parse.TariffNodes.Count, ErrorCount = errorCount,
            WarningCount = issues.Count(i => i.Severity == "WARNING"), CreatedBy = createdBy,
            // ponytail: report capped at 1000 issues to keep the row small; counts above stay exact.
            ReportJson = JsonSerializer.Serialize(new
            {
                totalRows = parse.TotalRows, skippedRows = parse.SkippedRows, parsedNodes = parse.TariffNodes.Count,
                ambiguousNodes = dry.AmbiguousNodes, issues = issues.Take(1000),
            }),
            Items = parse.TariffNodes.Select((n, i) => (n, i)).Where(x => x.n.IsAmbiguous)
                .Select(x => new AmbiguityItem
                {
                    Code = x.n.Code, RowNumber = x.i + 1, ProposedParent = x.n.ParentCode,
                    Reason = x.n.AmbiguityReason ?? "Parent hierarchy ambiguous",
                }).ToList(),
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);

        foreach (var s in path) await versionManager.TransitionStateAsync(versionId, s);
        await versionManager.TransitionStateAsync(versionId, errorCount == 0 ? ImportState.DryRunPassed : ImportState.DryRunFailed);
        await PromoteIfReadyAsync(versionId, ct);
        return (batch, null);
    }

    private static readonly Regex ParentFormat = new(@"^\d{4}(\.\d{1,2}){0,2}$");

    /// <summary>Decide one pending ambiguity item of the version's latest batch.</summary>
    public async Task<string?> DecideAsync(long itemId, AmbiguityDecision decision, string? correctedParent,
        string? note, string decidedBy, CancellationToken ct = default)
    {
        if (decision == AmbiguityDecision.Pending) return "Keputusan harus Approved atau Rejected.";
        if ((note?.Trim().Length ?? 0) < 5) return "Catatan keputusan wajib diisi (minimal 5 karakter).";
        correctedParent = string.IsNullOrWhiteSpace(correctedParent) ? null : correctedParent.Trim();
        if (correctedParent is not null && (decision != AmbiguityDecision.Approved || !ParentFormat.IsMatch(correctedParent)))
            return "Parent koreksi hanya untuk Approved dan harus berformat kode HS (mis. 8429 atau 8429.52).";

        var item = await db.AmbiguityItems.Include(i => i.Batch).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return "Item tidak ditemukan.";
        if (item.Decision != AmbiguityDecision.Pending) return "Item ini sudah diputuskan.";
        var latest = await LatestBatchAsync(item.Batch!.VersionId, ct);
        if (latest?.Id != item.BatchId) return "Item berasal dari upload lama; putuskan item pada upload terbaru.";

        item.Decision = decision;
        item.CorrectedParent = correctedParent;
        item.Note = note!.Trim();
        item.DecidedBy = decidedBy;
        item.DecidedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await PromoteIfReadyAsync(item.Batch.VersionId, ct);
        return null;
    }

    /// <summary>DryRunPassed → ReadyToCommit once the latest batch has no errors and nothing pending.</summary>
    private async Task PromoteIfReadyAsync(Guid versionId, CancellationToken ct)
    {
        var version = await db.NomenclatureVersions.FindAsync([versionId], ct);
        var batch = await LatestBatchAsync(versionId, ct);
        if (version?.State == ImportState.DryRunPassed && batch is { ErrorCount: 0 }
            && batch.Items.All(i => i.Decision != AmbiguityDecision.Pending))
            await versionManager.TransitionStateAsync(versionId, ImportState.ReadyToCommit);
    }

    public async Task<(bool Success, string Message, int Inserted)> CommitAsync(
        Guid versionId, BtkiParseResult parse, CancellationToken ct = default)
    {
        var version = await db.NomenclatureVersions.FindAsync([versionId], ct);
        if (version is null) return (false, "Version not found.", 0);
        if (version.State != ImportState.ReadyToCommit)
            return (false, $"Version state must be ReadyToCommit. Current: {version.State}", 0);
        // A commit that stopped half way leaves rows behind; inserting again would duplicate them
        if (await db.BtkiTariffLines.AnyAsync(t => t.VersionId == versionId, ct))
            return (false, "Versi ini sudah berisi baris tarif (commit sebelumnya tidak selesai). Buat versi baru.", 0);

        var batch = await LatestBatchAsync(versionId, ct);
        if (batch is null) return (false, "Belum ada upload untuk versi ini.", 0);
        // The decisions were made on a specific file; committing a different file would bypass them
        if (!string.Equals(batch.Checksum, parse.SourceChecksum, StringComparison.OrdinalIgnoreCase))
            return (false, "File berbeda dari yang divalidasi (checksum tidak sama). Upload ulang dan validasi ulang.", 0);

        var (nodes, blocker) = AmbiguityGate.ResolveForCommit(parse.TariffNodes,
            batch.Items.Select(i => new AmbiguityResolution(i.Code, i.Decision, i.CorrectedParent)));
        if (blocker is not null) return (false, blocker, 0);

        var inserted = 0;
        foreach (var chunk in nodes.Chunk(500))
        {
            await db.BtkiTariffLines.AddRangeAsync(chunk.Select(node => new BtkiTariffLine
            {
                VersionId = versionId, Code = node.Code, ParentCode = node.ParentCode,
                ChapterCode = node.ChapterCode, HeadingCode = node.HeadingCode, SubheadingCode = node.SubheadingCode,
                IndentLevel = node.IndentLevel, DescriptionId = node.DescriptionId, DescriptionEn = node.DescriptionEn,
                SectionCode = node.SectionCode,
                IngestionMethod = parse.IngestionMethod, SourceId = node.SourceId, IsActive = node.Status == "A",
            }), ct);
            await db.SaveChangesAsync(ct);
            inserted += chunk.Length;
        }

        // A PDF import covers only the chapters that have a text layer and carries no tariff rates:
        // complete the version from the active one before it can be activated
        var carried = parse.IngestionMethod == IngestionMethod.PdfParser
            ? await CarryForwardAsync(versionId, nodes, parse, ct) : null;

        await versionManager.TransitionStateAsync(versionId, ImportState.Committed);
        await versionManager.TransitionStateAsync(versionId, ImportState.Indexed);
        var rejected = parse.TariffNodes.Count - nodes.Count;
        return (true, $"Commit berhasil{(rejected > 0 ? $" ({rejected} node ditolak tidak ikut)" : "")}." +
                      (carried is null ? "" : " " + carried.Summary), inserted);
    }

    // ─── Complete a PDF version from the active version ──────────────────────

    public record CarryReport(int ChapterRows, int ChaptersCarried, int GroupRows, int MfnRates, int LinesWithoutMfn, string Summary);

    /// <summary>
    /// Copies from the active version what the PDF does not contain, marking every copied row with a
    /// "carry:" SourceId: (1) every chapter the PDF has no text for (scanned chapters 1–15), (2) the text of
    /// one-dash groups the PDF omits for chapters it does cover, (3) MFN rates by tariff-line code.
    /// A tariff line that is new in the PDF gets no rate, and the summary says how many. Nothing is invented.
    /// ponytail: FTA / PPN / PPh 22 rates are not copied because those tables are empty today; add them here once seeded.
    /// </summary>
    private async Task<CarryReport> CarryForwardAsync(Guid versionId, List<ParsedTariffNode> committed, BtkiParseResult parse, CancellationToken ct)
    {
        var active = await db.NomenclatureVersions.AsNoTracking().FirstOrDefaultAsync(v => v.IsActive && v.Id != versionId, ct);
        if (active is null) return new(0, 0, 0, 0, 0, "Tidak ada versi aktif, jadi Bab tanpa teks dan tarif MFN tidak disalin.");

        var haveChapters = committed.Select(n => n.Code[..2]).ToHashSet();
        var haveCodes = committed.Select(n => n.Code).ToHashSet();
        var wantedGroups = parse.MissingGroupCodes.ToHashSet();

        var oldRows = (await db.BtkiTariffLines.AsNoTracking().Where(t => t.VersionId == active.Id && t.IsActive).ToListAsync(ct))
            .DistinctBy(t => t.Code).ToList();

        string Carry(string? source)
        {
            var s = $"carry:{active.Name}:{source}";
            return s.Length > 500 ? s[..500] : s;
        }
        BtkiTariffLine Copy(BtkiTariffLine o) => new()
        {
            VersionId = versionId, Code = o.Code, ParentCode = o.ParentCode, SectionCode = o.SectionCode, ChapterCode = o.ChapterCode,
            HeadingCode = o.HeadingCode, SubheadingCode = o.SubheadingCode, IndentLevel = o.IndentLevel,
            DescriptionId = o.DescriptionId, DescriptionEn = o.DescriptionEn, Unit = o.Unit, IngestionMethod = o.IngestionMethod,
            SourceId = Carry(o.SourceId), IsActive = true, EffectiveFrom = o.EffectiveFrom, EffectiveTo = o.EffectiveTo,
        };

        var chapterRows = oldRows.Where(t => !haveChapters.Contains(t.Code[..2])).ToList();

        // Same repair the PDF parser makes: a heading printed only as one tariff line ("0409.00.00 Madu alam.", no dash)
        // has no 4-digit row, so it could never be found as a heading.
        var headingSeen = chapterRows.Where(t => t.Code.Length == 4).Select(t => t.Code).ToHashSet();
        var headingRows = new List<BtkiTariffLine>();
        foreach (var line in chapterRows.Where(t => t.Code.Length == 10 && BtkiTableAssembler.Dashes(t.DescriptionId) == 0))
            if (headingSeen.Add(line.Code[..4]))
                headingRows.Add(new BtkiTariffLine
                {
                    Code = line.Code[..4], ParentCode = null, SectionCode = line.SectionCode, ChapterCode = line.Code[..2],
                    HeadingCode = line.Code[..4], IndentLevel = 0, DescriptionId = line.DescriptionId, DescriptionEn = line.DescriptionEn,
                    IngestionMethod = line.IngestionMethod, SourceId = line.SourceId + ":heading", IsActive = true,
                    EffectiveFrom = line.EffectiveFrom, EffectiveTo = line.EffectiveTo,
                });
        // A group is a one-dash text ("- Dari babi :"). The old data also holds deeper texts ("- - - Luncheon meat :")
        // filed under a group code by mistake; those are not copied.
        var groupRows = oldRows.Where(t => haveChapters.Contains(t.Code[..2]) && t.Code.Length == 6
            && wantedGroups.Contains(t.Code) && !haveCodes.Contains(t.Code)
            && BtkiTableAssembler.Dashes(t.DescriptionId) == 1).ToList();
        foreach (var chunk in chapterRows.Concat(groupRows).Select(Copy)
                     .Concat(headingRows.Select(h => { h.VersionId = versionId; h.SourceId = Carry(h.SourceId); return h; })).Chunk(500))
        {
            await db.BtkiTariffLines.AddRangeAsync(chunk, ct);
            await db.SaveChangesAsync(ct);
        }

        // MFN by code: old line → rate, new line by the same code
        var oldRates = (await (from r in db.MfnRates.AsNoTracking()
                               join l in db.BtkiTariffLines.AsNoTracking() on r.TariffLineId equals l.Id
                               where r.VersionId == active.Id && r.IsActive
                               select new { l.Code, r.RateValue, r.RateUnit, r.Currency, r.QuantityUnit, r.Formula, r.EffectiveFrom, r.EffectiveTo, r.SourceId })
            .ToListAsync(ct)).DistinctBy(r => r.Code).ToDictionary(r => r.Code);
        var newLines = (await db.BtkiTariffLines.AsNoTracking().Where(t => t.VersionId == versionId && t.Code.Length == 10)
            .Select(t => new { t.Code, t.Id }).ToListAsync(ct)).DistinctBy(t => t.Code).ToList();

        var rates = new List<MfnRate>();
        foreach (var l in newLines)
            if (oldRates.TryGetValue(l.Code, out var r))
                rates.Add(new MfnRate
                {
                    TariffLineId = l.Id, VersionId = versionId, RateValue = r.RateValue, RateUnit = r.RateUnit, Currency = r.Currency,
                    QuantityUnit = r.QuantityUnit, Formula = r.Formula, EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo,
                    SourceId = Carry(r.SourceId), IsActive = true,
                });
        foreach (var chunk in rates.Chunk(1000))
        {
            await db.MfnRates.AddRangeAsync(chunk, ct);
            await db.SaveChangesAsync(ct);
        }

        var withoutMfn = newLines.Count - rates.Count;
        var chapters = chapterRows.Select(t => t.Code[..2]).Distinct().Count();
        return new(chapterRows.Count, chapters, groupRows.Count, rates.Count, withoutMfn,
            $"Disalin dari versi aktif \"{active.Name}\": {chapterRows.Count} baris dari {chapters} Bab yang tidak punya teks di PDF, " +
            $"{groupRows.Count} teks grup, {headingRows.Count} pos dibuat dari baris tarif tunggal, {rates.Count} tarif MFN. " +
            $"{withoutMfn} pos tarif belum punya tarif MFN.");
    }
}
