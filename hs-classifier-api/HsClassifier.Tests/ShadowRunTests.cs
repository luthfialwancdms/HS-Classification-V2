using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HsClassifier.Tests;

/// <summary>A shadow run classifies the same cases on the active version and on a draft, and saves nothing.</summary>
public class ShadowRunTests
{
    private static ShadowCase Case(string name, params (string, string)[] facts) =>
        new(name, facts.ToDictionary(f => f.Item1, f => (string?)f.Item2));

    private static async Task<(MiniBtki Mini, ShadowRunService Svc, Guid DraftId)> Setup(bool excludePumpInDraft)
    {
        var mini = await MiniBtki.CreateAsync();
        var active = await mini.Db.NomenclatureVersions.SingleAsync();
        var (draft, error) = await new VersionCloneService(mini.Db).CloneAsync(active.Id, "Draft EN", "admin");
        Assert.Null(error);
        if (excludePumpInDraft)
        {
            mini.Db.ChapterNotes.Add(new ChapterNote { VersionId = draft!.Id, ChapterNumber = "84", NoteNumber = "1", EffectType = EffectType.Exclusion,
                TextId = "Bab ini tidak mencakup: (a) Pompa (pos 90.18)", SourceId = "pmk:ch84:1" });
            await mini.Db.SaveChangesAsync();
        }
        return (mini, new ShadowRunService(mini.Db, new ConfigurationBuilder().Build()), draft!.Id);
    }

    [Fact]
    public async Task Identical_versions_give_identical_answers()
    {
        var (_, svc, draft) = await Setup(excludePumpInDraft: false);

        var (report, error) = await svc.RunAsync(draft, [Case("Laptop", ("generic_name", "Laptop"),
            ("primary_function", "mengolah data, dengan kamera, speaker dan mikrofon terintegrasi"))]);

        Assert.Null(error);
        var row = Assert.Single(report!.Rows);
        Assert.False(row.Changed);
        Assert.Equal("8471.30.20", row.Active.FinalCode);
        Assert.Equal(row.Active.FinalCode, row.Draft.FinalCode);
        Assert.Equal(0, report.Changed);
    }

    [Fact]
    public async Task A_note_only_in_the_draft_changes_the_outcome_and_the_report_says_which_note()
    {
        var (_, svc, draft) = await Setup(excludePumpInDraft: true);

        var (report, error) = await svc.RunAsync(draft, [Case("Pompa", ("generic_name", "Pompa"))]);

        Assert.Null(error);
        var row = Assert.Single(report!.Rows);
        Assert.True(row.Changed);
        Assert.Equal(1, report.Changed);
        Assert.DoesNotContain(row.Active.Notes, n => n.Contains("Catatan Bab 84"));
        Assert.Contains(row.Draft.Notes, n => n.Contains("Catatan Bab 84 no. 1") && n.Contains("Pompa"));
    }

    [Fact]
    public async Task Nothing_is_saved_and_the_ai_helper_is_not_called()
    {
        var (mini, svc, draft) = await Setup(excludePumpInDraft: true);
        var casesBefore = await mini.Db.ClassificationCases.CountAsync();
        var eventsBefore = await mini.Db.ReasoningEvents.CountAsync();

        // "zzz qqq" finds no heading, which is exactly where the live pipeline would call the AI
        var (report, _) = await svc.RunAsync(draft, [Case("Pompa", ("generic_name", "Pompa")), Case("Tidak dikenal", ("generic_name", "zzz qqq"))]);

        Assert.Equal(2, report!.Rows.Count);
        Assert.Equal(casesBefore, await mini.Db.ClassificationCases.CountAsync());
        Assert.Equal(eventsBefore, await mini.Db.ReasoningEvents.CountAsync());
        Assert.Equal(0, await mini.Db.AiUsageLogs.CountAsync());
        Assert.Equal(0, await mini.Db.DailyUsages.CountAsync());
        Assert.Equal("NEED_EXPERT_REVIEW", report.Rows[1].Active.Status);
        Assert.Equal("NEED_EXPERT_REVIEW", report.Rows[1].Draft.Status);
    }

    [Fact]
    public async Task An_empty_case_does_not_stop_the_others()
    {
        var (_, svc, draft) = await Setup(excludePumpInDraft: false);

        var (report, _) = await svc.RunAsync(draft, [Case("Kosong"), Case("Laptop", ("generic_name", "Laptop"),
            ("primary_function", "mengolah data, dengan kamera, speaker dan mikrofon terintegrasi"))]);

        Assert.Equal(2, report!.Rows.Count);
        Assert.Equal("8471.30.20", report.Rows[1].Active.FinalCode);
    }

    [Fact]
    public async Task The_draft_must_exist_differ_from_the_active_version_and_an_active_version_must_exist()
    {
        var (mini, svc, draft) = await Setup(excludePumpInDraft: false);
        var active = await mini.Db.NomenclatureVersions.SingleAsync(v => v.IsActive);

        Assert.NotNull((await svc.RunAsync(Guid.NewGuid(), [Case("x", ("generic_name", "Pompa"))])).Error);
        Assert.NotNull((await svc.RunAsync(active.Id, [Case("x", ("generic_name", "Pompa"))])).Error);
        Assert.NotNull((await svc.RunAsync(draft, [])).Error);

        active.IsActive = false;
        active.State = ImportState.Indexed;
        await mini.Db.SaveChangesAsync();
        Assert.NotNull((await svc.RunAsync(draft, [Case("x", ("generic_name", "Pompa"))])).Error);
    }

    [Fact]
    public void The_built_in_case_list_has_unique_names_and_facts()
    {
        Assert.True(ShadowCases.Default.Count >= 6);
        Assert.Equal(ShadowCases.Default.Count, ShadowCases.Default.Select(c => c.Name).Distinct().Count());
        Assert.All(ShadowCases.Default, c => Assert.NotEmpty(c.Facts));
    }
}
