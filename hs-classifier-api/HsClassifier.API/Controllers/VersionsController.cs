using System.Security.Claims;
using HsClassifier.Application.Interfaces;
using HsClassifier.Application.Services;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.API.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/versions")]
public class VersionsController(IVersionManager versionManager, HsClassifierDbContext db) : ControllerBase
{
    /// <summary>
    /// What activating this version would change, compared with the active version (or with <c>against</c>):
    /// codes added, removed, reworded, and texts that differ only by OCR-prone characters.
    /// </summary>
    /// <param name="id">Version to look at (the candidate).</param>
    /// <param name="against">"active" (default) or the id of another version.</param>
    /// <param name="chapter">Only list entries of this 2-digit chapter; the counts stay for the whole version.</param>
    /// <param name="take">Entries listed per category (1-1000).</param>
    [HttpGet("{id:guid}/diff")]
    public async Task<IActionResult> Diff(Guid id, [FromQuery] string? against, [FromQuery] string? chapter, [FromQuery] int take = 200)
    {
        var candidate = await db.NomenclatureVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
        if (candidate is null) return NotFound(new { message = "Versi tidak ditemukan." });

        NomenclatureVersion? baseline;
        if (string.IsNullOrWhiteSpace(against) || against.Equals("active", StringComparison.OrdinalIgnoreCase))
            baseline = await db.NomenclatureVersions.AsNoTracking().FirstOrDefaultAsync(v => v.IsActive && v.Id != id);
        else if (Guid.TryParse(against, out var otherId))
            baseline = await db.NomenclatureVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == otherId);
        else return BadRequest(new { message = "against harus 'active' atau id versi." });
        if (baseline is null) return BadRequest(new { message = "Versi pembanding tidak ditemukan (belum ada versi aktif lain)." });

        async Task<List<LineText>> Lines(Guid versionId) =>
            await db.BtkiTariffLines.AsNoTracking().Where(t => t.VersionId == versionId && t.IsActive)
                .Select(t => new LineText(t.Code, t.DescriptionId, t.DescriptionEn)).ToListAsync();

        var d = VersionDiff.Compare(await Lines(baseline.Id), await Lines(id));
        take = Math.Clamp(take, 1, 1000);
        object Page(List<DiffEntry> all)
        {
            var rows = string.IsNullOrWhiteSpace(chapter) ? all : all.Where(e => e.Chapter == chapter.Trim()).ToList();
            return new { total = rows.Count, truncated = rows.Count > take, items = rows.Take(take) };
        }

        var chapters = d.Added.Concat(d.Removed).Concat(d.Changed).Concat(d.OcrOnly).Select(e => e.Chapter).Distinct().Order().ToList();
        return Ok(new
        {
            baseline = new { baseline.Id, baseline.Name },
            candidate = new { candidate.Id, candidate.Name },
            counts = new
            {
                baseline = d.BaselineCount, candidate = d.CandidateCount, unchanged = d.Unchanged,
                added = d.Added.Count, removed = d.Removed.Count, changed = d.Changed.Count, ocrOnly = d.OcrOnly.Count,
            },
            perChapter = chapters.Select(c => new
            {
                chapter = c,
                added = d.Added.Count(e => e.Chapter == c), removed = d.Removed.Count(e => e.Chapter == c),
                changed = d.Changed.Count(e => e.Chapter == c), ocrOnly = d.OcrOnly.Count(e => e.Chapter == c),
            }),
            added = Page(d.Added), removed = Page(d.Removed), changed = Page(d.Changed), ocrOnly = Page(d.OcrOnly),
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var versions = await versionManager.GetAllVersionsAsync();
        return Ok(versions.Select(v => new
        {
            v.Id, v.Name, v.BtkiEdition, v.RegulationNumber,
            v.EffectiveFrom, v.EffectiveTo,
            State = v.State.ToString(),
            v.IsActive, v.CreatedAt, v.CreatedBy
        }));
    }

    /// <summary>Tambah versi nomenklatur baru (status Uploaded). Upload DOCX lalu dilakukan ke versi ini.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVersionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Nama versi wajib diisi." });
        if (req.EffectiveTo is not null && req.EffectiveTo < req.EffectiveFrom)
            return BadRequest(new { message = "Tanggal akhir tidak boleh sebelum tanggal mulai." });
        var by = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var id = await versionManager.CreateVersionAsync(req.Name.Trim(), req.BtkiEdition?.Trim() ?? "",
            req.RegulationNumber?.Trim() ?? "", req.EffectiveFrom, req.EffectiveTo, by);
        return Ok(new { id, message = "Versi dibuat." });
    }

