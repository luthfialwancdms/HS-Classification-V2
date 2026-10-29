using HsClassifier.Application.Interfaces;
using HsClassifier.Application.Services;
using HsClassifier.Infrastructure.Parsing;

namespace HsClassifier.Tests;

/// <summary>
/// The parser against the real official chapter PDFs in docs/btki (download.ps1 fetches them).
/// A test returns without asserting when its PDF is not on disk, so a fresh clone still builds green.
/// </summary>
public class BtkiPdfFileTests
{
    private static string? Pdf(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "docs", "btki", name);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static async Task<BtkiParseResult?> Parse(string name)
    {
        if (Pdf(name) is not { } path) return null;
        await using var fs = File.OpenRead(path);
        return await new BtkiPdfParser().ParseAsync(fs, name);
    }

    [Fact]
    public async Task Chapter_74_headings_subheadings_and_lines_with_both_languages()
    {
        if (await Parse("chapter-74.pdf") is not { } r) return;

        var by = r.TariffNodes.ToDictionary(n => n.Code);
        Assert.All(r.TariffNodes, n => Assert.Equal("74", n.ChapterCode));
        Assert.Equal("XV", by["7404"].SectionCode);

        // 74.04 is printed only as one tariff line; the heading is built from it
        Assert.Equal("Sisa dan skrap tembaga.", by["7404"].DescriptionId);
        Assert.Equal("Copper waste and scrap.", by["7404"].DescriptionEn);
        Assert.Equal("7404", by["7404.00.00"].ParentCode);

        Assert.Equal("7407", by["7407.10"].ParentCode);
        Assert.Equal("7407.10", by["7407.10.30"].ParentCode);
        Assert.Equal("- - Profil", by["7407.10.30"].DescriptionId);

        // a row that wraps over several lines keeps all its words
        Assert.Contains("ketebalan", by["7410"].DescriptionId);

        var dry = BtkiStructureService.DryRun(r.TariffNodes);
        Assert.Equal(0, dry.ErrorCount);
        Assert.Equal(0, dry.AmbiguousNodes);
        Assert.DoesNotContain(r.Issues, i => i.Severity is "ERROR" or "WARNING");
    }

    [Fact]
    public async Task Chapter_84_machines_the_classifier_depends_on()
    {
        if (await Parse("chapter-84.pdf") is not { } r) return;

        var by = r.TariffNodes.ToDictionary(n => n.Code);
        Assert.Contains("8429.52.00", by.Keys);
        Assert.Equal("8431.41", by["8431.41.10"].ParentCode);
        Assert.Contains("Laptop", by["8471.30.20"].DescriptionId);
        Assert.Equal("8471.30", by["8471.30.20"].ParentCode);
        Assert.Contains("10 kg", by["8471.30"].DescriptionId);
        Assert.Contains("pos 84.26", by["8431.41.10"].DescriptionId);

        Assert.True(r.TariffNodes.Count(n => n.Code.Length == 10) > 1200);
        Assert.Equal(0, BtkiStructureService.DryRun(r.TariffNodes).ErrorCount);
        Assert.DoesNotContain(r.Issues, i => i.Severity is "ERROR" or "WARNING");
    }

    [Fact]
    public async Task Superscripts_stay_inside_their_row()
    {
        if (await Parse("chapter-56.pdf") is not { } r) return;

        var by = r.TariffNodes.ToDictionary(n => n.Code);
        Assert.Contains("g/m\u00B2", by["5603.11.00"].DescriptionId);
        Assert.Contains("g/m\u00B2", by["5603.12.00"].DescriptionId);
        Assert.Contains("70 g/m\u00B2", by["5603.12.00"].DescriptionId);
        Assert.All(r.TariffNodes, n => Assert.Contains(n.DescriptionId, c => char.IsLetter(c)));
    }

    [Fact]
    public async Task Scanned_chapter_is_reported_not_guessed()
    {
        if (await Parse("chapter-01.pdf") is not { } r) return;

        Assert.Empty(r.TariffNodes);
        Assert.Contains(r.Issues, i => i.Type == "NO_TEXT_PAGE");
        Assert.Contains(r.Issues, i => i.Type == "NO_TARIFF_ROWS" && i.Severity == "ERROR");
    }
}
