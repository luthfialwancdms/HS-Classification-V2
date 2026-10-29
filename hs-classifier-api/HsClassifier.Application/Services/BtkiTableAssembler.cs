using System.Text.RegularExpressions;
using HsClassifier.Application.Interfaces;

namespace HsClassifier.Application.Services;

/// <summary>
/// One word of a PDF page: horizontal extent, text baseline (PDF units, origin bottom-left) and font size.
/// Baseline, not the glyph box, because glyph boxes differ per letter ("-" is 1.7 high, "g" has a descender).
/// </summary>
public readonly record struct PdfWordBox(string Text, double Left, double Right, double Baseline, double Size = 5.8);

public record PdfPageWords(int PageNumber, double Width, double Height, IReadOnlyList<PdfWordBox> Words);

public enum BtkiRowKind { Heading, Subheading, Line, Group }

/// <summary>One logical row of the BTKI table (a code and/or its wrapped description lines).</summary>
public class BtkiTableRow
{
    public BtkiRowKind Kind { get; init; }
    /// <summary>"7404" | "7407.10" | "7407.10.30" | "7407.1"; null for a one-dash group that has no code in the PDF.</summary>
    public string? Code { get; init; }
    public string TextId { get; set; } = string.Empty;
    public string TextEn { get; set; } = string.Empty;
    public int Page { get; init; }
    public int Seq { get; init; }
    /// <summary>Number of leading "-" in the Indonesian text: 0 for headings, 1 for "- ...", 2 for "- - ..." and so on.</summary>
    public int Depth => BtkiTableAssembler.Dashes(TextId);
}

public record BtkiAssembly(List<BtkiTableRow> Rows, int SkippedLines);

/// <summary>
/// Turns the words of the official BTKI PDF tables (Pos Tarif | Uraian Barang | Description of Goods) into rows.
/// Pure: it only sees words and their positions, so it is testable without a PDF.
/// Columns are found from the whitespace corridors of each page, not from fixed coordinates.
/// A new row starts at a line with a code in the first column, or at a line whose Indonesian text starts
/// with "-" and has no code (a group heading). Every other line continues the current row.
/// </summary>
public static class BtkiTableAssembler
{
    private static readonly HashSet<string> HeaderWords = new(StringComparer.OrdinalIgnoreCase)
        { "Pos", "Tarif", "Uraian", "Barang", "Description", "of", "Goods", "HS", "Code" };

    private static readonly Regex HeadingCode = new(@"^(\d{2})\.(\d{2})$");
    private static readonly Regex SubCode = new(@"^(\d{4})\.(\d{2})$");
    private static readonly Regex LineCode = new(@"^(\d{4})\.(\d{2})\.(\d{2})$");
    private static readonly Regex GroupCode = new(@"^(\d{4})\.(\d)$");

    private sealed record Columns(double CodeEnd, double EnStart);
    private sealed record Gap(double Start, double End)
    {
        public double Width => End - Start;
    }

