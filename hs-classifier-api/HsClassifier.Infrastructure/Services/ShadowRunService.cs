using HsClassifier.Application.Classification;
using HsClassifier.Application.Interfaces;
using HsClassifier.Domain.Classification;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HsClassifier.Infrastructure.Services;

public record ShadowCase(string Name, Dictionary<string, string?> Facts);

/// <param name="Notes">Reasoning lines where a legal note or EN removed a heading, so a change can be traced to its note.</param>
public record ShadowSide(string Status, string? FinalCode, string? Confidence, int? Score, string? Message, int Questions, List<string> Notes);

public record ShadowRow(string Name, ShadowSide Active, ShadowSide Draft)
{
    /// <summary>The answer differs: another status or another code. A different score alone is not a change.</summary>
    public bool Changed => Active.Status != Draft.Status || Active.FinalCode != Draft.FinalCode;
}

public record ShadowReport(Guid ActiveId, string ActiveName, Guid DraftId, string DraftName, List<ShadowRow> Rows)
{
    public int Changed => Rows.Count(r => r.Changed);
}

/// <summary>Versions are chosen by id here, not by "active": classification runs against exactly the version it is given.</summary>
public class PinnedVersionManager(Guid versionId, HsClassifierDbContext db) : IVersionManager
{
    private readonly VersionManager _inner = new(db);

    public Task<NomenclatureVersion?> GetActiveVersionAsync(DateTime effectiveDate) => _inner.GetVersionAsync(versionId);
    public Task<NomenclatureVersion?> GetVersionAsync(Guid id) => _inner.GetVersionAsync(id);

    // A pinned manager only answers "which version"; it never changes versions.
    public Task<Guid> CreateVersionAsync(string name, string btkiEdition, string regulationNumber, DateTime effectiveFrom, DateTime? effectiveTo, string createdBy)
        => throw new NotSupportedException();
    public Task<IReadOnlyList<NomenclatureVersion>> GetAllVersionsAsync() => throw new NotSupportedException();
    public Task<bool> TransitionStateAsync(Guid id, ImportState newState) => throw new NotSupportedException();
    public Task<bool> ActivateVersionAsync(Guid id) => throw new NotSupportedException();
    public Task<bool> DeactivateVersionAsync(Guid id) => throw new NotSupportedException();
}

/// <summary>Cases that go to every shadow run unless the Admin sends their own.</summary>
public static class ShadowCases
{
    private static ShadowCase C(string name, params (string, string)[] facts) =>
        new(name, facts.ToDictionary(f => f.Item1, f => (string?)f.Item2));

    public static readonly IReadOnlyList<ShadowCase> Default =
    [
        C("Pompa industri", ("generic_name", "pompa industri")),
        C("Pompa", ("generic_name", "pompa")),
        C("Laptop dengan kamera, speaker, mikrofon", ("generic_name", "Laptop"),
            ("primary_function", "mengolah data, dengan kamera, speaker dan mikrofon terintegrasi")),
        C("Mesin pengolah data portabel", ("generic_name", "mesin pengolah data otomatis portabel")),
        C("Dumper bermerek Tractor", ("generic_name", "Dumper"), ("commercial_name", "Tractor"), ("primary_function", "pengangkutan barang")),
        C("Crane di atas tongkang", ("generic_name", "crane"), ("primary_function", "mengangkat muatan"),
            ("mounting", "terpasang tetap di atas tongkang (barge)")),
        C("Pulpen (bukan set)", ("commercial_name", "Pulpen Pilot"), ("generic_name", "Pulpen"),
            ("set_kit", "retail_set"), ("set_single_purpose", "no"), ("set_retail_ready", "yes"), ("set_essential_item", "pulpen")),
    ];
}

/// <summary>
/// Classifies the same cases on the active version and on a draft and reports where the answers differ. Nothing is saved
/// (no case, no reasoning event, no quota) and the AI helper is not used, so a difference comes from the data in the
/// two versions and not from the AI's variation. A case that needs the AI ends as an expert review on both sides.
/// </summary>
public class ShadowRunService(HsClassifierDbContext db, IConfiguration config)
{
    private sealed class NoAiSuggester : ITermSuggester
    {
        public string Model => "none";
        public Task<SuggestionResult> SuggestAsync(FactSet facts, CancellationToken ct = default) =>
            Task.FromResult(new SuggestionResult(null, false));
    }

    private ClassificationOrchestrator PipelineFor(Guid versionId)
    {
        var vm = new PinnedVersionManager(versionId, db);
        return new ClassificationOrchestrator(db, new CandidateGeneratorService(db, vm), new ClassificationValidator(db, vm),
            new TariffLookupService(db, vm), new NoAiSuggester(), config) { DryRun = true };
    }

    public async Task<(ShadowReport? Report, string? Error)> RunAsync(
        Guid draftId, IReadOnlyList<ShadowCase> cases, CancellationToken ct = default)
    {
        if (cases.Count == 0) return (null, "Daftar kasus kosong.");
        var draft = await db.NomenclatureVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == draftId, ct);
        if (draft is null) return (null, "Versi draft tidak ditemukan.");
        var active = await new VersionManager(db).GetActiveVersionAsync(DateTime.UtcNow);
        if (active is null) return (null, "Belum ada versi aktif sebagai pembanding.");
        if (active.Id == draftId) return (null, "Versi ini sedang aktif. Pilih draft untuk dibandingkan dengan versi aktif.");

        var onActive = PipelineFor(active.Id);
        var onDraft = PipelineFor(draftId);
        var rows = new List<ShadowRow>(cases.Count);
        foreach (var c in cases)
        {
            var a = await RunOneAsync(onActive, c, ct);
            var d = await RunOneAsync(onDraft, c, ct);
            rows.Add(new ShadowRow(c.Name, a, d));
        }
        return (new ShadowReport(active.Id, active.Name, draft.Id, draft.Name, rows), null);
    }

    private async Task<ShadowSide> RunOneAsync(ClassificationOrchestrator pipeline, ShadowCase c, CancellationToken ct)
    {
        try
        {
            var r = await pipeline.ClassifyAsync(0, new Dictionary<string, string?>(c.Facts), null, ct);
            return new ShadowSide(r.Status, r.FinalCode, r.Confidence, r.ConfidenceScore, r.Message, r.Questions?.Count ?? 0,
                (r.Trace ?? []).Where(l => l.Contains("Dikecualikan oleh")).ToList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ShadowSide("EXCEPTION", null, null, null, ex.Message, 0, []);
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
