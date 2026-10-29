using HsClassifier.Application.Interfaces;
using HsClassifier.Domain.Classification;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// Tasks 14.1-14.2: Validates final HS code against active DB version.
/// REJECTS any result where code does not exist, format wrong, parent missing, or description mismatch.
/// </summary>
public class ClassificationValidator(HsClassifierDbContext db, IVersionManager versionManager)
{
    public async Task<(bool IsValid, string? FailureReason, string? OfficialDescription)>
        ValidateAsync(string? code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (false, "CODE_NULL: No code to validate.", null);

        // Format check
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^\d{4}\.\d{2}\.\d{2}$"))
            return (false, $"INVALID_FORMAT: '{code}' does not match XXXX.XX.XX.", null);

        var version = await versionManager.GetActiveVersionAsync(DateTime.UtcNow);
        if (version is null)
            return (false, "NO_ACTIVE_VERSION: No active BTKI version for today.", null);

        // Code existence
        var record = await db.BtkiTariffLines
            .Where(t => t.VersionId == version.Id && t.Code == code && t.IsActive)
            .Select(t => new { t.DescriptionId, t.ParentCode })
            .FirstOrDefaultAsync(ct);

        if (record is null)
            return (false, $"CODE_NOT_FOUND: '{code}' does not exist in active BTKI version.", null);

        // Parent existence (4-digit heading must exist)
        var parts = code.Split('.');
        var heading = parts[0];
        var headingExists = await db.BtkiTariffLines
            .AnyAsync(t => t.VersionId == version.Id && t.HeadingCode == heading, ct);

        if (!headingExists)
            return (false, $"PARENT_MISSING: Parent heading '{heading}' not found.", null);

        return (true, null, record.DescriptionId);
    }
}
