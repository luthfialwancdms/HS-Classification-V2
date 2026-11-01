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
