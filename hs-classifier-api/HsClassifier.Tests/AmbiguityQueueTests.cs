using HsClassifier.Application.Interfaces;
using HsClassifier.Application.Services;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Tests;

/// <summary>Validation report + STRUCTURE_AMBIGUITY queue gate the DOCX commit (Req 1.4, 1.9, 1.11).</summary>
public class AmbiguityQueueTests
{
    private static ParsedTariffNode Node(string code, string? parent) => new()
    {
        Code = code, ParentCode = parent, HeadingCode = code[..4], ChapterCode = code[..2],
        DescriptionId = "Uraian " + code, Status = "A", SourceId = "docx:test:" + code,
    };

    /// <summary>Two clean lines (parent is their own prefix) + one whose parent conflicts with its code.</summary>
    private static BtkiParseResult Parse(string checksum = "abc123") => new()
    {
        SourceFileName = "btki.docx", SourceChecksum = checksum, TotalRows = 3,
        TariffNodes = [Node("8471.30.20", "8471.30"), Node("8471.30.90", "8471.30"), Node("8471.41.10", "8528.52")],
    };

    [Fact]
    public void Prefix_parents_are_not_ambiguous_conflicting_parent_is()
    {
        var nodes = Parse().TariffNodes;
        BtkiStructureService.BuildHierarchy(nodes);
        Assert.Equal(["8471.41.10"], nodes.Where(n => n.IsAmbiguous).Select(n => n.Code));
    }

    [Fact]
    public void Gate_blocks_pending_and_applies_decisions()
    {
        var (_, pending) = AmbiguityGate.ResolveForCommit(Parse().TariffNodes, []);
        Assert.Contains("8471.41.10", pending);

        var (approved, ok) = AmbiguityGate.ResolveForCommit(Parse().TariffNodes,
            [new AmbiguityResolution("8471.41.10", AmbiguityDecision.Approved, "8471.41")]);
        Assert.Null(ok);
        Assert.Equal("8471.41", approved.Single(n => n.Code == "8471.41.10").ParentCode);

        var (rejected, _) = AmbiguityGate.ResolveForCommit(Parse().TariffNodes,
            [new AmbiguityResolution("8471.41.10", AmbiguityDecision.Rejected, null)]);
        Assert.Equal(2, rejected.Count);
    }

    private static async Task<(HsClassifierDbContext Db, BtkiImportService Svc, VersionManager Vm, Guid Id)> NewVersion()
    {
        var db = new HsClassifierDbContext(new DbContextOptionsBuilder<HsClassifierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var vm = new VersionManager(db);
        var id = await vm.CreateVersionAsync("BTKI test", "PMK x", "PMK x", new DateTime(2026, 1, 1), null, "admin");
        return (db, new BtkiImportService(db, vm), vm, id);
    }

    [Fact]
    public async Task Upload_persists_report_and_queue_then_decisions_unlock_commit()
    {
        var (db, svc, vm, id) = await NewVersion();

        var (batch, err) = await svc.RecordUploadAsync(id, Parse(), "admin@test");
        Assert.Null(err);
        var item = Assert.Single(batch!.Items);
        Assert.Equal(ImportState.DryRunPassed, (await vm.GetVersionAsync(id))!.State); // waiting on the queue

        // Commit is refused while the item is pending
        Assert.False((await svc.CommitAsync(id, Parse())).Success);

        Assert.NotNull(await svc.DecideAsync(item.Id, AmbiguityDecision.Approved, null, "x", "admin@test")); // note too short
        Assert.Null(await svc.DecideAsync(item.Id, AmbiguityDecision.Rejected, null, "Parent salah bab", "admin@test"));
        Assert.Equal(ImportState.ReadyToCommit, (await vm.GetVersionAsync(id))!.State);
        Assert.NotNull(await svc.DecideAsync(item.Id, AmbiguityDecision.Approved, null, "ubah pikiran", "admin@test")); // locked

        // A different file than the one reviewed is refused
        Assert.False((await svc.CommitAsync(id, Parse(checksum: "other"))).Success);

        var (ok, _, inserted) = await svc.CommitAsync(id, Parse());
        Assert.True(ok);
        Assert.Equal(2, inserted); // rejected node excluded
        Assert.Equal(ImportState.Indexed, (await vm.GetVersionAsync(id))!.State);
        Assert.True(await vm.ActivateVersionAsync(id));
    }

    [Fact]
    public async Task Clean_upload_goes_straight_to_ReadyToCommit_and_upload_after_that_is_refused()
    {
        var (_, svc, vm, id) = await NewVersion();
        var clean = Parse();
        clean.TariffNodes.RemoveAt(2);
        Assert.Empty((await svc.RecordUploadAsync(id, clean, "admin")).Batch!.Items);
        Assert.Equal(ImportState.ReadyToCommit, (await vm.GetVersionAsync(id))!.State);
        Assert.NotNull((await svc.RecordUploadAsync(id, clean, "admin")).Error);
    }

    [Fact]
    public async Task Activation_is_refused_while_latest_batch_has_pending_items()
    {
        var (db, svc, vm, id) = await NewVersion();
        await svc.RecordUploadAsync(id, Parse(), "admin");
        // force the version to Indexed to isolate the activation gate itself
        (await db.NomenclatureVersions.FindAsync(id))!.State = ImportState.Indexed;
        await db.SaveChangesAsync();
        Assert.False(await vm.ActivateVersionAsync(id));
    }
}
