using System.Text.Json;
using System.Text.RegularExpressions;
using HsClassifier.Application.Interfaces;

namespace HsClassifier.Infrastructure.Parsing;

/// <summary>One non-empty Word paragraph with the few properties the EN parser needs. Index = Word paragraph ordinal (1-based).</summary>
public record RawLine(int Index, string Text, bool Bold = false, bool IsList = false, bool InTable = false);

/// <summary>
/// Turns the lines of the WCO Explanatory Notes Word file into paragraphs per 4-digit heading.
/// Built from what the file looks like (docs/TEMUAN_EN.md): OCR text, headings written "84.71- Title" in bold,
/// sentences wrapped over several Word paragraphs, English Section/Chapter notes at the start of each chapter
/// (those come from the PMK, not from here), listings of subheadings right after each heading.
/// ponytail: rules are line patterns, not a real layout analysis. A heading whose marker OCR garbled and whose
/// subheading listing is also garbled is not found; the dry run reports which database headings got no EN.
/// </summary>
public static class EnTextParser
{
    private static readonly Regex Noise = new(@"^[\s()\[\]|.\-–—_:;,*+]*$|^\d{1,3}$|^[IVXLC]{1,4}$", RegexOptions.Compiled);
    private static readonly Regex DottedHeading = new(@"^\p{Pi}?\p{Pf}?'?(\d{2})\.(\d{2})(?![\d.])\s*(?:[-–—:]\s*)?(.*)$", RegexOptions.Compiled);
    private static readonly Regex GarbledHeading = new(@"^\d{2}[.\s]\s?\d", RegexOptions.Compiled);
    private static readonly Regex SubLine = new(@"^(\d{4})\.(\d{2})\b\s*(?:[-–—]\s*)?(.*)$", RegexOptions.Compiled);
    private static readonly Regex ChapterLine = new(@"^\p{Pi}?\p{Pf}?'?Chapt[eo]r\s*(\d{1,2})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SectionLine = new(@"^\p{Pi}?\p{Pf}?'?Sect[il]on\s*[IVXL]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NotesLine = new(@"^\p{Pi}?\p{Pf}?'?(Sub-?heading\s+)?Notes?\s*\.?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GeneralLine = new(@"^GENERAL(\s+CONSIDERATIONS)?\.?$", RegexOptions.Compiled);
    private static readonly Regex TableCode = new(@"^(\d{4})\.\d{2}$", RegexOptions.Compiled);
    private static readonly Regex Number = new(@"^\(([IVX]+|[A-Z]|\d+)\)\s", RegexOptions.Compiled);

    private static readonly Regex ExclusionLead = new(
        @"^(however,\s+)?(this|the)\s+(heading|group|subheading|chapter|item)\s+(also\s+)?(does\s+not\s+(cover|include|apply)|excludes?)\b" +
        @"|^(the\s+)?(following|these)\s+(are|is)\s+(also\s+)?excluded\b|^excluded\b|^(but\s+)?(does|do)\s+not\s+(cover|include)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InclusionLead = new(
        @"^(this|the)\s+(heading|group|subheading)\s+(also\s+)?(covers|includes|comprises|applies\s+to)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DefinitionLead = new(
        @"^(the\s+)?(terms?|expressions?)\b.{0,120}\b(means|covers|refers\s+to)\b|\bis\s+defined\s+as\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ExampleLead = new(@"^(for\s+example|examples?\b|e\.g\.)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CrossRefLead = new(@"^\(?\s*see\b|^for\s+further\s+details?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private enum Mode { Front, Notes, ChapterGeneral, Heading }

    private sealed class Buf
    {
        public int Index; public string Text = ""; public bool IsList; public bool IsTitle; public string Heading = ""; public bool WasList;
    }

    public static EnParseResult Parse(IEnumerable<RawLine> lines, string fileName, string checksum)
    {
        var result = new EnParseResult { SourceFileName = fileName, SourceChecksum = checksum };
        var mode = Mode.Front;
        string? chapter = null, heading = null;
        var lastHeading = "";
        var bodyStarted = false;
        var exclusionList = false; var inclusionList = false;
        var chapterLevel = 0;
        Buf? buf = null;
        RawLine? suspect = null;
        var seq = new Dictionary<string, int>();

        void Emit(Buf b)
        {
            var text = Regex.Replace(b.Text, @"\s+", " ").Trim();
            if (text.Length < (b.IsTitle ? 4 : 15)) { result.SkippedLines++; return; }
            var type = b.IsTitle ? "SCOPE" : Classify(text, b.IsList, ref exclusionList, ref inclusionList);
            var referred = HsCodes(text);
            seq[b.Heading] = seq.GetValueOrDefault(b.Heading) + 1;
            result.Paragraphs.Add(new ParsedEnParagraph
            {
                HeadingCode = b.Heading,
                ParagraphNumber = Number.Match(text) is { Success: true } n ? n.Groups[1].Value : null,
                ParagraphType = type,
                TextContent = text,
                ReferredCodesJson = referred.Count > 0 ? JsonSerializer.Serialize(referred) : null,
                SourceId = $"docx:{checksum}:en:{b.Heading}:para_{b.Index}",
                SequenceInHeading = seq[b.Heading],
            });
            if (!result.Headings.Contains(b.Heading)) result.Headings.Add(b.Heading);
        }

        void Flush() { if (buf is not null) { Emit(buf); buf = null; } }

        void OpenHeading(string code)
        {
            Flush();
            heading = code; lastHeading = code; mode = Mode.Heading; bodyStarted = false;
            exclusionList = false; inclusionList = false;
        }

        // A subheading code alone in a table cell ("0101.21") also names its heading
        bool OpenFromSubCode(string code)
        {
            if (chapter is null || code == heading || code[..2] != chapter || string.CompareOrdinal(code, lastHeading) <= 0) return false;
            OpenHeading(code);
            return true;
        }

        void Body(RawLine l, string text)
        {
            if (heading is null) return;
            var title = IsTitle(text);
            var continues = buf is { IsTitle: false } && !title && !l.IsList && !text.StartsWith("- ")
                && (StartsLower(text) || buf.Text.EndsWith(','));
            if (continues) { buf!.Text += " " + text; return; }
            Flush();
            buf = new Buf { Index = l.Index, Text = text, IsList = l.IsList || text.StartsWith("- "), IsTitle = title, Heading = heading };
            bodyStarted = true;
        }

        void Handle(RawLine l, string text)
        {
            // Section / chapter structure
            if (ChapterLine.Match(text) is { Success: true } cm)
            {
                var number = int.Parse(cm.Groups[1].Value);
                if (chapter is null || (number > int.Parse(chapter) && number <= int.Parse(chapter) + 3))
                {
                    Flush(); chapter = number.ToString("00"); heading = null; lastHeading = ""; mode = Mode.ChapterGeneral; return;
                }
            }
            if (SectionLine.IsMatch(text)) { Flush(); heading = null; mode = Mode.Front; return; }
            if (mode != Mode.Heading && NotesLine.IsMatch(text)) { Flush(); mode = Mode.Notes; return; }
            if (mode == Mode.Notes && GeneralLine.IsMatch(text)) { mode = Mode.ChapterGeneral; return; }

            // Heading marker "84.71- Title"
            if (chapter is not null && DottedHeading.Match(text) is { Success: true } dm)
            {
                var code = dm.Groups[1].Value + dm.Groups[2].Value;
                var rest = dm.Groups[3].Value;
                var dash = Regex.IsMatch(text, @"^\p{Pi}?\p{Pf}?'?\d{2}\.\d{2}\s*[-–—:]");
                if ((l.Bold || (dash && rest.Length >= 8)) && code[..2] == chapter && string.CompareOrdinal(code, lastHeading) > 0)
                { OpenHeading(code); return; }
            }
            // Heading whose marker is garbled: its subheading listing ("8471.30 - ...") names it
            if (chapter is not null && SubLine.Match(text) is { Success: true } sm)
            {
                var code = sm.Groups[1].Value;
                if (code == heading && !bodyStarted) { result.SkippedLines++; return; }
                if (code != heading && code[..2] == chapter && string.CompareOrdinal(code, lastHeading) > 0)
                { OpenHeading(code); result.SkippedLines++; return; }
            }

            switch (mode)
            {
                case Mode.Front: result.SkippedLines++; return;
                case Mode.Notes: case Mode.ChapterGeneral: chapterLevel++; return;
            }

            if (!bodyStarted && text.StartsWith("- ") && text.Length < 140) { result.SkippedLines++; return; } // listing continuation
            Body(l, text);
        }

        foreach (var l in lines)
        {
            var text = Regex.Replace(l.Text ?? "", @"\s+", " ").Trim();
            if (text.Length == 0 || Noise.IsMatch(text)) { result.SkippedLines++; continue; }
            if (l.InTable && text.Length <= 40)
            {
                if (TableCode.Match(text) is { Success: true } tc) OpenFromSubCode(tc.Groups[1].Value);
                result.SkippedLines++;
                continue;
            }

            // A bold "84.7 1- ..." line that is not a valid marker: held back until we know whether a listing follows it
            if (suspect is not null)
            {
                var s = suspect; suspect = null;
                if (SubLine.IsMatch(text)) result.SkippedLines++;          // it was the garbled marker: drop it
                else Handle(s, Regex.Replace(s.Text, @"\s+", " ").Trim()); // it was ordinary text: process it now
            }
            if (l.Bold && !DottedHeading.IsMatch(text) && GarbledHeading.IsMatch(text) && mode == Mode.Heading)
            { suspect = l; continue; }

            Handle(l, text);
        }
        if (suspect is not null) Handle(suspect, Regex.Replace(suspect.Text, @"\s+", " ").Trim());
        Flush();

        result.TotalParagraphs = result.Paragraphs.Count;
        if (chapterLevel > 0)
            result.Issues.Add(new ParseIssue { Severity = "INFO", Type = "CHAPTER_LEVEL_SKIPPED",
                Message = $"{chapterLevel} baris catatan/uraian umum tingkat bab tidak disimpan sebagai paragraf pos." });
        if (result.Paragraphs.Count == 0)
            result.Issues.Add(new ParseIssue { Severity = "ERROR", Type = "NO_PARAGRAPHS", Message = "Tidak ada paragraf EN yang dikenali." });
        return result;
    }

    private static bool StartsLower(string t) =>
        t.Length > 0 && (char.IsLower(t[0]) || (t[0] == '(' && t.Length > 1 && char.IsLower(t[1])));

    private static bool IsTitle(string t)
    {
        var letters = t.Count(char.IsLetter);
        if (letters < 6 || t.EndsWith('.')) return false;
        return (double)t.Count(char.IsUpper) / letters >= 0.8;
    }

    private static string Classify(string t, bool isList, ref bool exclusionList, ref bool inclusionList)
    {
        if (isList)
        {
            if (exclusionList) return "EXCLUSION";
            if (inclusionList) return "INCLUSION";
        }
        else { exclusionList = false; inclusionList = false; }

        var listFollows = t.EndsWith(':') || t.EndsWith(":-");
        if (ExclusionLead.IsMatch(t)) { exclusionList = listFollows; return "EXCLUSION"; }
        if (InclusionLead.IsMatch(t)) { inclusionList = listFollows; return "INCLUSION"; }
        if (DefinitionLead.IsMatch(t)) return "DEFINITION";
        if (ExampleLead.IsMatch(t)) return "EXAMPLE";
        if (t.Length < 160 && CrossRefLead.IsMatch(t)) return "CROSS_REFERENCE";
        return "SCOPE";
    }

    /// <summary>Headings named in the text: "95.08", "8471", "8471.30" all give the 4-digit heading.</summary>
    public static List<string> HsCodes(string text)
    {
        var codes = new List<string>();
        foreach (Match m in Regex.Matches(text, @"\b(\d{2})\.(\d{2})(?:\.(\d{2}))?\b"))
            codes.Add(m.Value.Replace(".", "")[..4]);
        foreach (Match m in Regex.Matches(text, @"\b(\d{4})\b"))
            codes.Add(m.Value);
        return codes.Distinct().ToList();
    }
}
