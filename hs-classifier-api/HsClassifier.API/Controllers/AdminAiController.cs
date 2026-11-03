using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HsClassifier.API.Controllers;

/// <summary>
/// Status koneksi AI dan riwayat penggunaannya. Hanya Admin. Tidak pernah mengembalikan API key,
/// prompt, atau isi respons AI.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/ai")]
public class AdminAiController(IAiProvider provider, AiConnectionChecker checker, AiUsageReportService report) : ControllerBase
{
    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    private Dictionary<string, object?> StatusBody(AiStatusSnapshot? s) => new()
    {
        ["pernahDicek"] = s is not null,
        ["terhubung"] = s?.Connected ?? false,
        ["provider"] = s?.Provider ?? provider.ProviderName,
        ["model"] = s?.Model ?? provider.Model,
        ["keyTerkonfigurasi"] = provider.IsConfigured,
        ["latensiMs"] = s?.LatencyMs,
        ["waktuCek"] = s is null ? null : Utc(s.CheckedAtUtc),
        ["pesanError"] = s?.ErrorMessage,
        ["kodeError"] = s?.ErrorCode,
    };

    /// <summary>Hasil cek terakhir dari memori server. Tidak memanggil AI.</summary>
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(StatusBody(checker.Last));

    /// <summary>
    /// Cek koneksi aktif: satu permintaan kecil ke AI (timeout 10 detik). Minimal 10 detik antar-permintaan nyata;
    /// bila terlalu sering, hasil terakhir dikembalikan dengan <c>dariCache: true</c>.
    /// </summary>
    [HttpPost("check")]
    public async Task<IActionResult> Check(CancellationToken ct)
    {
        var o = await checker.CheckAsync(ct);
        var body = StatusBody(o.Status);
        body["dariCache"] = o.FromCache;
        body["bisaCekLagiDalamDetik"] = o.RetryAfterSeconds;
        return Ok(body);
    }

    /// <summary>
    /// Ringkasan penggunaan: hari ini, 7 hari, 30 hari, semua waktu, dan rincian per hari. Hari mengikuti offset
    /// konfigurasi (default WIB). Angka total sudah termasuk cek koneksi; <c>klasifikasi</c> dan <c>cekKoneksi</c> memisahkannya.
    /// </summary>
    [HttpGet("usage")]
    public async Task<IActionResult> Usage([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var u = await report.SummaryAsync(days, ct);
        return Ok(new
        {
            zonaWaktuOffsetMenit = u.OffsetMinutes,
            hariIni = Map(u.Today),
            tujuhHari = Map(u.Last7Days),
            tigaPuluhHari = Map(u.Last30Days),
            semua = Map(u.AllTime),
            perHari = u.PerDay.Select(d => new { tanggal = d.Date.ToString("yyyy-MM-dd"), ringkasan = Map(d.Usage) }),
            requestSuksesTanpaDataToken = u.SuccessWithoutTokenData,
        });
    }

    /// <summary>Riwayat request ke AI, terbaru dulu. <c>from</c>/<c>to</c> adalah tanggal kalender di zona waktu laporan (to inklusif).</summary>
    [HttpGet("requests")]
    public async Task<IActionResult> Requests([FromQuery] AiCallStatus? status, [FromQuery] AiCallKind? kind,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var p = await report.ListAsync(kind, status, from, to, page, pageSize, ct);
        return Ok(new
        {
            total = p.Total, page = p.Page, pageSize = p.PageSize,
            items = p.Items.Select(i => new
            {
                id = i.Id, waktu = Utc(i.CreatedAtUtc), jenis = i.Kind.ToString(), provider = i.Provider, host = i.Host,
                model = i.Model, caseId = i.CaseId, status = i.Status.ToString(), kodeError = i.ErrorCode,
                pesanError = AiErrorMessages.For(i.ErrorCode), httpStatus = i.HttpStatus, latensiMs = i.LatencyMs,
                tokenMasuk = i.PromptTokens, tokenKeluar = i.CompletionTokens, tokenTotal = i.TotalTokens,
            }),
        });
    }

    private static object Map(AiUsageBlock b) => new
    {
        request = b.All.Requests, sukses = b.All.Success, gagal = b.All.Failed,
        tokenMasuk = b.All.PromptTokens, tokenKeluar = b.All.CompletionTokens, tokenTotal = b.All.TotalTokens,
        klasifikasi = Mini(b.Classification),
        cekKoneksi = Mini(b.Check),
        estimasiTarif = Mini(b.TariffEstimate ?? new AiUsageCounts(0, 0, 0, 0, 0, 0)),
    };

    private static object Mini(AiUsageCounts c) => new
    {
        request = c.Requests, sukses = c.Success, gagal = c.Failed, tokenTotal = c.TotalTokens,
    };
}
