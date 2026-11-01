using HsClassifier.Application.Interfaces;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Tests;

/// <summary>EN goes into a version that is not active yet, only after a dry run of the same file, and never twice by accident.</summary>
public class EnImportServiceTests
{
    private sealed record Setup(HsClassifierDbContext Db, EnImportService Svc, Guid VersionId);

    private static async Task<Setup> NewSetup(bool active = false, bool withLines = true)
    {
        var db = new HsClassifierDbContext(AiTest.NewOptions());
        var v = new NomenclatureVersion { Name = "Draft", EffectiveFrom = new DateTime(2022, 4, 1), IsActive = active,
            State = active ? ImportState.Active : ImportState.Indexed };
        db.NomenclatureVersions.Add(v);
        if (withLines)
            foreach (var code in new[] { "8471.30.20", "8413.70.00", "8704.10.00", "0101.21.00" })
                db.BtkiTariffLines.Add(new BtkiTariffLine { VersionId = v.Id, Code = code, HeadingCode = code[..4], DescriptionId = "x", IsActive = true });
        await db.SaveChangesAsync();
        return new Setup(db, new EnImportService(db), v.Id);
    }

    private static ParsedEnParagraph P(string heading, int seq, string type = "SCOPE", string? text = null) => new()
    {
        HeadingCode = heading, SequenceInHeading = seq, ParagraphType = type,
        TextContent = text ?? $"Paragraph {seq} of heading {heading} with enough words to count.",
        SourceId = $"docx:abc:en:{heading}:para_{seq}", ReferredCodesJson = "[\"9508\"]",
    };

    private static EnParseResult Parse(string checksum = "abc", params ParsedEnParagraph[] ps)
    {
        var r = new EnParseResult { SourceFileName = "en.docx", SourceChecksum = checksum, Paragraphs = [.. ps], TotalParagraphs = ps.Length };
        r.Headings = ps.Select(p => p.HeadingCode).Distinct().ToList();
        return r;
    }

    private static EnParseResult Sample(string checksum = "abc") => Parse(checksum,
        P("8471", 1), P("8471", 2, "EXCLUSION"), P("8471", 3, "CROSS_REFERENCE"), P("8413", 1, "INCLUSION"), P("0101", 1));

    [Fact]
    public async Task Dry_run_records_an_En_batch_with_counts_and_the_gaps_in_both_directions()
    {
        var s = await NewSetup();
        var parse = Parse("abc", P("8471", 1), P("8413", 1), P("9999", 1));      // 9999 is not in the version; 8704 and 0101 got no EN
        var (batch, error) = await s.Svc.RecordUploadAsync(s.VersionId, parse, "admin");

        Assert.Null(error);
        Assert.Equal(ImportKind.En, batch!.Kind);
        Assert.Equal(3, batch.TotalNodes);
        Assert.Equal(0, batch.ErrorCount);
        Assert.Contains("9999", batch.ReportJson);                // heading not in the version
        Assert.Contains("8704", batch.ReportJson);                // version heading without EN
    }

    [Fact]
    public async Task Dry_run_with_no_paragraphs_is_an_error()
    {
        var s = await NewSetup();
        var (batch, _) = await s.Svc.RecordUploadAsync(s.VersionId, Parse("abc"), "admin");
        Assert.True(batch!.ErrorCount > 0);
    }

    [Fact]
    public async Task Upload_is_refused_for_the_active_version_and_for_a_version_without_tariff_lines()
    {
        var active = await NewSetup(active: true);
        Assert.NotNull((await active.Svc.RecordUploadAsync(active.VersionId, Sample(), "admin")).Error);

        var empty = await NewSetup(withLines: false);
        Assert.NotNull((await empty.Svc.RecordUploadAsync(empty.VersionId, Sample(), "admin")).Error);

        Assert.NotNull((await active.Svc.RecordUploadAsync(Guid.NewGuid(), Sample(), "admin")).Error);
    }

    [Fact]
    public async Task Commit_needs_a_dry_run_of_the_same_file_first()
    {
        var s = await NewSetup();
        var none = await s.Svc.CommitAsync(s.VersionId, Sample());
        Assert.False(none.Success);

        await s.Svc.RecordUploadAsync(s.VersionId, Sample("abc"), "admin");
        var other = await s.Svc.CommitAsync(s.VersionId, Sample("different"));
        Assert.False(other.Success);
        Assert.Contains("checksum", other.Message);
        Assert.Equal(0, await s.Db.ExplanatoryNotes.CountAsync());
    }

