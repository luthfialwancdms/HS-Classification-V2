using System.Security.Claims;
using System.Text.Json;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.API.Controllers;

[ApiController]
[Authorize] // all roles may read their own cases; writes need Classifier or Admin
[Route("api/classification")]
public class ClassificationController(
    ClassificationOrchestrator orchestrator,
    HsClassifierDbContext db,
    TariffLookupService tariffLookup) : ControllerBase
{
    private const string Writers = $"{nameof(UserRole.Admin)},{nameof(UserRole.Classifier)}";

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool SeesAll => CaseQueries.SeesAll(User.FindAll(ClaimTypes.Role).Select(c => c.Value));

    /// <summary>Loads a case only if the caller owns it or is Admin/ExpertReviewer (prevents reading others' cases by id).</summary>
    private async Task<ClassificationCase?> FindOwnCaseAsync(long caseId)
    {
        var c = await db.ClassificationCases.FindAsync(caseId);
        return c is not null && (c.UserId == UserId || SeesAll) ? c : null;
    }

    /// <summary>Riwayat klasifikasi (paged, newest first). Classifier/Viewer: own cases; Admin/ExpertReviewer: all.</summary>
    /// <param name="status">Determined | NeedClarification | NeedExpertReview | Rejected</param>
    /// <param name="q">Cari di kode final atau fakta barang (nama dagang/generik)</param>
    [HttpGet("cases")]
    public async Task<IActionResult> ListCases([FromQuery] CaseStatus? status, [FromQuery] string? q,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.ClassificationCases.Visible(UserId, SeesAll).Filter(status, q, from, to);

        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new { c.Id, c.UserId, c.FactsJson, Status = c.Status.ToString(), c.FinalCode,
                c.FinalDescription, Confidence = c.Confidence.ToString(), c.CreatedAt, c.ClassificationDate })
            .ToListAsync();

        var emails = await db.AppUsers.Where(u => rows.Select(r => r.UserId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email);

        return Ok(new
        {
            total, page, pageSize,
            items = rows.Select(r =>
            {
                var facts = ParseFacts(r.FactsJson);
                return new
                {
                    r.Id, r.Status, r.FinalCode, r.FinalDescription, r.Confidence, r.CreatedAt, r.ClassificationDate,
                    GoodsName = facts.GetValueOrDefault("commercial_name") ?? facts.GetValueOrDefault("generic_name"),
                    GenericName = facts.GetValueOrDefault("generic_name"),
                    User = emails.GetValueOrDefault(r.UserId),
                };
            }),
        });
    }

    /// <summary>Detail satu case: fakta, kandidat, status, dan riwayat keputusan ahli.</summary>
    [HttpGet("{caseId:long}")]
    public async Task<IActionResult> GetCase(long caseId)
    {
        var c = await FindOwnCaseAsync(caseId);
        if (c is null) return NotFound(new { message = "Case not found." });
        var corrections = await db.ExpertCorrections.Where(e => e.CaseId == caseId)
            .OrderBy(e => e.CreatedAt).ToListAsync();
        return Ok(new
        {
            c.Id, Status = c.Status.ToString(), c.FinalCode, c.FinalDescription,
            Confidence = c.Confidence?.ToString(), c.ConfidenceScore,
            ConfidenceDetail = c.ConfidenceJson is null ? (JsonElement?)null : JsonDocument.Parse(c.ConfidenceJson).RootElement,
            c.CreatedAt, c.ClassificationDate,
            Facts = ParseFacts(c.FactsJson).Where(kv => kv.Key != ClassificationOrchestrator.AiSuggestionKey)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            CandidateCodes = c.CandidateCodesJson is null ? [] : JsonSerializer.Deserialize<List<string>>(c.CandidateCodesJson) ?? [],
            Corrections = corrections.Select(e => new
                { e.Action, e.BeforeCode, e.AfterCode, e.BeforeStatus, e.AfterStatus, e.Reason, e.CreatedAt }),
        });
    }

    private static Dictionary<string, string?> ParseFacts(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    /// <summary>Mulai klasifikasi dari fakta barang.</summary>
    /// <remarks>
    /// Contoh body: <c>{ "facts": { "commercial_name": "Excavator", "primary_function": "menggali tanah", "assembled_status": "complete" } }</c>.
    /// Response <c>status</c>: DETERMINED (finalCode + tariffs), NEED_CLARIFICATION (questions + candidateCodes;
    /// jawab lewat <c>POST /{caseId}/facts</c>), NEED_EXPERT_REVIEW (tanpa kode final, candidateCodes untuk ahli),
    /// atau ERROR (message berisi alasan validasi). Semua status selain ERROR mengembalikan caseId.
    /// </remarks>
    [Authorize(Roles = Writers)]
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] ClassificationStartRequest req)
        => Ok(await orchestrator.ClassifyAsync(UserId, req.Facts ?? new()));

    /// <summary>Tambah fakta (jawaban klarifikasi) ke case; fakta lama digabung, lalu klasifikasi dijalankan ulang.</summary>
    [Authorize(Roles = Writers)]
    [HttpPost("{caseId:long}/facts")]
    public async Task<IActionResult> SubmitFacts(long caseId, [FromBody] ClassificationStartRequest req)
    {
        var existing = await FindOwnCaseAsync(caseId);
        if (existing is null) return NotFound(new { message = "Case not found." });
        return Ok(await orchestrator.ClassifyAsync(UserId, req.Facts ?? new(), existing));
    }

    /// <summary>Ajukan case berstatus Determined ke antrian ahli ("Minta Expert Review"). Alasan wajib.</summary>
    [Authorize(Roles = Writers)]
    [HttpPost("{caseId:long}/request-review")]
    public async Task<IActionResult> RequestReview(long caseId, [FromBody] ReviewDecisionRequest req,
        [FromServices] ExpertReviewService review)
    {
        if (await FindOwnCaseAsync(caseId) is null) return NotFound(new { message = "Case not found." });
        var r = await review.RequestReviewAsync(caseId, req.Reason, UserId);
        return r.Ok ? Ok(new { r.Action }) : BadRequest(new { message = r.Error });
    }

    /// <summary>Status, kode final, uraian resmi, dan confidence sebuah case.</summary>
    [HttpGet("{caseId:long}/status")]
    public async Task<IActionResult> GetStatus(long caseId)
    {
        var c = await FindOwnCaseAsync(caseId);
        if (c is null) return NotFound(new { message = "Case not found." });
        return Ok(new
        {
            c.Id, Status = c.Status.ToString(), c.FinalCode,
            c.FinalDescription, Confidence = c.Confidence?.ToString(), c.CreatedAt
        });
    }

    /// <summary>Rantai reasoning KUM HS (urut) beserta sourceIds tiap langkah.</summary>
    [HttpGet("{caseId:long}/reasoning")]
    public async Task<IActionResult> GetReasoning(long caseId)
    {
        if (await FindOwnCaseAsync(caseId) is null) return NotFound(new { message = "Case not found." });
        var events = await db.ReasoningEvents
            .Where(e => e.CaseId == caseId)
            .OrderBy(e => e.EventOrder)
            .Select(e => new { e.EventOrder, e.RuleApplied, e.Decision, e.Reasoning, e.SourceIdsJson, e.Timestamp })
            .ToListAsync();
        // Req 17.3: expose source_ids so the UI can show traceability per step
        return Ok(events.Select(e => new
        {
            e.EventOrder, e.RuleApplied, e.Decision, e.Reasoning, e.Timestamp,
            SourceIds = e.SourceIdsJson is null ? [] : JsonSerializer.Deserialize<List<string>>(e.SourceIdsJson) ?? []
        }));
    }

    /// <summary>Tarif MFN, FTA yang tersedia, PPN, PPh 22 untuk kode final (dengan disclaimer). 404 jika belum ada kode final.</summary>
    [HttpGet("{caseId:long}/tariffs")]
    public async Task<IActionResult> GetTariffs(long caseId)
    {
        var c = await FindOwnCaseAsync(caseId);
        if (c?.FinalCode is null) return NotFound(new { message = "No final code yet." });
        return Ok(await tariffLookup.GetRatesAsync(c.FinalCode));
    }
}

public class ClassificationStartRequest
{
    public Dictionary<string, string?>? Facts { get; set; }
}
