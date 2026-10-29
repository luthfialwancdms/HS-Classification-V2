using HsClassifier.Application.Interfaces;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.API.Controllers;

[ApiController]
[Route("api/btki")]
public class BtkiController(HsClassifierDbContext db, IVersionManager versionManager) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(new { message = "Query must be at least 2 characters." });

        var version = await versionManager.GetActiveVersionAsync(DateTime.UtcNow);
        if (version is null) return NotFound(new { message = "No active BTKI version." });

        var results = await db.BtkiTariffLines
            .Where(t => t.VersionId == version.Id && t.IsActive
                && (t.Code.Contains(q)
                    || t.DescriptionId.Contains(q)
                    || (t.DescriptionEn != null && t.DescriptionEn.Contains(q))))
            .OrderBy(t => t.Code)
            .Take(limit)
            .Select(t => new
            {
                t.Code, t.ParentCode, t.IndentLevel,
                t.DescriptionId, t.DescriptionEn,
                t.ChapterCode, t.HeadingCode
            })
            .ToListAsync();

        return Ok(new { total = results.Count, data = results });
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetByCode(string code)
    {
        var version = await versionManager.GetActiveVersionAsync(DateTime.UtcNow);
        if (version is null) return NotFound(new { message = "No active BTKI version." });

        var tariffLine = await db.BtkiTariffLines
            .Where(t => t.VersionId == version.Id && t.Code == code)
            .Select(t => new
            {
                t.Code, t.ParentCode, t.IndentLevel,
                t.DescriptionId, t.DescriptionEn,
                t.ChapterCode, t.HeadingCode, t.SubheadingCode,
                t.IsActive, t.EffectiveFrom, t.EffectiveTo, t.IngestionMethod
            })
            .FirstOrDefaultAsync();

        if (tariffLine is null) return NotFound();
        return Ok(tariffLine);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var version = await versionManager.GetActiveVersionAsync(DateTime.UtcNow);
        if (version is null) return NotFound(new { message = "No active BTKI version." });

        var total = await db.BtkiTariffLines.CountAsync(t => t.VersionId == version.Id);
        var active = await db.BtkiTariffLines.CountAsync(t => t.VersionId == version.Id && t.IsActive);
        var chapters = await db.BtkiTariffLines
            .Where(t => t.VersionId == version.Id && t.IsActive)
            .Select(t => t.ChapterCode)
            .Distinct()
            .CountAsync();

        return Ok(new
        {
            version = version.Name,
            versionId = version.Id,
            total, active, chapters
        });
    }
}
