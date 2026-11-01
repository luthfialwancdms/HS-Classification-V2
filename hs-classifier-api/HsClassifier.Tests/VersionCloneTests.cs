using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Tests;

/// <summary>A draft copy of a version to load EN and notes into, so the active version is never touched.</summary>
public class VersionCloneTests
{
    private static async Task<(HsClassifierDbContext Db, Guid SourceId)> Source()
    {
        var db = new HsClassifierDbContext(AiTest.NewOptions());
        var v = new NomenclatureVersion
        {
            Name = "BTKI 2022", BtkiEdition = "PMK 26/2022", RegulationNumber = "26/PMK.010/2022",
            EffectiveFrom = new DateTime(2022, 4, 1), State = ImportState.Active, IsActive = true,
        };
        db.NomenclatureVersions.Add(v);
        var a = new BtkiTariffLine { VersionId = v.Id, Code = "8471.30.20", HeadingCode = "8471", DescriptionId = "Laptop", IsActive = true, SourceId = "s1" };
        var b = new BtkiTariffLine { VersionId = v.Id, Code = "8504.50.20", HeadingCode = "8504", DescriptionId = "Pertama", IsActive = true };
        var dup = new BtkiTariffLine { VersionId = v.Id, Code = "8504.50.20", HeadingCode = "8504", DescriptionId = "Duplikat sumber CSV", IsActive = true };
        var off = new BtkiTariffLine { VersionId = v.Id, Code = "0101.21.00", HeadingCode = "0101", DescriptionId = "Tidak aktif", IsActive = false };
        db.BtkiTariffLines.AddRange(a, b, dup, off);
        await db.SaveChangesAsync();
        db.MfnRates.AddRange(new MfnRate { TariffLineId = a.Id, VersionId = v.Id, RateValue = 5, SourceId = "m1" },
            new MfnRate { TariffLineId = b.Id, VersionId = v.Id, RateValue = 10 }, new MfnRate { TariffLineId = dup.Id, VersionId = v.Id, RateValue = 20 });
        db.FtaRates.Add(new FtaRate { TariffLineId = a.Id, VersionId = v.Id, SchemeCode = "ATIGA", PartnerCountryCode = "ASEAN", RateValue = 0 });
        db.TaxRates.Add(new TaxRate { TariffLineId = a.Id, VersionId = v.Id, TaxType = TaxType.Ppn, RateValue = 11 });
        var n1 = new ExplanatoryNote { VersionId = v.Id, HeadingCode = "8471", TextContent = "one", ParagraphType = ParagraphType.Scope };
        var n2 = new ExplanatoryNote { VersionId = v.Id, HeadingCode = "8471", TextContent = "two", ParagraphType = ParagraphType.Exclusion };
        db.ExplanatoryNotes.AddRange(n1, n2);
        await db.SaveChangesAsync();
        n1.NextParagraphId = n2.Id; n2.PreviousParagraphId = n1.Id;
        db.ChapterNotes.Add(new ChapterNote { VersionId = v.Id, ChapterNumber = "84", NoteNumber = "1", TextId = "Bab ini tidak meliputi", EffectType = EffectType.Exclusion });
        db.SectionNotes.Add(new SectionNote { VersionId = v.Id, SectionNumber = "XVI", NoteNumber = "1", TextId = "Bagian ini", EffectType = EffectType.Definition });
        db.SubheadingNotes.Add(new SubheadingNote { VersionId = v.Id, HeadingCode = "8471", NoteNumber = "1", TextId = "Subpos", EffectType = EffectType.Definition });
        await db.SaveChangesAsync();
        return (db, v.Id);
    }

    [Fact]
    public async Task Clone_is_a_non_active_Indexed_draft_with_the_same_headline_data()
    {
        var (db, sourceId) = await Source();
        var (clone, error) = await new VersionCloneService(db).CloneAsync(sourceId, null, "admin");

        Assert.Null(error);
        Assert.NotEqual(sourceId, clone!.Id);
        Assert.False(clone.IsActive);
        Assert.Equal(ImportState.Indexed, clone.State);
        Assert.Equal("PMK 26/2022", clone.BtkiEdition);
        Assert.Equal(new DateTime(2022, 4, 1), clone.EffectiveFrom);
        Assert.Contains("draft", clone.Name);
        Assert.Equal("admin", clone.CreatedBy);
    }

