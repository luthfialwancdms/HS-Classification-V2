using HsClassifier.Application.Interfaces;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Tests;

/// <summary>A PDF import is completed from the active version: scanned chapters, omitted group texts and MFN rates.</summary>
public class PdfImportCarryTests
{
    private static BtkiTariffLine Line(Guid version, string code, string text, string? parent = null, bool active = true) => new()
    {
        VersionId = version, Code = code, ParentCode = parent, ChapterCode = code[..2], HeadingCode = code[..4],
        DescriptionId = text, SourceId = "csv:old:" + code, IsActive = active, IngestionMethod = IngestionMethod.CsvTemplate,
    };

    private static ParsedTariffNode Node(string code, string text, string? parent) => new()
    {
        Code = code, ParentCode = parent, ChapterCode = code[..2], HeadingCode = code[..4], DescriptionId = text,
        Status = "A", SourceId = "pdf:chapter-74.pdf:" + code,
    };

    private static async Task<(HsClassifierDbContext Db, BtkiImportService Svc, Guid Active, Guid Fresh)> Setup()
    {
        var db = new HsClassifierDbContext(new DbContextOptionsBuilder<HsClassifierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var vm = new VersionManager(db);

        var active = new NomenclatureVersion { Name = "BTKI aktif", IsActive = true, State = ImportState.Active, EffectiveFrom = new DateTime(2022, 4, 1) };
        db.NomenclatureVersions.Add(active);
        db.BtkiTariffLines.AddRange(
            Line(active.Id, "0101", "Kuda hidup."), Line(active.Id, "0101.21.00", "- - Bibit murni", "0101"),
            Line(active.Id, "7409", "Pelat tembaga."), Line(active.Id, "7409.1", "- Dari tembaga dimurnikan :", "7409"),
            Line(active.Id, "7409.11.00", "- - Dalam gulungan", "7409"),
            Line(active.Id, "7499.00.00", "Kode yang tidak ada di PDF", "7499"),
            Line(active.Id, "0101.29.00", "- - Dihapus", "0101", active: false));
        await db.SaveChangesAsync();
        var ids = await db.BtkiTariffLines.ToDictionaryAsync(t => t.Code + (t.IsActive ? "" : "#off"), t => t.Id);
        db.MfnRates.AddRange(
            new MfnRate { TariffLineId = ids["0101.21.00"], VersionId = active.Id, RateValue = 5m, SourceId = "csv:mfn:1" },
            new MfnRate { TariffLineId = ids["7409.11.00"], VersionId = active.Id, RateValue = 10m, SourceId = "csv:mfn:2" });
        await db.SaveChangesAsync();

        var fresh = await vm.CreateVersionAsync("BTKI PDF", "PMK 26/2022", "PMK 26/2022", new DateTime(2022, 4, 1), null, "admin");
        return (db, new BtkiImportService(db, vm), active.Id, fresh);
    }

    private static BtkiParseResult PdfResult() => new()
    {
        SourceFileName = "chapter-74.pdf", SourceChecksum = "pdf-1", IngestionMethod = IngestionMethod.PdfParser,
        TariffNodes =
        [
            Node("7409", "Pelat, lembaran dan strip tembaga.", null),
            Node("7409.11.00", "- - Dalam gulungan", "7409"),
            Node("7410.10.00", "Baru di PDF", "7410"),   // no old counterpart: no MFN
            Node("7410", "Foil tembaga.", null),
        ],
        MissingGroupCodes = ["7409.1"],
    };

    [Fact]
    public async Task Pdf_commit_carries_scanned_chapters_group_texts_and_mfn_rates()
    {
        var (db, svc, active, fresh) = await Setup();
        Assert.Null((await svc.RecordUploadAsync(fresh, PdfResult(), "admin")).Error);

        var (ok, message, inserted) = await svc.CommitAsync(fresh, PdfResult());

        Assert.True(ok, message);
        Assert.Equal(4, inserted);
        var rows = await db.BtkiTariffLines.Where(t => t.VersionId == fresh).ToListAsync();
        string[] expected = ["0101", "0101.21.00", "7409", "7409.1", "7409.11.00", "7410", "7410.10.00"];
        Assert.Equal(expected.Order(), rows.Select(r => r.Code).Order());

        // the PDF's own text wins for chapters it covers; carried rows are marked and keep the old text
        Assert.Equal("Pelat, lembaran dan strip tembaga.", rows.Single(r => r.Code == "7409").DescriptionId);
        Assert.Equal(IngestionMethod.PdfParser, rows.Single(r => r.Code == "7409").IngestionMethod);
        var group = rows.Single(r => r.Code == "7409.1");
        Assert.Equal("- Dari tembaga dimurnikan :", group.DescriptionId);
        Assert.StartsWith("carry:BTKI aktif:", group.SourceId);
        Assert.StartsWith("carry:", rows.Single(r => r.Code == "0101.21.00").SourceId);

        // chapter 1 has "0101" in the old data already; a heading printed only as a single line gets its row too
        Assert.Equal(1, rows.Count(r => r.Code == "0101"));

        // codes only the active version has (or inactive ones) are not brought along
        Assert.DoesNotContain(rows, r => r.Code == "7499.00.00");
        Assert.DoesNotContain(rows, r => r.Code == "0101.29.00");

        // MFN follows the code; a line new in the PDF gets none and is counted
        var rates = await db.MfnRates.Where(r => r.VersionId == fresh).ToListAsync();
        var byLine = rows.ToDictionary(r => r.Id, r => r.Code);
        Assert.Equal(["0101.21.00", "7409.11.00"], rates.Select(r => byLine[r.TariffLineId]).Order());
        Assert.Equal(10m, rates.Single(r => byLine[r.TariffLineId] == "7409.11.00").RateValue);
        Assert.Contains("1 pos tarif belum punya tarif MFN", message);

        // the active version is untouched
        Assert.Equal(7, await db.BtkiTariffLines.CountAsync(t => t.VersionId == active));
        Assert.Equal(2, await db.MfnRates.CountAsync(r => r.VersionId == active));
        Assert.True((await db.NomenclatureVersions.FindAsync(active))!.IsActive);
    }

    [Fact]
    public async Task Only_one_dash_group_texts_are_carried_not_deeper_texts_filed_under_a_group_code()
    {
        var (db, svc, active, fresh) = await Setup();
        db.BtkiTariffLines.Add(Line(active, "7410.1", "- - - Teks tiga strip yang salah tempat :", "7410"));
        await db.SaveChangesAsync();
        var parse = PdfResult();
        parse.MissingGroupCodes = ["7409.1", "7410.1"];
        await svc.RecordUploadAsync(fresh, parse, "admin");

        Assert.True((await svc.CommitAsync(fresh, parse)).Success);

        var codes = await db.BtkiTariffLines.Where(t => t.VersionId == fresh).Select(t => t.Code).ToListAsync();
        Assert.Contains("7409.1", codes);
        Assert.DoesNotContain("7410.1", codes);
    }

    [Fact]
    public async Task Carried_chapter_gets_a_heading_row_for_a_heading_printed_only_as_one_tariff_line()
    {
        var (db, svc, active, fresh) = await Setup();
        db.BtkiTariffLines.Add(Line(active, "0409.00.00", "Madu alam.", "0409"));       // no 0409 row in the old data
        db.BtkiTariffLines.Add(Line(active, "0410.00.10", "- Sarang burung", "0410"));  // dashed: no heading text to build from
        await db.SaveChangesAsync();
        await svc.RecordUploadAsync(fresh, PdfResult(), "admin");

        var (ok, message, _) = await svc.CommitAsync(fresh, PdfResult());

        Assert.True(ok, message);
        var rows = await db.BtkiTariffLines.Where(t => t.VersionId == fresh).ToListAsync();
        var heading = rows.Single(r => r.Code == "0409");
        Assert.Equal("Madu alam.", heading.DescriptionId);
        Assert.Null(heading.ParentCode);
        Assert.StartsWith("carry:", heading.SourceId);
        Assert.DoesNotContain(rows, r => r.Code == "0410");
        Assert.Contains("1 pos dibuat dari baris tarif tunggal", message);
    }

    [Fact]
    public async Task Docx_commit_is_not_completed_from_the_active_version()
    {
        var (db, svc, _, fresh) = await Setup();
        var docx = PdfResult();
        docx.IngestionMethod = IngestionMethod.DocxParser;
        await svc.RecordUploadAsync(fresh, docx, "admin");

        Assert.True((await svc.CommitAsync(fresh, docx)).Success);
        Assert.Equal(4, await db.BtkiTariffLines.CountAsync(t => t.VersionId == fresh));
        Assert.Equal(0, await db.MfnRates.CountAsync(r => r.VersionId == fresh));
    }

    [Fact]
    public async Task A_version_that_already_has_rows_is_not_committed_twice()
    {
        var (db, svc, _, fresh) = await Setup();
        await svc.RecordUploadAsync(fresh, PdfResult(), "admin");
        db.BtkiTariffLines.Add(Line(fresh, "7409", "sisa commit yang gagal"));
        await db.SaveChangesAsync();

        var (ok, message, _) = await svc.CommitAsync(fresh, PdfResult());

        Assert.False(ok);
        Assert.Contains("sudah berisi", message);
    }
}
