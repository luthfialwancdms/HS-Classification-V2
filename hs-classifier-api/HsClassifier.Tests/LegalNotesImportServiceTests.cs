using HsClassifier.Application.Interfaces;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Tests;

/// <summary>Catatan go into a non-active version after a dry run of the same file, like EN.</summary>
public class LegalNotesImportServiceTests
{
    private sealed record Setup(HsClassifierDbContext Db, LegalNotesImportService Svc, Guid VersionId);

    private static async Task<Setup> NewSetup(bool active = false)
    {
        var db = new HsClassifierDbContext(AiTest.NewOptions());
        var v = new NomenclatureVersion { Name = "Draft", EffectiveFrom = new DateTime(2022, 4, 1), IsActive = active,
            State = active ? ImportState.Active : ImportState.Indexed };
        db.NomenclatureVersions.Add(v);
        foreach (var code in new[] { "8471.30.20", "8517.13.00" })
            db.BtkiTariffLines.Add(new BtkiTariffLine { VersionId = v.Id, Code = code, HeadingCode = code[..4], ChapterCode = code[..2], DescriptionId = "x", IsActive = true });
        await db.SaveChangesAsync();
        return new Setup(db, new LegalNotesImportService(db), v.Id);
    }

    private static ParsedNote N(string scope, string type, string number, string effect = "EXCLUSION", string? id = null, string? en = "In English") => new()
    {
        ScopeCode = scope, NoteType = type, NoteNumber = number, EffectType = effect,
        TextId = id ?? $"{number}.- Catatan {type} {scope}", TextEn = en, ReferredCodesJson = "[\"8508\"]", SourceId = $"docx:abc:notes:{type.ToLower()}:{scope}:{number}",
    };

    private static LegalNotesParseResult Sample(string checksum = "abc") => new()
    {
        SourceFileName = "pmk.docx", SourceChecksum = checksum,
        SectionNotes = [N("XVI", "SECTION", "1"), N("XVI", "SECTION", "2", "REFERRAL")],
        ChapterNotes = [N("84", "CHAPTER", "1"), N("85", "CHAPTER", "1", "DEFINITION")],
        SubheadingNotes = [N("8517", "SUBHEADING", "1", "DEFINITION")],
    };

    [Fact]
    public async Task Dry_run_records_a_Notes_batch_with_counts_and_scopes_that_are_not_in_the_version()
    {
        var s = await NewSetup();
        var parse = Sample();
        parse.SubheadingNotes.Add(N("9999", "SUBHEADING", "1"));          // heading not in the version
        parse.SubheadingNotes.Add(N("74", "SUBHEADING", "1"));            // not a 4-digit heading at all
        var (batch, error) = await s.Svc.RecordUploadAsync(s.VersionId, parse, "admin");

        Assert.Null(error);
        Assert.Equal(ImportKind.Notes, batch!.Kind);
        Assert.Equal(7, batch.TotalNodes);
        Assert.Equal(0, batch.ErrorCount);
        Assert.Contains("9999", batch.ReportJson);
        Assert.Contains("74", batch.ReportJson);
    }

    [Fact]
    public async Task Dry_run_without_notes_is_an_error_and_the_active_version_is_refused()
    {
        var s = await NewSetup();
        var empty = new LegalNotesParseResult { SourceChecksum = "abc" };
        Assert.True((await s.Svc.RecordUploadAsync(s.VersionId, empty, "admin")).Batch!.ErrorCount > 0);

        var active = await NewSetup(active: true);
        Assert.NotNull((await active.Svc.RecordUploadAsync(active.VersionId, Sample(), "admin")).Error);
    }

    [Fact]
    public async Task Commit_needs_the_same_file_to_have_been_checked_first()
    {
        var s = await NewSetup();
        Assert.False((await s.Svc.CommitAsync(s.VersionId, Sample())).Success);

        await s.Svc.RecordUploadAsync(s.VersionId, Sample("abc"), "admin");
        var r = await s.Svc.CommitAsync(s.VersionId, Sample("other"));
        Assert.False(r.Success);
        Assert.Contains("checksum", r.Message);
    }