    public static BtkiAssembly Assemble(IEnumerable<PdfPageWords> pages, List<ParseIssue> issues)
    {
        var rows = new List<BtkiTableRow>();
        BtkiTableRow? cur = null;
        Columns? prev = null;
        var skipped = 0;
        // A code printed on its own line whose text starts on the next, slightly lower line ("4403.23" then "- - Dari fir ...")
        var awaitingText = false;

        foreach (var page in pages)
        {
            var words = page.Words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
            if (words.Count == 0) continue;

            var lines = ToLines(words);
            var headerFlags = lines.Select(l => IsHeaderLine(l, page)).ToList();
            var header = lines.Where((_, i) => headerFlags[i]).SelectMany(l => l).ToList();
            var bodyLines = lines.Where((_, i) => !headerFlags[i]).ToList();
            skipped += headerFlags.Count(f => f);
            var body = bodyLines.SelectMany(l => l).ToList();
            if (body.Count == 0) continue;

            var cols = DetectColumns(page, header, body, prev, issues);
            prev = cols;
            var seq = 0;

            foreach (var line in bodyLines)
            {
                if (IsPageNumber(line, page)) { skipped++; continue; }
                var code = Join(line.Where(w => w.Left < cols.CodeEnd));
                var id = Join(line.Where(w => w.Left >= cols.CodeEnd && w.Left < cols.EnStart));
                var en = Join(line.Where(w => w.Left >= cols.EnStart));

                if (code.Length > 0 && TryCode(code, out var kind, out var normalised))
                {
                    cur = new BtkiTableRow { Kind = kind, Code = normalised, TextId = id, TextEn = en, Page = page.PageNumber, Seq = ++seq };
                    rows.Add(cur);
                    awaitingText = id.Length == 0;
                }
                else if (awaitingText && code.Length == 0 && cur is not null)
                {
                    cur.TextId = Append(cur.TextId, id);
                    cur.TextEn = Append(cur.TextEn, en);
                    awaitingText = false;
                }
                else if (StartsWithDash(id))
                {
                    awaitingText = false;
                    if (code.Length > 0)
                        issues.Add(new ParseIssue
                        {
                            Severity = "WARNING", Type = "UNPARSEABLE_CODE", RowNumber = rows.Count + 1,
                            Message = $"Halaman {page.PageNumber}: kode '{code}' tidak dikenali; baris \"{Shorten(id)}\" diperlakukan sebagai grup tanpa kode.",
                        });
                    cur = new BtkiTableRow { Kind = BtkiRowKind.Group, Code = null, TextId = id, TextEn = en, Page = page.PageNumber, Seq = ++seq };
                    rows.Add(cur);
                }
                else
                {
                    if (code.Length > 0)
                        issues.Add(new ParseIssue
                        {
                            Severity = "WARNING", Type = "UNPARSEABLE_CODE", RowNumber = rows.Count,
                            Message = $"Halaman {page.PageNumber}: kode '{code}' tidak dikenali; barisnya disambung ke baris sebelumnya.",
                        });
                    if (cur is null)
                    {
                        skipped++;
                        issues.Add(new ParseIssue
                        {
                            Severity = "WARNING", Type = "ORPHAN_LINE", RowNumber = 0,
                            Message = $"Halaman {page.PageNumber}: baris tanpa kode di awal dokumen dilewati: \"{Shorten(id)}\".",
                        });
                        continue;
                    }
                    cur.TextId = Append(cur.TextId, id);
                    cur.TextEn = Append(cur.TextEn, en);
                }
            }
        }
        return new BtkiAssembly(rows, skipped);
    }

    // ---- lines and columns ----------------------------------------------------