    /// <summary>
    /// Salin versi ini (baris tarif, tarif, catatan, EN) menjadi draft baru berstatus Indexed dan tidak aktif.
    /// EN dan Catatan dimuat ke draft itu, dibandingkan dengan versi aktif, lalu diaktifkan oleh Admin.
    /// </summary>
    [HttpPost("{id:guid}/clone")]
    public async Task<IActionResult> Clone(Guid id, [FromBody] CloneVersionRequest? req, [FromServices] VersionCloneService cloner)
    {
        var by = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var (clone, error) = await cloner.CloneAsync(id, req?.Name, by);
        return clone is null ? BadRequest(new { message = error })
            : Ok(new { id = clone.Id, name = clone.Name, message = "Draft dibuat. Muat EN dan Catatan ke draft ini." });
    }

    /// <summary>
    /// Classify the same cases on the active version and on this draft and list where the answers differ. Saves nothing,
    /// uses no quota and does not call the AI. Without <c>cases</c> the built-in list (the regression cases) is used.
    /// </summary>
    [HttpPost("{id:guid}/shadow-run")]
    public async Task<IActionResult> ShadowRun(Guid id, [FromBody] ShadowRunRequest? req, [FromServices] ShadowRunService shadow, CancellationToken ct)
    {
        var cases = req?.Cases is { Count: > 0 } c
            ? c.Select((x, i) => new ShadowCase(string.IsNullOrWhiteSpace(x.Name) ? $"Kasus {i + 1}" : x.Name.Trim(), x.Facts ?? [])).ToList()
            : ShadowCases.Default.ToList();
        if (cases.Count > 200) return BadRequest(new { message = "Maksimal 200 kasus per shadow run." });
        var (report, error) = await shadow.RunAsync(id, cases, ct);
        return report is null ? BadRequest(new { message = error }) : Ok(new
        {
            active = new { id = report.ActiveId, name = report.ActiveName },
            draft = new { id = report.DraftId, name = report.DraftName },
            total = report.Rows.Count, changed = report.Changed,
            rows = report.Rows.Select(r => new { r.Name, r.Changed, active = r.Active, draft = r.Draft }),
        });
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive([FromQuery] DateTime? date)
    {
        var effectiveDate = date ?? DateTime.UtcNow;
        var version = await versionManager.GetActiveVersionAsync(effectiveDate);
        if (version is null) return NotFound(new { message = "No active version for the given date." });
        return Ok(new
        {
            version.Id, version.Name, version.BtkiEdition,
            State = version.State.ToString(), version.IsActive,
            version.EffectiveFrom, version.EffectiveTo
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var version = await versionManager.GetVersionAsync(id);
        if (version is null) return NotFound();
        return Ok(version);
    }

    [HttpPost("{id:guid}/transition")]
    public async Task<IActionResult> Transition(Guid id, [FromBody] TransitionRequest req)
    {
        if (!Enum.TryParse<ImportState>(req.NewState, true, out var newState))
            return BadRequest(new { message = $"Invalid state: {req.NewState}" });

        var success = await versionManager.TransitionStateAsync(id, newState);
        return success ? Ok(new { message = $"Transitioned to {newState}" })
                       : BadRequest(new { message = "Transition not allowed from current state." });
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var success = await versionManager.ActivateVersionAsync(id);
        return success ? Ok(new { message = "Version activated." })
                       : BadRequest(new { message = "Cannot activate. Version must be in Indexed state." });
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var success = await versionManager.DeactivateVersionAsync(id);
        return success ? Ok(new { message = "Version deactivated." })
                       : BadRequest(new { message = "Version is not active." });
    }
}

public record TransitionRequest(string NewState);
public record CloneVersionRequest(string? Name);
public record ShadowRunCase(string? Name, Dictionary<string, string?>? Facts);
public record ShadowRunRequest(List<ShadowRunCase>? Cases);
public record CreateVersionRequest(string Name, string? BtkiEdition, string? RegulationNumber,
    DateTime EffectiveFrom, DateTime? EffectiveTo);
