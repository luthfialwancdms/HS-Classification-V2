using System.Security.Claims;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HsClassifier.API.Controllers;

/// <summary>
/// Expert review decisions. The queue itself is <c>GET /api/classification/cases?status=NeedExpertReview</c>
/// (Admin/ExpertReviewer see every case there).
/// </summary>
[ApiController]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.ExpertReviewer)}")]
[Route("api/review")]
public class ReviewController(ExpertReviewService review, TariffLookupService tariffs) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Setujui case dengan kode final. Kode di luar kandidat sistem diizinkan (dicatat EXPERT_OVERRIDE)
    /// asal ada di BTKI aktif dan lolos validator. Alasan wajib.
    /// </summary>
    [HttpPost("{caseId:long}/approve")]
    public async Task<IActionResult> Approve(long caseId, [FromBody] ReviewDecisionRequest req)
    {
        var r = await review.ApproveAsync(caseId, req.Code, req.Reason, UserId);
        if (!r.Ok) return BadRequest(new { message = r.Error });
        return Ok(new { r.Action, r.FinalCode, r.FinalDescription, tariffs = await tariffs.GetRatesAsync(r.FinalCode!) });
    }

    /// <summary>Tolak rekomendasi; case menjadi Rejected. Alasan wajib.</summary>
    [HttpPost("{caseId:long}/reject")]
    public async Task<IActionResult> Reject(long caseId, [FromBody] ReviewDecisionRequest req)
    {
        var r = await review.RejectAsync(caseId, req.Reason, UserId);
        return r.Ok ? Ok(new { r.Action }) : BadRequest(new { message = r.Error });
    }
}

public record ReviewDecisionRequest(string? Code, string? Reason);
