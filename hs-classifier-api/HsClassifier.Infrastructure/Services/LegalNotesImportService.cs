using System.Text.Json;
using HsClassifier.Application.Interfaces;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// Loads Catatan Bagian, Bab and Subpos into a version that is not active. Same two steps as EN: a dry run
/// stored as an ImportBatch of Kind Notes, then a commit of the same file (same checksum).
/// </summary>
public class LegalNotesImportService(HsClassifierDbContext db)
{
    private async Task<string?> CheckVersionAsync(Guid versionId, CancellationToken ct)
    {
        var version = await db.NomenclatureVersions.FindAsync([versionId], ct);
        if (version is null) return "Versi tidak ditemukan.";
        if (version.IsActive || version.State == ImportState.Active)
            return "Versi yang sedang aktif tidak boleh diubah. Klon versi aktif menjadi draft, muat catatan ke draft itu, lalu aktifkan setelah dibandingkan.";
        if (!await db.BtkiTariffLines.AnyAsync(t => t.VersionId == versionId, ct))
            return "Versi ini belum berisi baris tarif BTKI, jadi pos untuk catatan subpos tidak bisa dicocokkan.";
        return null;
    }

    private static int Total(LegalNotesParseResult p) => p.SectionNotes.Count + p.ChapterNotes.Count + p.SubheadingNotes.Count;

    private async Task<HashSet<string>> VersionHeadingsAsync(Guid versionId, CancellationToken ct) =>
        (await db.BtkiTariffLines.Where(t => t.VersionId == versionId && t.HeadingCode != null)
            .Select(t => t.HeadingCode!).Distinct().ToListAsync(ct)).ToHashSet();

    /// <summary>A subheading note is only stored when its scope is a 4-digit heading that exists in the version.</summary>
    private static bool IsKnownHeading(string scope, HashSet<string> headings) => scope.Length == 4 && headings.Contains(scope);

