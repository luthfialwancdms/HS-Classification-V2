using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Tests;

/// <summary>Deactivate must not strand a version (the bug: State=Active + IsActive=false → stuck).</summary>
public class VersionLifecycleTests
{
    private static HsClassifierDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HsClassifierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(HsClassifierDbContext Db, Guid Id)> SeededActiveVersion()
    {
        var db = NewDb();
        var v = new NomenclatureVersion
        {
            Name = "BTKI 2022", EffectiveFrom = new DateTime(2022, 4, 1),
            State = ImportState.Active, IsActive = true,
        };
        db.NomenclatureVersions.Add(v);
        await db.SaveChangesAsync();
        return (db, v.Id);
    }

    [Fact]
    public async Task Deactivate_then_reactivate_round_trips()
    {
        var (db, id) = await SeededActiveVersion();
        var mgr = new VersionManager(db);

        Assert.True(await mgr.DeactivateVersionAsync(id));
        var afterOff = await db.NomenclatureVersions.FindAsync(id);
        Assert.False(afterOff!.IsActive);
        Assert.Equal(ImportState.Indexed, afterOff.State); // not stuck in Active

        Assert.True(await mgr.ActivateVersionAsync(id)); // Indexed → Active is allowed
        var afterOn = await db.NomenclatureVersions.FindAsync(id);
        Assert.True(afterOn!.IsActive);
        Assert.Equal(ImportState.Active, afterOn.State);
    }

    [Fact]
    public async Task Activating_a_new_version_leaves_the_old_one_ready_for_rollback()
    {
        var (db, oldId) = await SeededActiveVersion();
        var mgr = new VersionManager(db);
        var fresh = new NomenclatureVersion
        {
            Name = "BTKI PDF", EffectiveFrom = new DateTime(2022, 4, 1), State = ImportState.Indexed, IsActive = false,
        };
        db.NomenclatureVersions.Add(fresh);
        await db.SaveChangesAsync();

        Assert.True(await mgr.ActivateVersionAsync(fresh.Id));
        var old = await db.NomenclatureVersions.FindAsync(oldId);
        Assert.False(old!.IsActive);
        Assert.Equal(ImportState.Indexed, old.State);

        // rollback: switch back to the old version
        Assert.True(await mgr.ActivateVersionAsync(oldId));
        Assert.False((await db.NomenclatureVersions.FindAsync(fresh.Id))!.IsActive);
        Assert.Equal(oldId, (await mgr.GetActiveVersionAsync(new DateTime(2023, 1, 1)))!.Id);
    }

    [Fact]
    public async Task Deactivated_version_is_not_returned_as_active()
    {
        var (db, id) = await SeededActiveVersion();
        var mgr = new VersionManager(db);
        await mgr.DeactivateVersionAsync(id);
        Assert.Null(await mgr.GetActiveVersionAsync(new DateTime(2023, 1, 1)));
    }
}
