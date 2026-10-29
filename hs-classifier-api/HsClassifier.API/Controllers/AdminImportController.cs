using System.Security.Claims;
using System.Text.Json;
using HsClassifier.Application.Interfaces;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using HsClassifier.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HsClassifier.API.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin")]
public class AdminImportController(IDocxParser parser, IBtkiPdfParser pdfParser, BtkiImportService importService,
    IVersionManager versionManager, HsClassifierDbContext db) : ControllerBase
{
    // ─── BTKI ─────────────────────────────────────────────────────────────────

    private string Actor => User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "admin";

    /// <summary>
    /// One .docx, or one or more .pdf (official chapter PDFs or the whole book). The same reader is used
    /// for upload and commit, so the checksum of what was validated is what gets committed.
    /// </summary>
    private async Task<(BtkiParseResult? Result, string? Error)> ParseBtkiAsync(List<IFormFile>? files)
    {
        files = files?.Where(f => f.Length > 0).ToList();
        if (files is null || files.Count == 0) return (null, "No file uploaded.");
        var extensions = files.Select(f => Path.GetExtension(f.FileName).ToLowerInvariant()).Distinct().ToList();
        if (extensions.Count != 1 || extensions[0] is not (".docx" or ".pdf"))
            return (null, "Upload file .docx saja, atau satu/banyak file .pdf (jangan dicampur).");

        if (extensions[0] == ".docx")
        {
            if (files.Count != 1) return (null, "Upload .docx hanya satu file.");
            await using var s = files[0].OpenReadStream();
            return (await parser.ParseBTKIAsync(s, files[0].FileName), null);
        }

        var streams = new List<(Stream, string)>();
        try
        {
            foreach (var f in files) streams.Add((f.OpenReadStream(), f.FileName));
            return (await pdfParser.ParseManyAsync(streams), null);
        }
        finally { foreach (var (s, _) in streams) await s.DisposeAsync(); }
    }

    /// <summary>
    /// Upload BTKI (DOCX, atau PDF resmi per Bab / gabungan) ke sebuah versi: parse, dry run,
    /// simpan laporan validasi (import batch), dan masukkan node STRUCTURE_AMBIGUITY ke antrian review.
    /// </summary>
    [HttpPost("btki/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> UploadBtki([FromQuery] Guid versionId, List<IFormFile> files)
    {
        if (versionId == Guid.Empty) return BadRequest(new { message = "versionId wajib. Buat versi baru dulu." });
        var (result, parseError) = await ParseBtkiAsync(files);
        if (result is null) return BadRequest(new { message = parseError });
        var (batch, error) = await importService.RecordUploadAsync(versionId, result, Actor);
        if (batch is null) return BadRequest(new { message = error });

        return Ok(new
        {
            batchId = batch.Id,
            fileName = result.SourceFileName,
            checksum = result.SourceChecksum,
            ingestion = result.IngestionMethod.ToString(),
            totalRows = result.TotalRows,
            skippedRows = result.SkippedRows,
            parsedNodes = result.TariffNodes.Count,
            nodesByLevel = result.TariffNodes.GroupBy(n => n.Code.Length switch { 4 => "pos", 6 => "grup", 7 => "subpos", _ => "pos tarif" })
                .ToDictionary(g => g.Key, g => g.Count()),
            chapters = result.TariffNodes.Select(n => n.ChapterCode).Distinct().Count(),
            dryRun = new
            {
                errorCount = batch.ErrorCount,
                warningCount = batch.WarningCount,
                ambiguousNodes = batch.Items.Count,
            },
            versionState = (await versionManager.GetVersionAsync(versionId))?.State.ToString(),
            sample = result.TariffNodes.Take(10),
        });
    }

    /// <summary>Commit file yang sama dengan upload terakhir. Diblokir bila ada error atau ambiguitas pending.</summary>
    [HttpPost("btki/commit/{versionId:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> CommitBtki(Guid versionId, List<IFormFile> files)
    {
        var (parseResult, parseError) = await ParseBtkiAsync(files);
        if (parseResult is null) return BadRequest(new { message = parseError });
        var (success, message, inserted) = await importService.CommitAsync(versionId, parseResult);

        return success ? Ok(new { message, inserted })
                       : BadRequest(new { message });
    }

    // ─── Laporan Validasi & Antrian Ambiguitas ────────────────────────────────

    /// <summary>Daftar laporan validasi (import batch), terbaru dulu. Filter per versi opsional.</summary>
    [HttpGet("import-batches")]
    public async Task<IActionResult> ListBatches([FromQuery] Guid? versionId)
    {
        var q = db.ImportBatches.AsQueryable();
        if (versionId is not null) q = q.Where(b => b.VersionId == versionId);
        var rows = await q.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id).Take(100)
            .Select(b => new
            {
                b.Id, b.VersionId, b.FileName, b.Checksum, b.TotalNodes, b.ErrorCount, b.WarningCount, b.CreatedBy, b.CreatedAt,
                Ambiguous = b.Items.Count(),
                Pending = b.Items.Count(i => i.Decision == AmbiguityDecision.Pending),
            }).ToListAsync();
        return Ok(rows);
    }

    /// <summary>Detail laporan validasi beserta antrian ambiguitasnya.</summary>
    [HttpGet("import-batches/{id:long}")]
    public async Task<IActionResult> GetBatch(long id)
    {
        var b = await db.ImportBatches.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound(new { message = "Batch tidak ditemukan." });
        var latest = await importService.LatestBatchAsync(b.VersionId);
        return Ok(new
        {
            b.Id, b.VersionId, b.FileName, b.Checksum, b.TotalNodes, b.ErrorCount, b.WarningCount, b.CreatedBy, b.CreatedAt,
            IsLatest = latest?.Id == b.Id,
            Report = JsonDocument.Parse(b.ReportJson).RootElement,
            Items = b.Items.OrderBy(i => i.RowNumber).Select(i => new
            {
                i.Id, i.Code, i.RowNumber, i.ProposedParent, i.Reason, Decision = i.Decision.ToString(),
                i.CorrectedParent, i.Note, i.DecidedBy, i.DecidedAt,
            }),
        });
    }

    /// <summary>Putuskan satu item ambiguitas: Approved (boleh koreksi parent) atau Rejected (node tidak di-commit).</summary>
    [HttpPost("ambiguity/{itemId:long}/decide")]
    public async Task<IActionResult> Decide(long itemId, [FromBody] AmbiguityDecisionRequest req)
    {
        if (!Enum.TryParse<AmbiguityDecision>(req.Decision, true, out var decision))
            return BadRequest(new { message = "Decision harus Approved atau Rejected." });
        var error = await importService.DecideAsync(itemId, decision, req.CorrectedParent, req.Note, Actor);
        return error is null ? Ok(new { message = "Keputusan disimpan." }) : BadRequest(new { message = error });
    }

    // ─── Explanatory Notes ────────────────────────────────────────────────────

    [HttpPost("en/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadEN(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "No file uploaded." });
        if (!file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .docx files are accepted." });

        await using var stream = file.OpenReadStream();
        var result = await parser.ParseENAsync(stream, file.FileName);

        return Ok(new
        {
            fileName = result.SourceFileName,
            checksum = result.SourceChecksum,
            totalParagraphs = result.TotalParagraphs,
            byType = result.Paragraphs
                .GroupBy(p => p.ParagraphType)
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count),
            errorCount = result.Issues.Count(i => i.Severity == "ERROR"),
            issues = result.Issues.Take(20),
            sample = result.Paragraphs.Take(10),
        });
    }

    // ─── Legal Notes ──────────────────────────────────────────────────────────

    [HttpPost("notes/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadNotes(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "No file uploaded." });
        if (!file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .docx files are accepted." });

        await using var stream = file.OpenReadStream();
        var result = await parser.ParseLegalNotesAsync(stream, file.FileName);

        return Ok(new
        {
            fileName = result.SourceFileName,
            checksum = result.SourceChecksum,
            sectionNotes = result.SectionNotes.Count,
            chapterNotes = result.ChapterNotes.Count,
            subheadingNotes = result.SubheadingNotes.Count,
            errorCount = result.Issues.Count(i => i.Severity == "ERROR"),
            issues = result.Issues.Take(20),
            sampleSectionNotes = result.SectionNotes.Take(5),
            sampleChapterNotes = result.ChapterNotes.Take(5),
        });
    }
}

public record AmbiguityDecisionRequest(string Decision, string? CorrectedParent, string? Note);
