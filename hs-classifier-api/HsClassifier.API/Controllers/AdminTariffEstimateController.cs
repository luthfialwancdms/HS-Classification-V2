using System.Security.Claims;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HsClassifier.API.Controllers;

/// <summary>
/// Saklar estimasi AI untuk PPN, PPh 22 dan skema FTA. Hanya Admin. Saat mati, layar dan ekspor kembali
/// menampilkan "tidak tersedia" untuk tarif yang tidak ada di database.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/ai/tariff-estimate")]
public class AdminTariffEstimateController(TariffEstimateService estimates) : ControllerBase
{
    public record SetRequest(bool Aktif);

    private string Actor => User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "admin";

    private async Task<object> Body(CancellationToken ct) =>
        new { aktif = await estimates.IsEnabledAsync(ct), jumlahTersimpan = await estimates.CachedCountAsync(ct) };

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await Body(ct));

    [HttpPut]
    public async Task<IActionResult> Set([FromBody] SetRequest request, CancellationToken ct)
    {
        await estimates.SetEnabledAsync(request.Aktif, Actor, ct);
        return Ok(await Body(ct));
    }

    /// <summary>Hapus semua estimasi yang tersimpan. Kode yang dibuka lagi akan meminta AI lagi.</summary>
    [HttpDelete("cache")]
    public async Task<IActionResult> ClearCache(CancellationToken ct)
    {
        var removed = await estimates.ClearCacheAsync(ct);
        return Ok(new { dihapus = removed });
    }
}
