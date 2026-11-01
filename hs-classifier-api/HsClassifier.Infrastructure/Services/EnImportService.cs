using System.Text.Json;
using HsClassifier.Application.Interfaces;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// Loads parsed Explanatory Notes into a version that is not active. Two steps, like the BTKI import: a dry run
/// of the file (stored as an ImportBatch of Kind En with a report), then a commit of the same file (same checksum).
/// A version that is already active is never changed: EN is loaded into a draft, compared, and only then activated.
/// </summary>
public class EnImportService(HsClassifierDbContext db)
{
    private async Task<(NomenclatureVersion? Version, string? Error)> CheckVersionAsync(Guid versionId, CancellationToken ct)
    {
        var version = await db.NomenclatureVersions.FindAsync([versionId], ct);
        if (version is null) return (null, "Versi tidak ditemukan.");
        if (version.IsActive || version.State == ImportState.Active)
            return (null, "Versi yang sedang aktif tidak boleh diubah. Klon versi aktif menjadi draft, muat EN ke draft itu, lalu aktifkan setelah dibandingkan.");
        if (!await db.BtkiTariffLines.AnyAsync(t => t.VersionId == versionId, ct))
            return (null, "Versi ini belum berisi baris tarif BTKI, jadi pos untuk EN tidak bisa dicocokkan.");
        return (version, null);
    }

