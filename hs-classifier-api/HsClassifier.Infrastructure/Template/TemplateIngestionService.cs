using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using HsClassifier.Application.Interfaces;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Template;

/// <summary>
/// Tasks 5B.1–5B.3: CSV/Excel template generation, validation, preview diff, and commit.
/// </summary>
public class TemplateIngestionService(HsClassifierDbContext db, IVersionManager versionManager)
{
    // ─── Column definitions (single source of truth) ─────────────────────────
    private static readonly (string Name, string Example, bool Required)[] Columns =
    [
        ("code",           "0101.21.00",   true),
        ("description_id", "Bibit kuda",   true),
        ("description_en", "Breeding horses", false),
        ("parent_code",    "0101.21",      true),
        ("indent_level",   "3",            true),
        ("mfn_rate",       "5",            false),
        ("mfn_rate_unit",  "PERCENT",      false),
        ("ppn_rate",       "11",           false),
        ("pph22_rate",     "7.5",          false),
        ("effective_from", "2022-04-01",   true),
        ("effective_to",   "",             false),
        ("status",         "A",            true),
    ];

    // ─── 5B.1: Template downloads ─────────────────────────────────────────────

    public byte[] GenerateCsvTemplate()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Columns.Select(c => c.Name)));
        sb.AppendLine(string.Join(",", Columns.Select(c => c.Example)));
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] GenerateExcelTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("BTKI Template");

        // Header row
        for (int i = 0; i < Columns.Length; i++)
        {
            var (name, _, required) = Columns[i];
            var cell = ws.Cell(1, i + 1);
            cell.Value = name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = required
                ? XLColor.LightBlue : XLColor.LightGray;
            // Tooltip via comment
            cell.GetComment().AddText(required ? $"{name} (WAJIB)" : $"{name} (opsional)");
        }

        // Example row
        for (int i = 0; i < Columns.Length; i++)
            ws.Cell(2, i + 1).Value = Columns[i].Example;

        ws.Columns().AdjustToContents();

        // Instructions sheet
        var info = wb.Worksheets.Add("Petunjuk");
        info.Cell(1, 1).Value = "Petunjuk Pengisian Template BTKI";
        info.Cell(1, 1).Style.Font.Bold = true;
        info.Cell(2, 1).Value = "code         : Format XXXX.XX.XX (8 digit, wajib)";
        info.Cell(3, 1).Value = "status       : A = aktif, I = tidak aktif";
        info.Cell(4, 1).Value = "mfn_rate_unit: PERCENT | SPECIFIC | COMPOUND";
        info.Cell(5, 1).Value = "indent_level : 0=seksi, 1=bab, 2=heading, 3=subheading, 4=tariff line";
        info.Cell(6, 1).Value = "effective_from/to: format YYYY-MM-DD";
        info.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── 5B.2: Validate uploaded file ─────────────────────────────────────────

    public TemplateValidationResult ValidateFile(Stream stream, string fileName)
    {
        var rows = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? ReadExcel(stream)
            : ReadCsv(stream);

        var result = new TemplateValidationResult { TotalRows = rows.Count };
        var codeRegex = new System.Text.RegularExpressions.Regex(@"^\d{4}\.\d{2}\.\d{2}$");

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNum = i + 2; // 1-based, skip header

            // code format
            if (!row.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
                result.Errors.Add(new TemplateRowError(rowNum, "code", "MISSING_REQUIRED", code, "Masukkan kode HS format XXXX.XX.XX"));
            else if (!codeRegex.IsMatch(code))
                result.Errors.Add(new TemplateRowError(rowNum, "code", "INVALID_FORMAT", code, "Format harus XXXX.XX.XX"));

            // description_id required
            if (!row.TryGetValue("description_id", out var desc) || string.IsNullOrWhiteSpace(desc))
                result.Errors.Add(new TemplateRowError(rowNum, "description_id", "MISSING_REQUIRED", null, "Uraian Bahasa Indonesia wajib diisi"));

            // status
            if (row.TryGetValue("status", out var status) && status != "A" && status != "I")
                result.Errors.Add(new TemplateRowError(rowNum, "status", "INVALID_VALUE", status, "Harus A atau I"));

            // effective_from
            if (row.TryGetValue("effective_from", out var effFrom) && !string.IsNullOrWhiteSpace(effFrom))
                if (!DateTime.TryParseExact(effFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    result.Errors.Add(new TemplateRowError(rowNum, "effective_from", "INVALID_DATE", effFrom, "Format harus YYYY-MM-DD"));

            // mfn_rate_unit
            if (row.TryGetValue("mfn_rate_unit", out var unit) && !string.IsNullOrWhiteSpace(unit))
                if (unit != "PERCENT" && unit != "SPECIFIC" && unit != "COMPOUND")
                    result.Warnings.Add(new TemplateRowError(rowNum, "mfn_rate_unit", "INVALID_VALUE", unit, "Gunakan PERCENT, SPECIFIC, atau COMPOUND"));

            if (result.Errors.Count == 0) result.ValidRows++;
        }

        result.IsValid = result.Errors.Count == 0;
        result.ParsedRows = rows;
        return result;
    }

    // ─── 5B.3: Preview diff ───────────────────────────────────────────────────

    public async Task<TemplatePreview> PreviewAsync(
        TemplateValidationResult validated, Guid versionId, CancellationToken ct = default)
    {
        var existingCodes = await db.BtkiTariffLines
            .Where(t => t.VersionId == versionId)
            .Select(t => new { t.Code, t.DescriptionId })
            .ToDictionaryAsync(t => t.Code, t => t.DescriptionId, ct);

        var preview = new TemplatePreview();

        foreach (var row in validated.ParsedRows)
        {
            if (!row.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code)) continue;

            if (existingCodes.TryGetValue(code, out var existingDesc))
            {
                var newDesc = row.GetValueOrDefault("description_id", "");
                if (existingDesc != newDesc)
                    preview.ToUpdate.Add(new TemplateDiffRow { Code = code, OldValue = existingDesc, NewValue = newDesc, Row = row });
                else
                    preview.Unchanged.Add(code);
            }
            else
            {
                preview.ToInsert.Add(new TemplateDiffRow { Code = code, Row = row });
            }
        }

        return preview;
    }

    // ─── 5B.3: Commit ─────────────────────────────────────────────────────────

    public async Task<(int Inserted, int Updated)> CommitAsync(
        TemplatePreview preview, Guid versionId, string fileName,
        IngestionMethod method, CancellationToken ct = default)
    {
        var checksum = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(fileName + DateTime.UtcNow.Ticks)))[..12].ToLower();

        // INSERT new rows
        var newLines = preview.ToInsert.Select((d, i) => BuildLine(d.Row, versionId, method, checksum, i)).ToList();
        await db.BtkiTariffLines.AddRangeAsync(newLines, ct);

        // UPDATE changed rows
        foreach (var d in preview.ToUpdate)
        {
            var existing = await db.BtkiTariffLines
                .FirstOrDefaultAsync(t => t.VersionId == versionId && t.Code == d.Code, ct);
            if (existing is null) continue;
            existing.DescriptionId = d.Row.GetValueOrDefault("description_id", existing.DescriptionId) ?? existing.DescriptionId;
            existing.DescriptionEn = d.Row.GetValueOrDefault("description_en") ?? existing.DescriptionEn;
            existing.IngestionMethod = method;
        }

        await db.SaveChangesAsync(ct);
        return (newLines.Count, preview.ToUpdate.Count);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static BtkiTariffLine BuildLine(Dictionary<string, string> row, Guid versionId,
        IngestionMethod method, string checksum, int rowNum)
    {
        var code = row.GetValueOrDefault("code", "");
        var parts = code.Split('.');
        DateTime.TryParseExact(row.GetValueOrDefault("effective_from", ""), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var effFrom);
        DateTime.TryParseExact(row.GetValueOrDefault("effective_to", ""), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var effTo);

        return new BtkiTariffLine
        {
            VersionId     = versionId,
            Code          = code,
            ParentCode    = row.GetValueOrDefault("parent_code"),
            ChapterCode   = parts.Length > 0 ? parts[0][..2] : null,
            HeadingCode   = parts.Length > 0 ? parts[0] : null,
            SubheadingCode = parts.Length > 1 ? $"{parts[0]}.{parts[1]}" : null,
            IndentLevel   = int.TryParse(row.GetValueOrDefault("indent_level"), out var il) ? il : 3,
            DescriptionId = row.GetValueOrDefault("description_id", ""),
            DescriptionEn = row.GetValueOrDefault("description_en"),
            IngestionMethod = method,
            SourceId      = $"template:upload:{checksum}:row_{rowNum + 2}",
            IsActive      = row.GetValueOrDefault("status", "A") == "A",
            EffectiveFrom = effFrom == default ? null : effFrom,
            EffectiveTo   = effTo == default ? null : effTo,
        };
    }

    private static List<Dictionary<string, string>> ReadCsv(Stream stream)
    {
        var rows = new List<Dictionary<string, string>>();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var headerLine = reader.ReadLine();
        if (headerLine is null) return rows;
        var headers = headerLine.Split(',').Select(h => h.Trim().Trim('"').ToLowerInvariant()).ToArray();

        while (reader.ReadLine() is { } line)
        {
            var values = line.Split(',').Select(v => v.Trim().Trim('"')).ToArray();
            var row = new Dictionary<string, string>();
            for (int i = 0; i < headers.Length && i < values.Length; i++)
                row[headers[i]] = values[i];
            rows.Add(row);
        }
        return rows;
    }

    private static List<Dictionary<string, string>> ReadExcel(Stream stream)
    {
        var rows = new List<Dictionary<string, string>>();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var headers = ws.Row(1).Cells()
            .Select(c => c.GetString().Trim().ToLowerInvariant()).ToArray();

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var dict = new Dictionary<string, string>();
            for (int i = 0; i < headers.Length; i++)
                dict[headers[i]] = row.Cell(i + 1).GetString().Trim();
            rows.Add(dict);
        }
        return rows;
    }
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public class TemplateValidationResult
{
    public bool IsValid { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public List<TemplateRowError> Errors { get; set; } = new();
    public List<TemplateRowError> Warnings { get; set; } = new();
    public List<Dictionary<string, string>> ParsedRows { get; set; } = new();
}

public record TemplateRowError(int RowNumber, string ColumnName, string ErrorType, string? ActualValue, string SuggestedFix);

public class TemplatePreview
{
    public List<TemplateDiffRow> ToInsert { get; set; } = new();
    public List<TemplateDiffRow> ToUpdate { get; set; } = new();
    public List<string> Unchanged { get; set; } = new();
}

public class TemplateDiffRow
{
    public string Code { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Dictionary<string, string> Row { get; set; } = new();
}
