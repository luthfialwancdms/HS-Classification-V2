using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Template;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HsClassifier.API.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/template")]
public class AdminTemplateController(TemplateIngestionService templateService) : ControllerBase
{
    // GET /api/admin/template/download/csv
    [HttpGet("download/csv")]
    public IActionResult DownloadCsv()
    {
        var bytes = templateService.GenerateCsvTemplate();
        return File(bytes, "text/csv", "btki_template.csv");
    }

    // GET /api/admin/template/download/xlsx
    [HttpGet("download/xlsx")]
    public IActionResult DownloadExcel()
    {
        var bytes = templateService.GenerateExcelTemplate();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "btki_template.xlsx");
    }

    // POST /api/admin/template/validate
    [HttpPost("validate")]
    [Consumes("multipart/form-data")]
    public IActionResult Validate(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "No file." });
        using var stream = file.OpenReadStream();
        var result = templateService.ValidateFile(stream, file.FileName);
        return Ok(new
        {
            isValid      = result.IsValid,
            totalRows    = result.TotalRows,
            validRows    = result.ValidRows,
            errorCount   = result.Errors.Count,
            warningCount = result.Warnings.Count,
            errors       = result.Errors.Take(50),
            warnings     = result.Warnings.Take(50),
        });
    }

    // POST /api/admin/template/preview/{versionId}
    [HttpPost("preview/{versionId:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Preview(Guid versionId, IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "No file." });
        using var stream = file.OpenReadStream();
        var validated = templateService.ValidateFile(stream, file.FileName);
        if (!validated.IsValid)
            return UnprocessableEntity(new { message = "Fix errors before previewing.", errors = validated.Errors.Take(20) });

        var preview = await templateService.PreviewAsync(validated, versionId);
        return Ok(new
        {
            insertCount   = preview.ToInsert.Count,
            updateCount   = preview.ToUpdate.Count,
            unchangedCount = preview.Unchanged.Count,
            toInsert      = preview.ToInsert.Take(20).Select(r => new { r.Code, r.Row }),
            toUpdate      = preview.ToUpdate.Take(20).Select(r => new { r.Code, r.OldValue, r.NewValue }),
        });
    }

    // POST /api/admin/template/commit/{versionId}
    [HttpPost("commit/{versionId:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Commit(Guid versionId, IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "No file." });

        var method = file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? IngestionMethod.XlsxTemplate : IngestionMethod.CsvTemplate;

        using var stream = file.OpenReadStream();
        var validated = templateService.ValidateFile(stream, file.FileName);
        if (!validated.IsValid)
            return UnprocessableEntity(new { message = "Fix errors before committing.", errors = validated.Errors.Take(20) });

        // Re-open stream for preview (already consumed)
        stream.Position = 0;
        var preview = await templateService.PreviewAsync(validated, versionId);
        var (inserted, updated) = await templateService.CommitAsync(preview, versionId, file.FileName, method);

        return Ok(new { message = "Commit successful.", inserted, updated, unchanged = preview.Unchanged.Count });
    }
}