    private Task<ImportBatch?> LatestEnBatchAsync(Guid versionId, CancellationToken ct) =>
        db.ImportBatches.Where(b => b.VersionId == versionId && b.Kind == ImportKind.En)
            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id).FirstOrDefaultAsync(ct);

    /// <summary>Dry run: compare the parsed headings with the version's BTKI headings and keep the report.</summary>
    public async Task<(ImportBatch? Batch, string? Error)> RecordUploadAsync(
        Guid versionId, EnParseResult parse, string createdBy, CancellationToken ct = default)
    {
        var (_, error) = await CheckVersionAsync(versionId, ct);
        if (error is not null) return (null, error);

        var versionHeadings = (await db.BtkiTariffLines
            .Where(t => t.VersionId == versionId && t.HeadingCode != null).Select(t => t.HeadingCode!).Distinct().ToListAsync(ct))
            .ToHashSet();
        var parsedHeadings = parse.Paragraphs.Select(p => p.HeadingCode).Distinct().ToList();
        var unknown = parsedHeadings.Where(h => !versionHeadings.Contains(h)).Order().ToList();
        var without = versionHeadings.Where(h => !parsedHeadings.Contains(h)).Order().ToList();

        var issues = parse.Issues.ToList();
        if (parse.Paragraphs.Count == 0 && !issues.Any(i => i.Type == "NO_PARAGRAPHS"))
            issues.Add(new ParseIssue { Severity = "ERROR", Type = "NO_PARAGRAPHS", Message = "Tidak ada paragraf EN yang dikenali." });
        if (unknown.Count > 0)
            issues.Add(new ParseIssue { Severity = "WARNING", Type = "UNKNOWN_HEADING",
                Message = $"{unknown.Count} pos di EN tidak ada di BTKI versi ini; paragrafnya tidak akan disimpan: {string.Join(", ", unknown.Take(30))}{(unknown.Count > 30 ? ", …" : "")}." });
        if (without.Count > 0)
            issues.Add(new ParseIssue { Severity = "INFO", Type = "HEADING_WITHOUT_EN",
                Message = $"{without.Count} dari {versionHeadings.Count} pos BTKI tidak punya paragraf EN dari file ini (EN tidak ada, atau penanda pos rusak karena OCR)." });

        var batch = new ImportBatch
        {
            VersionId = versionId, Kind = ImportKind.En, FileName = parse.SourceFileName, Checksum = parse.SourceChecksum,
            TotalNodes = parse.Paragraphs.Count, CreatedBy = createdBy,
            ErrorCount = issues.Count(i => i.Severity == "ERROR"), WarningCount = issues.Count(i => i.Severity == "WARNING"),
            // ponytail: lists capped so the row stays small; counts stay exact.
            ReportJson = JsonSerializer.Serialize(new
            {
                paragraphs = parse.Paragraphs.Count, headings = parsedHeadings.Count, skippedLines = parse.SkippedLines,
                versionHeadings = versionHeadings.Count,
                byType = parse.Paragraphs.GroupBy(p => p.ParagraphType).ToDictionary(g => g.Key, g => g.Count()),
                unknownHeadings = unknown.Take(100), headingsWithoutEn = without.Take(400),
                issues = issues.Take(1000),
            }),
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        return (batch, null);
    }

    /// <param name="replace">Delete the EN already stored for this version first (to load a corrected file into a draft).</param>
    public async Task<(bool Success, string Message, int Inserted)> CommitAsync(
        Guid versionId, EnParseResult parse, bool replace = false, CancellationToken ct = default)
    {
        var (_, error) = await CheckVersionAsync(versionId, ct);
        if (error is not null) return (false, error, 0);

        var batch = await LatestEnBatchAsync(versionId, ct);
        if (batch is null) return (false, "Belum ada dry run EN untuk versi ini. Upload file EN dulu dan periksa laporannya.", 0);
        if (!string.Equals(batch.Checksum, parse.SourceChecksum, StringComparison.OrdinalIgnoreCase))
            return (false, "File berbeda dari yang divalidasi (checksum tidak sama). Upload ulang dan periksa laporannya.", 0);
        if (batch.ErrorCount > 0) return (false, "Dry run EN masih berisi ERROR; perbaiki dulu.", 0);

        var existing = await db.ExplanatoryNotes.Where(n => n.VersionId == versionId).ToListAsync(ct);
        if (existing.Count > 0 && !replace)
            return (false, $"Versi ini sudah berisi {existing.Count} paragraf EN. Pakai replace untuk menggantinya (hanya di versi non-aktif).", 0);
        if (existing.Count > 0) db.ExplanatoryNotes.RemoveRange(existing);

        var known = (await db.BtkiTariffLines.Where(t => t.VersionId == versionId && t.HeadingCode != null)
            .Select(t => t.HeadingCode!).Distinct().ToListAsync(ct)).ToHashSet();
        var toStore = parse.Paragraphs.Where(p => known.Contains(p.HeadingCode)).ToList();
        var skipped = parse.Paragraphs.Count - toStore.Count;

        // ponytail: no surrounding transaction (EF InMemory has none); a failure half way leaves rows behind,
        // and the retry uses replace=true. Upgrade: wrap in a transaction when only SQL Server is targeted.
        var rows = new List<ExplanatoryNote>(toStore.Count);
        foreach (var chunk in toStore.Chunk(1000))
        {
            var entities = chunk.Select(p => new ExplanatoryNote
            {
                VersionId = versionId, HeadingCode = p.HeadingCode, SubheadingCode = p.SubheadingCode,
                ParagraphNumber = p.ParagraphNumber, ParagraphType = ToType(p.ParagraphType),
                TextContent = p.TextContent, ReferredCodesJson = p.ReferredCodesJson, SourceId = p.SourceId,
            }).ToList();
            db.ExplanatoryNotes.AddRange(entities);
            await db.SaveChangesAsync(ct);
            rows.AddRange(entities);
        }

        // Neighbours within one heading, in document order (ids exist now)
        foreach (var group in rows.GroupBy(r => r.HeadingCode))
        {
            ExplanatoryNote? prev = null;
            foreach (var row in group)
            {
                if (prev is not null) { row.PreviousParagraphId = prev.Id; prev.NextParagraphId = row.Id; }
                prev = row;
            }
        }
        await db.SaveChangesAsync(ct);

        var headings = rows.Select(r => r.HeadingCode).Distinct().Count();
        return (true, $"EN tersimpan: {rows.Count} paragraf untuk {headings} pos." +
                      (skipped > 0 ? $" {skipped} paragraf dilewati karena posnya tidak ada di versi ini." : ""), rows.Count);
    }

    /// <summary>"CROSS_REFERENCE" and "CrossReference" both give ParagraphType.CrossReference; anything unknown is Scope.</summary>
    public static ParagraphType ToType(string s) =>
        Enum.TryParse<ParagraphType>(s.Replace("_", ""), true, out var t) ? t : ParagraphType.Scope;
}
