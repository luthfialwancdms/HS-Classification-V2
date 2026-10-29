using HsClassifier.Application.Interfaces;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Domain.Logic;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Services;

public class VersionManager(HsClassifierDbContext db) : IVersionManager
{
    public async Task<Guid> CreateVersionAsync(string name, string btkiEdition,
        string regulationNumber, DateTime effectiveFrom, DateTime? effectiveTo, string createdBy)
    {
        var version = new NomenclatureVersion
        {
            Name = name,
            BtkiEdition = btkiEdition,
            RegulationNumber = regulationNumber,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedBy = createdBy,
            State = ImportState.Uploaded,
            IsActive = false
        };
        db.NomenclatureVersions.Add(version);
        await db.SaveChangesAsync();
        return version.Id;
    }

    public async Task<NomenclatureVersion?> GetVersionAsync(Guid versionId)
        => await db.NomenclatureVersions.FindAsync(versionId);

    public async Task<NomenclatureVersion?> GetActiveVersionAsync(DateTime effectiveDate)
        => await db.NomenclatureVersions
            .Where(v => v.IsActive
                && v.State == ImportState.Active
                && v.EffectiveFrom <= effectiveDate
                && (v.EffectiveTo == null || v.EffectiveTo >= effectiveDate))
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<NomenclatureVersion>> GetAllVersionsAsync()
        => await db.NomenclatureVersions
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();

    public async Task<bool> TransitionStateAsync(Guid versionId, ImportState newState)
    {
        var version = await db.NomenclatureVersions.FindAsync(versionId);
        if (version is null) return false;
        if (!VersionStateMachine.CanTransition(version.State, newState)) return false;

        version.State = newState;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateVersionAsync(Guid versionId)
    {
        var version = await db.NomenclatureVersions.FindAsync(versionId);
        if (version is null || version.State != ImportState.Indexed) return false;

        // Req 1.11: never activate while the latest import still has undecided ambiguities
        var latestBatchId = await db.ImportBatches.Where(b => b.VersionId == versionId)
            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id).Select(b => (long?)b.Id).FirstOrDefaultAsync();
        if (latestBatchId is not null
            && await db.AmbiguityItems.AnyAsync(i => i.BatchId == latestBatchId && i.Decision == AmbiguityDecision.Pending))
            return false;

        // Deactivate any overlapping active versions
        var overlapping = await db.NomenclatureVersions
            .Where(v => v.IsActive && v.Id != versionId
                && v.EffectiveFrom <= (version.EffectiveTo ?? DateTime.MaxValue)
                && (v.EffectiveTo == null || v.EffectiveTo >= version.EffectiveFrom))
            .ToListAsync();

        foreach (var ov in overlapping)
        {
            // Same as Deactivate: back to Indexed, otherwise the replaced version could never be re-activated (rollback)
            ov.IsActive = false;
            if (ov.State == ImportState.Active) ov.State = ImportState.Indexed;
        }

        version.State = ImportState.Active;
        version.IsActive = true;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateVersionAsync(Guid versionId)
    {
        var version = await db.NomenclatureVersions.FindAsync(versionId);
        if (version is null || !version.IsActive) return false;

        // Return to Indexed so the version can be re-activated; leaving State=Active with
        // IsActive=false left it stuck (couldn't activate — needs Indexed; couldn't deactivate — needs IsActive).
        version.IsActive = false;
        version.State = ImportState.Indexed;
        await db.SaveChangesAsync();
        return true;
    }
}