    /// <summary>
    /// Words on the same baseline (within 2.5) form a line, left to right; rows are at least 6 apart.
    /// Small words (superscripts such as the 2 in "g/m2") sit 3 above the line, so they are set aside first and
    /// glued to the word they touch afterwards, otherwise they would split a row in two.
    /// </summary>
    private static List<List<PdfWordBox>> ToLines(List<PdfWordBox> words)
    {
        var median = words.Select(w => w.Size).OrderBy(s => s).ElementAt(words.Count / 2);
        var normal = words.Where(w => w.Size >= median * 0.8);
        var small = words.Where(w => w.Size < median * 0.8).ToList();

        var lines = new List<List<PdfWordBox>>();
        var anchor = double.NaN;
        foreach (var w in normal.OrderByDescending(w => w.Baseline).ThenBy(w => w.Left))
        {
            if (lines.Count == 0 || anchor - w.Baseline > 2.5) { lines.Add([w]); anchor = w.Baseline; }
            else lines[^1].Add(w);
        }

        foreach (var s in small.OrderBy(s => s.Left))
        {
            (List<PdfWordBox> Line, int Index)? host = null;
            var best = double.MaxValue;
            foreach (var line in lines)
                for (var i = 0; i < line.Count; i++)
                {
                    var dy = Math.Abs(line[i].Baseline - s.Baseline);
                    var dx = Math.Abs(s.Left - line[i].Right);
                    if (dy > 5 || dx > 2.0 || dx + dy * 0.1 >= best) continue;
                    best = dx + dy * 0.1; host = (line, i);
                }
            if (host is { } h)
            {
                var w = h.Line[h.Index];
                // below the host's baseline = subscript (Al2O3), above = superscript (g/m2)
                var glyphs = s.Baseline < w.Baseline - 0.7 ? Subscript(s.Text) : Superscript(s.Text);
                var merged = w with { Text = w.Text + glyphs, Right = Math.Max(w.Right, s.Right) };
                // "Al" + sub 2 + "O": the letter that follows the subscript touches it, so it belongs to the same word
                var next = h.Line.FindIndex(x => x != w && Math.Abs(x.Baseline - w.Baseline) < 0.5 && x.Left >= merged.Right - 0.5 && x.Left - merged.Right <= 1.0);
                if (next >= 0 && s.Baseline < w.Baseline - 0.7)
                {
                    merged = merged with { Text = merged.Text + h.Line[next].Text, Right = h.Line[next].Right };
                    h.Line.RemoveAt(next);
                    h.Index = h.Line.IndexOf(w);
                }
                h.Line[h.Line.IndexOf(w)] = merged;
                continue;
            }
            var near = lines.Where(l => Math.Abs(l[0].Baseline - s.Baseline) <= 5).OrderBy(l => Math.Abs(l[0].Baseline - s.Baseline)).FirstOrDefault();
            if (near is not null) near.Add(s); else lines.Add([s]);
        }
        return lines;
    }

    private static string Subscript(string t) =>
        string.Concat(t.Select(c => c is >= '0' and <= '9' ? (char)('\u2080' + (c - '0')) : c));

    /// <summary>Small digits become superscript digits; a small "o" or "º" next to a number is a degree sign.</summary>
    private static string Superscript(string t) => t is "o" or "\u00BA"
        ? "\u00B0"
        : string.Concat(t.Select(c => c is >= '0' and <= '9' ? "\u2070\u00B9\u00B2\u00B3\u2074\u2075\u2076\u2077\u2078\u2079"[c - '0'] : c));

    private static bool IsHeaderLine(List<PdfWordBox> line, PdfPageWords page) =>
        line.All(w => HeaderWords.Contains(w.Text)) && line.Max(w => w.Baseline) > page.Height * 0.75;

    private static bool IsPageNumber(List<PdfWordBox> line, PdfPageWords page) =>
        line.Count == 1 && line[0].Text.Length <= 4 && line[0].Text.All(char.IsDigit)
        && (line[0].Baseline < page.Height * 0.07 || line[0].Baseline > page.Height * 0.93);