    [Fact]
    public async Task Clone_copies_all_lines_including_inactive_and_duplicate_codes_and_keeps_the_source_untouched()
    {
        var (db, sourceId) = await Source();
        var (clone, _) = await new VersionCloneService(db).CloneAsync(sourceId, "Draft EN", "admin");

        Assert.Equal("Draft EN", clone!.Name);
        Assert.Equal(4, await db.BtkiTariffLines.CountAsync(t => t.VersionId == clone.Id));
        Assert.Equal(1, await db.BtkiTariffLines.CountAsync(t => t.VersionId == clone.Id && !t.IsActive));
        Assert.Equal(2, await db.BtkiTariffLines.CountAsync(t => t.VersionId == clone.Id && t.Code == "8504.50.20"));
        Assert.Equal(4, await db.BtkiTariffLines.CountAsync(t => t.VersionId == sourceId));
        Assert.True((await db.NomenclatureVersions.FindAsync(sourceId))!.IsActive);
    }

    [Fact]
    public async Task Rates_follow_their_own_copy_of_the_line_even_when_the_code_is_duplicated()
    {
        var (db, sourceId) = await Source();
        var (clone, _) = await new VersionCloneService(db).CloneAsync(sourceId, null, "admin");

        var mfn = await db.MfnRates.Where(r => r.VersionId == clone!.Id).Include(r => r.TariffLine).ToListAsync();
        Assert.Equal(3, mfn.Count);
        Assert.All(mfn, r => Assert.Equal(clone!.Id, r.TariffLine!.VersionId));
        Assert.Equal("Pertama", mfn.Single(r => r.RateValue == 10).TariffLine!.DescriptionId);
        Assert.Equal("Duplikat sumber CSV", mfn.Single(r => r.RateValue == 20).TariffLine!.DescriptionId);
        Assert.Equal(1, await db.FtaRates.CountAsync(r => r.VersionId == clone!.Id));
        Assert.Equal(1, await db.TaxRates.CountAsync(r => r.VersionId == clone!.Id));
        Assert.Equal("m1", mfn.Single(r => r.RateValue == 5).SourceId);
    }

    [Fact]
    public async Task Notes_and_EN_are_copied_and_paragraph_links_point_inside_the_copy()
    {
        var (db, sourceId) = await Source();
        var (clone, _) = await new VersionCloneService(db).CloneAsync(sourceId, null, "admin");

        var en = await db.ExplanatoryNotes.Where(n => n.VersionId == clone!.Id).OrderBy(n => n.Id).ToListAsync();
        Assert.Equal(2, en.Count);
        Assert.Equal(en[1].Id, en[0].NextParagraphId);
        Assert.Equal(en[0].Id, en[1].PreviousParagraphId);
        Assert.Equal(1, await db.ChapterNotes.CountAsync(n => n.VersionId == clone!.Id));
        Assert.Equal(1, await db.SectionNotes.CountAsync(n => n.VersionId == clone!.Id));
        Assert.Equal(1, await db.SubheadingNotes.CountAsync(n => n.VersionId == clone!.Id));
        // the source's own links still point inside the source
        var src = await db.ExplanatoryNotes.Where(n => n.VersionId == sourceId).OrderBy(n => n.Id).ToListAsync();
        Assert.Equal(src[1].Id, src[0].NextParagraphId);
    }

    [Fact]
    public async Task Missing_or_empty_source_is_refused()
    {
        var (db, _) = await Source();
        var svc = new VersionCloneService(db);
        Assert.NotNull((await svc.CloneAsync(Guid.NewGuid(), null, "admin")).Error);

        var empty = new NomenclatureVersion { Name = "Kosong", State = ImportState.Uploaded };
        db.NomenclatureVersions.Add(empty);
        await db.SaveChangesAsync();
        Assert.NotNull((await svc.CloneAsync(empty.Id, null, "admin")).Error);
    }

    [Fact]
    public async Task Without_an_id_the_active_version_is_cloned()
    {
        var (db, sourceId) = await Source();
        var (clone, error) = await new VersionCloneService(db).CloneAsync(null, null, "admin");

        Assert.Null(error);
        Assert.Equal(4, await db.BtkiTariffLines.CountAsync(t => t.VersionId == clone!.Id));
        Assert.True((await db.NomenclatureVersions.FindAsync(sourceId))!.IsActive);
    }
}
