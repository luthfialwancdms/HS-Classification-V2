using System.Text.Json;
using HsClassifier.Infrastructure.Parsing;

namespace HsClassifier.Tests;

/// <summary>
/// Opt-in checks against the real Word files. They do nothing unless the environment variable is set, so the normal
/// test run stays fast and does not need the files:
/// <c>HS_EN_DOCX=D:\...\EN....docx dotnet test --filter RealFileChecks</c>. The report goes to the temp folder.
/// </summary>
public class RealFileChecks
{
    [Fact]
    public async Task Real_notes_file_report()
    {
        var path = Environment.GetEnvironmentVariable("HS_PMK_DOCX");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        await using var fs = File.OpenRead(path);
        var r = await new DocxParser().ParseLegalNotesAsync(fs, Path.GetFileName(path));
        string Cut(string s, int n = 170) => s.Replace('\n', '|')[..Math.Min(n, s.Length)];
        var report = new
        {
            sections = r.SectionNotes.Count, chapters = r.ChapterNotes.Count, subheadings = r.SubheadingNotes.Count,
            sectionScopes = r.SectionNotes.Select(n => n.ScopeCode).Distinct().ToList(),
            chapterScopes = r.ChapterNotes.Select(n => n.ScopeCode).Distinct().ToList(),
            subheadingScopes = r.SubheadingNotes.Select(n => n.ScopeCode).Distinct().ToList(),
            withoutIndonesian = r.ChapterNotes.Concat(r.SectionNotes).Count(n => n.TextId.Length == 0),
            withoutEnglish = r.ChapterNotes.Concat(r.SectionNotes).Count(n => n.TextEn is null),
            byEffect = r.ChapterNotes.Concat(r.SectionNotes).Concat(r.SubheadingNotes).GroupBy(n => n.EffectType).ToDictionary(g => g.Key, g => g.Count()),
            issues = r.Issues.GroupBy(i => $"{i.Severity} {i.Type}").ToDictionary(g => g.Key, g => g.Count()),
            sample84 = r.ChapterNotes.Where(n => n.ScopeCode == "84").Take(4).Select(n => $"{n.NoteNumber} [{n.EffectType}] ID: {Cut(n.TextId)} || EN: {Cut(n.TextEn ?? "-")}"),
            sampleXVI = r.SectionNotes.Where(n => n.ScopeCode == "XVI").Take(3).Select(n => $"{n.NoteNumber} [{n.EffectType}] ID: {Cut(n.TextId)}"),
            scopes74to83 = r.ChapterNotes.Where(n => string.CompareOrdinal(n.ScopeCode, "74") >= 0 && string.CompareOrdinal(n.ScopeCode, "83") <= 0).Select(n => $"{n.ScopeCode}/{n.NoteNumber} id={n.TextId.Length} en={n.TextEn?.Length ?? 0} {Cut(n.TextId.Length > 0 ? n.TextId : n.TextEn ?? "", 70)}"),
            sampleSub = r.SubheadingNotes.Take(4).Select(n => $"{n.ScopeCode}/{n.NoteNumber} [{n.EffectType}] {Cut(n.TextId)}"),
        };
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "notes_report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Assert.True(r.ChapterNotes.Count > 0);
    }

    [Fact]
    public async Task Real_en_file_report()
    {
        var path = Environment.GetEnvironmentVariable("HS_EN_DOCX");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        await using var fs = File.OpenRead(path);
        var r = await new DocxParser().ParseENAsync(fs, Path.GetFileName(path));

        var sample = new[] { "8413", "8426", "8429", "8471", "8704", "8905" }.ToDictionary(h => h, h =>
            r.Paragraphs.Where(p => p.HeadingCode == h).Take(4).Select(p => $"[{p.ParagraphType}] {p.TextContent[..Math.Min(160, p.TextContent.Length)]}").ToList());
        var report = new
        {
            r.TotalParagraphs, r.SkippedLines, headings = r.Headings,
            byType = r.Paragraphs.GroupBy(p => p.ParagraphType).ToDictionary(g => g.Key, g => g.Count()),
            perHeadingMedian = r.Paragraphs.GroupBy(p => p.HeadingCode).Select(g => g.Count()).Order().ElementAtOrDefault(r.Headings.Count / 2),
            longest = r.Paragraphs.Max(p => p.TextContent.Length),
            issues = r.Issues.Select(i => $"{i.Severity} {i.Type}: {i.Message}"),
            sample,
        };
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "en_report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Assert.True(r.TotalParagraphs > 0);
    }
}