    private static Columns DetectColumns(PdfPageWords page, List<PdfWordBox> header, List<PdfWordBox> body,
        Columns? prev, List<ParseIssue> issues)
    {
        var minLeft = body.Min(w => w.Left);
        var gaps = Gaps(body);

        // Code | text: the first corridor right after the code column (which is at most ~60 wide).
        // A page with no code column at all (only wrapped text) keeps the previous page's boundary.
        double codeEnd;
        if (prev is not null && minLeft >= prev.CodeEnd - 5) codeEnd = prev.CodeEnd;
        else
        {
            var gapA = gaps.FirstOrDefault(g => g.Start >= minLeft + 10 && g.Start <= minLeft + 60 && g.Width >= 0.5);
            if (gapA is not null) codeEnd = gapA.End;
            else if (prev is not null) codeEnd = prev.CodeEnd;
            else
            {
                var textLefts = body.Where(w => w.Left > minLeft + 30).Select(w => w.Left).ToList();
                codeEnd = textLefts.Count > 0 ? textLefts.Min() - 1 : minLeft + 40;
            }
        }

        // Indonesian | English: the corridor nearest to the middle of the two header titles.
        var uraian = header.Where(w => w.Text.Equals("Uraian", StringComparison.OrdinalIgnoreCase) || w.Text.Equals("Barang", StringComparison.OrdinalIgnoreCase)).ToList();
        var english = header.Where(w => w.Text.Equals("Description", StringComparison.OrdinalIgnoreCase) || w.Text.Equals("Goods", StringComparison.OrdinalIgnoreCase)).ToList();
        double estimate = uraian.Count > 0 && english.Count > 0
            ? ((uraian.Min(w => w.Left) + uraian.Max(w => w.Right)) / 2 + (english.Min(w => w.Left) + english.Max(w => w.Right)) / 2) / 2
            : prev?.EnStart ?? page.Width * 0.53;

        // The English column starts where the corridor ends (just left of its first word). On a page whose Indonesian
        // text is short everywhere the corridor is wide, but it still ends at the English column.
        // The corridor can be as narrow as ~1.4 (Indonesian text ends at 419.2, English starts at 421.4).
        var gapB = gaps.Where(g => g.End > codeEnd + 100 && g.Width >= 0.5 && Math.Abs(g.End - estimate) < page.Width * 0.15)
            .OrderBy(g => Math.Abs(g.End - estimate)).FirstOrDefault();
        if (gapB is null)
            issues.Add(new ParseIssue
            {
                Severity = "WARNING", Type = "COLUMN_FALLBACK", RowNumber = 0,
                Message = $"Halaman {page.PageNumber}: batas kolom Indonesia/Inggris tidak ditemukan; memakai perkiraan x={estimate:0}.",
            });
        return new Columns(codeEnd, gapB?.End ?? estimate);
    }

    /// <summary>White corridors: x ranges that no word on the page covers.</summary>
    private static List<Gap> Gaps(List<PdfWordBox> words)
    {
        var gaps = new List<Gap>();
        var end = double.NaN;
        foreach (var w in words.OrderBy(w => w.Left))
        {
            var l = w.Left - 0.4;
            if (double.IsNaN(end)) { end = w.Right + 0.4; continue; }
            if (l > end) gaps.Add(new Gap(end, l));
            end = Math.Max(end, w.Right + 0.4);
        }
        return gaps;
    }

    // ---- text helpers -----------------------------------------------------------

    private static string Join(IEnumerable<PdfWordBox> words) =>
        string.Join(' ', words.OrderBy(w => w.Left).Select(w => w.Text.Trim()).Where(t => t.Length > 0));

    private static string Append(string a, string b)
    {
        if (b.Length == 0) return a;
        if (a.Length == 0) return b;
        // a word split over two lines at its hyphen: "semata-" + "mata" is "semata-mata"
        if (a.Length >= 2 && a[^1] == '-' && char.IsLetter(a[^2]) && char.IsLower(b[0])) return a + b;
        return a + " " + b;
    }

    private static string Shorten(string s) => s.Length > 60 ? s[..60] + "..." : s;

    private static bool IsDash(string t) => t is "-" or "\u2013" or "\u2014";

    private static bool StartsWithDash(string text) => Dashes(text) > 0;

    /// <summary>Count of leading standalone "-" tokens ("- - - Lain-lain" gives 3).</summary>
    public static int Dashes(string text)
    {
        var n = 0;
        foreach (var t in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!IsDash(t)) break;
            n++;
        }
        return n;
    }

    private static bool TryCode(string raw, out BtkiRowKind kind, out string code)
    {
        raw = raw.Trim();
        kind = BtkiRowKind.Line; code = raw;
        if (HeadingCode.Match(raw) is { Success: true } h) { kind = BtkiRowKind.Heading; code = h.Groups[1].Value + h.Groups[2].Value; return true; }
        if (SubCode.IsMatch(raw)) { kind = BtkiRowKind.Subheading; return true; }
        if (LineCode.IsMatch(raw)) { kind = BtkiRowKind.Line; return true; }
        if (GroupCode.IsMatch(raw)) { kind = BtkiRowKind.Group; return true; }
        return false;
    }
}