    public async Task<(ImportBatch? Batch, string? Error)> RecordUploadAsync(
        Guid versionId, LegalNotesParseResult parse, string createdBy, CancellationToken ct = default)
    {
        var error = await CheckVersionAsync(versionId, ct);
        if (error is not null) return (null, error);

        var headings = await VersionHeadingsAsync(versionId, ct);
        var unknownScopes = parse.SubheadingNotes.Select(n => n.ScopeCode).Where(s => !IsKnownHeading(s, headings)).Distinct().Order().ToList();

        var issues = parse.Issues.ToList();
        if (Total(parse) == 0)
            issues.Add(new ParseIssue { Severity = "ERROR", Type = "NO_NOTES", Message = "Tidak ada catatan yang dikenali." });
        if (unknownScopes.Count > 0)
            issues.Add(new ParseIssue { Severity = "WARNING", Type = "UNKNOWN_SUBHEADING_SCOPE",
                Message = $"Catatan subpos untuk {unknownScopes.Count} cakupan tidak punya pos 4 digit di BTKI versi ini; tidak akan disimpan: {string.Join(", ", unknownScopes.Take(30))}." });

        var batch = new ImportBatch
        {
            VersionId = versionId, Kind = ImportKind.Notes, FileName = parse.SourceFileName, Checksum = parse.SourceChecksum,
            TotalNodes = Total(parse), CreatedBy = createdBy,
            ErrorCount = issues.Count(i => i.Severity == "ERROR"), WarningCount = issues.Count(i => i.Severity == "WARNING"),
            ReportJson = JsonSerializer.Serialize(new
            {
                sectionNotes = parse.SectionNotes.Count, chapterNotes = parse.ChapterNotes.Count, subheadingNotes = parse.SubheadingNotes.Count,
                sections = parse.SectionNotes.Select(n => n.ScopeCode).Distinct().Order().ToList(),
                chapters = parse.ChapterNotes.Select(n => n.ScopeCode).Distinct().Order().ToList(),
                unknownSubheadingScopes = unknownScopes,
                issues = issues.Take(1000),
            }),
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        return (batch, null);
    }

    /// <param name="replace">Delete the notes already stored for this version first.</param>
    public async Task<(bool Success, string Message, int Inserted)> CommitAsync(
        Guid versionId, LegalNotesParseResult parse, bool replace = false, CancellationToken ct = default)
    {
        var error = await CheckVersionAsync(versionId, ct);
        if (error is not null) return (false, error, 0);

        var batch = await db.ImportBatches.Where(b => b.VersionId == versionId && b.Kind == ImportKind.Notes)
            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id).FirstOrDefaultAsync(ct);
        if (batch is null) return (false, "Belum ada dry run catatan untuk versi ini. Upload file dulu dan periksa laporannya.", 0);
        if (!string.Equals(batch.Checksum, parse.SourceChecksum, StringComparison.OrdinalIgnoreCase))
            return (false, "File berbeda dari yang divalidasi (checksum tidak sama). Upload ulang dan periksa laporannya.", 0);
        if (batch.ErrorCount > 0) return (false, "Dry run catatan masih berisi ERROR; perbaiki dulu.", 0);

        var section = await db.SectionNotes.Where(n => n.VersionId == versionId).ToListAsync(ct);
        var chapter = await db.ChapterNotes.Where(n => n.VersionId == versionId).ToListAsync(ct);
        var sub = await db.SubheadingNotes.Where(n => n.VersionId == versionId).ToListAsync(ct);
        var existing = section.Count + chapter.Count + sub.Count;
        if (existing > 0 && !replace)
            return (false, $"Versi ini sudah berisi {existing} catatan. Pakai replace untuk menggantinya (hanya di versi non-aktif).", 0);
        db.SectionNotes.RemoveRange(section);
        db.ChapterNotes.RemoveRange(chapter);
        db.SubheadingNotes.RemoveRange(sub);

        var headings = await VersionHeadingsAsync(versionId, ct);
        var keep = parse.SubheadingNotes.Where(n => IsKnownHeading(n.ScopeCode, headings)).ToList();
        var skipped = parse.SubheadingNotes.Count - keep.Count;

        db.SectionNotes.AddRange(parse.SectionNotes.Select(n => new SectionNote
        {
            VersionId = versionId, SectionNumber = n.ScopeCode, NoteNumber = n.NoteNumber, ParagraphNumber = n.ParagraphNumber,
            TextId = n.TextId, TextEn = n.TextEn, EffectType = ToEffect(n.EffectType),
            TargetCodesJson = n.TargetCodesJson, ReferredCodesJson = n.ReferredCodesJson, SourceId = n.SourceId,
        }));
        db.ChapterNotes.AddRange(parse.ChapterNotes.Select(n => new ChapterNote
        {
            VersionId = versionId, ChapterNumber = n.ScopeCode, TargetHeading = n.TargetHeading, NoteNumber = n.NoteNumber,
            ParagraphNumber = n.ParagraphNumber, TextId = n.TextId, TextEn = n.TextEn, EffectType = ToEffect(n.EffectType),
            TargetCodesJson = n.TargetCodesJson, ReferredCodesJson = n.ReferredCodesJson, SourceId = n.SourceId,
        }));
        db.SubheadingNotes.AddRange(keep.Select(n => new SubheadingNote
        {
            VersionId = versionId, HeadingCode = n.ScopeCode, NoteNumber = n.NoteNumber, TextId = n.TextId, TextEn = n.TextEn,
            EffectType = ToEffect(n.EffectType), TargetCodesJson = n.TargetCodesJson, ReferredCodesJson = n.ReferredCodesJson, SourceId = n.SourceId,
        }));
        await db.SaveChangesAsync(ct);

        var inserted = parse.SectionNotes.Count + parse.ChapterNotes.Count + keep.Count;
        return (true, $"Catatan tersimpan: {parse.SectionNotes.Count} bagian, {parse.ChapterNotes.Count} bab, {keep.Count} subpos." +
                      (skipped > 0 ? $" {skipped} catatan subpos dilewati karena posnya tidak ada di versi ini." : ""), inserted);
    }

    /// <summary>"EXCLUSION" and "exclusion" give EffectType.Exclusion; anything unknown is Definition.</summary>
    public static EffectType ToEffect(string s) =>
        Enum.TryParse<EffectType>(s, true, out var t) ? t : EffectType.Definition;
}