    [Fact]
    public async Task Commit_stores_paragraphs_with_enum_types_source_ids_and_links_between_neighbours()
    {
        var s = await NewSetup();
        await s.Svc.RecordUploadAsync(s.VersionId, Sample(), "admin");
        var r = await s.Svc.CommitAsync(s.VersionId, Sample());

        Assert.True(r.Success, r.Message);
        Assert.Equal(5, r.Inserted);
        var rows = await s.Db.ExplanatoryNotes.Where(n => n.HeadingCode == "8471").OrderBy(n => n.Id).ToListAsync();
        Assert.Equal([ParagraphType.Scope, ParagraphType.Exclusion, ParagraphType.CrossReference], rows.Select(n => n.ParagraphType));
        Assert.All(rows, n => Assert.Equal(s.VersionId, n.VersionId));
        Assert.Equal("docx:abc:en:8471:para_2", rows[1].SourceId);
        Assert.Null(rows[0].PreviousParagraphId);
        Assert.Equal(rows[1].Id, rows[0].NextParagraphId);
        Assert.Equal(rows[0].Id, rows[1].PreviousParagraphId);
        Assert.Equal(rows[2].Id, rows[1].NextParagraphId);
        Assert.Null(rows[2].NextParagraphId);
        // another heading's first paragraph is not linked to this heading's last
        var other = await s.Db.ExplanatoryNotes.SingleAsync(n => n.HeadingCode == "8413");
        Assert.Null(other.PreviousParagraphId);
    }

    [Fact]
    public async Task Paragraphs_of_headings_that_are_not_in_the_version_are_skipped_and_counted()
    {
        var s = await NewSetup();
        var parse = Parse("abc", P("8471", 1), P("9999", 1), P("9999", 2));
        await s.Svc.RecordUploadAsync(s.VersionId, parse, "admin");
        var r = await s.Svc.CommitAsync(s.VersionId, parse);

        Assert.True(r.Success);
        Assert.Equal(1, r.Inserted);
        Assert.Contains("2 paragraf", r.Message);
        Assert.DoesNotContain(await s.Db.ExplanatoryNotes.ToListAsync(), n => n.HeadingCode == "9999");
    }

    [Fact]
    public async Task Committing_twice_is_refused_unless_replace_is_asked_for()
    {
        var s = await NewSetup();
        await s.Svc.RecordUploadAsync(s.VersionId, Sample(), "admin");
        Assert.True((await s.Svc.CommitAsync(s.VersionId, Sample())).Success);

        var again = await s.Svc.CommitAsync(s.VersionId, Sample());
        Assert.False(again.Success);
        Assert.Equal(5, await s.Db.ExplanatoryNotes.CountAsync());

        var replaced = await s.Svc.CommitAsync(s.VersionId, Sample(), replace: true);
        Assert.True(replaced.Success, replaced.Message);
        Assert.Equal(5, await s.Db.ExplanatoryNotes.CountAsync());      // old rows replaced, not added to
    }

    [Fact]
    public async Task Commit_is_refused_when_the_version_has_become_active()
    {
        var s = await NewSetup();
        await s.Svc.RecordUploadAsync(s.VersionId, Sample(), "admin");
        var v = await s.Db.NomenclatureVersions.FindAsync(s.VersionId);
        v!.IsActive = true; v.State = ImportState.Active;
        await s.Db.SaveChangesAsync();

        Assert.False((await s.Svc.CommitAsync(s.VersionId, Sample())).Success);
    }

    [Fact]
    public async Task An_En_batch_never_hides_the_latest_Btki_batch_that_gates_commit_and_activation()
    {
        var s = await NewSetup();
        var btki = new ImportBatch { VersionId = s.VersionId, FileName = "btki.pdf", Checksum = "b", CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
        btki.Items.Add(new AmbiguityItem { Code = "8471.30.20", Reason = "?", Decision = AmbiguityDecision.Pending });
        s.Db.ImportBatches.Add(btki);
        await s.Db.SaveChangesAsync();

        await s.Svc.RecordUploadAsync(s.VersionId, Sample(), "admin");      // newer, Kind = En

        var latest = await new BtkiImportService(s.Db, new VersionManager(s.Db)).LatestBatchAsync(s.VersionId);
        Assert.Equal(btki.Id, latest!.Id);
        Assert.False(await new VersionManager(s.Db).ActivateVersionAsync(s.VersionId));   // pending ambiguity still blocks
    }
}
