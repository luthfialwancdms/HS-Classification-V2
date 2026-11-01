using System.Text.Encodings.Web;
using System.Text.Json;
using HsClassifier.Domain.Entities;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// Builds the report model of one case and writes it as JSON, DOCX or PDF. Authorisation (who may export which
/// case) is the controller's job; this class only reads.
/// </summary>
public class ExportService(HsClassifierDbContext db, TariffLookupService tariffLookup)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // keep "→" and Indonesian text readable
    };

    public async Task<CaseExportModel> BuildAsync(ClassificationCase c, CancellationToken ct = default)
    {
        var versionName = await db.NomenclatureVersions.Where(v => v.Id == c.VersionId)
            .Select(v => v.Name).FirstOrDefaultAsync(ct);

        var steps = (await db.ReasoningEvents.Where(e => e.CaseId == c.Id).OrderBy(e => e.EventOrder)
            .Select(e => new { e.EventOrder, e.RuleApplied, e.Decision, e.Reasoning, e.SourceIdsJson }).ToListAsync(ct))
            .Select(e => new ExportStep(e.EventOrder, e.RuleApplied, e.Decision, e.Reasoning, Parse<List<string>>(e.SourceIdsJson) ?? []))
            .ToList();

        var corrections = (await db.ExpertCorrections.Where(e => e.CaseId == c.Id).OrderBy(e => e.CreatedAt).ToListAsync(ct))
            .Select(e => new ExportCorrection(e.Action, e.BeforeCode, e.AfterCode, e.BeforeStatus, e.AfterStatus, e.Reason, e.CreatedAt))
            .ToList();

        // ai_suggestion is an internal cache of the term helper, not a fact about the goods
        var facts = (Parse<Dictionary<string, string?>>(c.FactsJson) ?? [])
            .Where(kv => kv.Key != ClassificationOrchestrator.AiSuggestionKey && !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value!);

        var isFinal = c.FinalCode is not null && c.Status == Domain.Enums.CaseStatus.Determined;
        return new CaseExportModel
        {
            CaseId = c.Id,
            StatusCode = c.Status.ToString(),
            Status = CaseExportModel.StatusLabel(c.Status),
            IsFinal = isFinal,
            Notice = isFinal ? null : "Bukan keputusan final. Case ini belum memiliki kode HS yang ditetapkan.",
            ClassificationDate = c.ClassificationDate,
            CreatedAt = c.CreatedAt,
            GeneratedAtUtc = DateTime.UtcNow,
            VersionName = versionName,
            FinalCode = c.FinalCode,
            FinalDescription = c.FinalDescription,
            Confidence = c.Confidence?.ToString(),
            ConfidenceScore = c.ConfidenceScore,
            ConfidenceDetail = Parse<ConfidenceDetailDto>(c.ConfidenceJson),
            Facts = facts,
            CandidateCodes = Parse<List<string>>(c.CandidateCodesJson) ?? [],
            Reasoning = steps,
            Corrections = corrections,
            Tariff = isFinal ? await tariffLookup.GetRatesAsync(c.FinalCode!, ct) : null,
        };
    }

    /// <param name="format">json | docx | pdf</param>
    /// <exception cref="ArgumentException">Unknown format.</exception>
    public ExportFile Render(CaseExportModel model, string format) => format.ToLowerInvariant() switch
    {
        "json" => new(JsonSerializer.SerializeToUtf8Bytes(model, JsonOptions), "application/json", "json"),
        "docx" => new(CaseExportDocx.Render(model),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
        "pdf" => new(CaseExportPdf.Render(model), "application/pdf", "pdf"),
        _ => throw new ArgumentException($"Format tidak dikenal: {format}", nameof(format)),
    };

    private static T? Parse<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return null; }
    }
}