    [Fact]
    public async Task Commit_stores_the_three_kinds_with_enum_effects_and_both_languages()
    {
        var s = await NewSetup();
        await s.Svc.RecordUploadAsync(s.VersionId, Sample(), "admin");
        var r = await s.Svc.CommitAsync(s.VersionId, Sample());

        Assert.True(r.Success, r.Message);
        Assert.Equal(5, r.Inserted);
        var ch = await s.Db.ChapterNotes.SingleAsync(n => n.ChapterNumber == "84");
        Assert.Equal(EffectType.Exclusion, ch.EffectType);
        Assert.Equal("In English", ch.TextEn);
        Assert.Contains("Catatan CHAPTER 84", ch.TextId);
        Assert.Equal(s.VersionId, ch.VersionId);
        Assert.Equal(EffectType.Referral, (await s.Db.SectionNotes.SingleAsync(n => n.NoteNumber == "2")).EffectType);
        Assert.Equal("8517", (await s.Db.SubheadingNotes.SingleAsync()).HeadingCode);
    }

    [Fact]
    public async Task Subheading_notes_without_a_four_digit_heading_in_the_version_are_skipped_and_counted()
    {
        var s = await NewSetup();
        var parse = Sample();
        parse.SubheadingNotes.Add(N("9999", "SUBHEADING", "1"));
        parse.SubheadingNotes.Add(N("74", "SUBHEADING", "1"));
        await s.Svc.RecordUploadAsync(s.VersionId, parse, "admin");
        var r = await s.Svc.CommitAsync(s.VersionId, parse);

        Assert.True(r.Success);
        Assert.Equal(5, r.Inserted);
        Assert.Contains("2 catatan subpos dilewati", r.Message);
        Assert.Equal(1, await s.Db.SubheadingNotes.CountAsync());
    }

    [Fact]
    public async Task Committing_twice_needs_replace_and_replace_does_not_add_to_the_old_rows()
    {
        var s = await NewSetup();
        await s.Svc.RecordUploadAsync(s.VersionId, Sample(), "admin");
        Assert.True((await s.Svc.CommitAsync(s.VersionId, Sample())).Success);

        Assert.False((await s.Svc.CommitAsync(s.VersionId, Sample())).Success);
        Assert.True((await s.Svc.CommitAsync(s.VersionId, Sample(), replace: true)).Success);
        Assert.Equal(2, await s.Db.SectionNotes.CountAsync());
        Assert.Equal(2, await s.Db.ChapterNotes.CountAsync());
        Assert.Equal(1, await s.Db.SubheadingNotes.CountAsync());
    }

    [Fact]
    public async Task A_Notes_batch_never_hides_the_latest_Btki_batch()
    {
        var s = await NewSetup();
        var btki = new ImportBatch { VersionId = s.VersionId, FileName = "btki.pdf", Checksum = "b", CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
        btki.Items.Add(new AmbiguityItem { Code = "8471.30.20", Reason = "?", Decision = AmbiguityDecision.Pending });
        s.Db.ImportBatches.Add(btki);
        await s.Db.SaveChangesAsync();

        await s.Svc.RecordUploadAsync(s.VersionId, Sample(), "admin");

        Assert.False(await new VersionManager(s.Db).ActivateVersionAsync(s.VersionId));
    }

    [Fact]
    public async Task Effect_names_map_to_the_enum_and_unknown_ones_become_definition()
    {
        Assert.Equal(EffectType.Exclusion, LegalNotesImportService.ToEffect("EXCLUSION"));
        Assert.Equal(EffectType.Referral, LegalNotesImportService.ToEffect("referral"));
        Assert.Equal(EffectType.Definition, LegalNotesImportService.ToEffect("whatever"));
        await Task.CompletedTask;
    }
}
