using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HsClassifier.Application.Interfaces;

namespace HsClassifier.Infrastructure.Parsing;

/// <summary>
/// Catatan Bagian / Bab / Subpos from Lampiran II of PMK 26/PMK.010/2022 (layout in docs/TEMUAN_EN.md).
/// Per Bab the file has an Indonesian block ("Bab 84", "Catatan.", "1.- ...") and then an English block
/// ("Chapter 84", "Notes.", "1.- ..."); notes are paired by number. In the first Sections/Chapters both languages
/// are glued into one paragraph; that text is kept whole in TextId and reported as MIXED_LANGUAGE.
/// ponytail: line patterns, not layout analysis. Notes whose number line is garbled by OCR are appended to the
/// previous note; the report counts notes per scope so a gap is visible.
/// </summary>
public static class LegalNotesTextParser
{
    private static readonly Regex ChapterId = new(@"^Bab\s*(\d{1,2})(?<glue>\s*Chapt[eo]r\s*\d{1,2})?(?:\s*$|\s*(?=(?-i:\p{Lu})))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ChapterEn = new(@"^Chapt[eo]r\s*(\d{1,2})(?:\s*$|\s+(?=(?-i:\p{Lu})))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NotesId = new(@"^Catatan\s*\.?(?<glue>\s*Notes?\.?)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NotesSubId = new(@"^Catatan\s+Subpos\s*\.?(?<glue>\s*Sub-?heading\s+Notes?\s*\.?)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NotesEn = new(@"^(?<sub>Sub-?heading\s+)?Notes?\s*\.?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NoteStart = new(@"^(\d{1,2})\s*\.\s*[-–—]\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex SubheadingRef = new(@"(?:subpos|subheading|sub-?pos)\s+(\d{4})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Exclusion = new(@"tidak\s+(meliputi|termasuk|mencakup|berlaku)|dikecualikan|does\s+not\s+(cover|include|apply)|\bexcludes?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Condition = new(@"dengan\s+syarat|asalkan|provided\s+that", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Definition = new(@"\bberarti\b|\bmeans\b|\bdimaksud\s+dengan\b|\bis\s+defined\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Referral = new(@"diklasifikasikan\s+(dalam|pada|ke|sebagai)|harus\s+diklasifikasikan|classified\s+(in|under|as)|\blihat\b|\bsee\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Inclusion = new(@"\bmeliputi\b|\bmencakup\b|\btermasuk\b|\bcovers?\b|\bincludes?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal enum Lang { None, Id, En }
    private enum Kind { Section, Chapter, Subheading }

    private sealed class Note
    {
        public Kind Kind; public string Scope = ""; public Lang Lang; public string Number = "1";
        public readonly StringBuilder Text = new(); public int FirstIndex; public bool Mixed;
    }

    private sealed class Track { public Kind Kind; public Note? Cur; }

    private static readonly HashSet<string> IdWords = new(
        ("yang dan atau dari dengan untuk pada ini tidak dalam bab pos berarti termasuk barang adalah sebagai oleh hanya lainnya meliputi " +
         "bagian harus dapat serta kecuali sampai antara yaitu tersebut apabila jika istilah berdasarkan seperti suatu semua maupun bahwa " +
         "dimaksud merupakan digunakan dibuat mengenai kepada atas setiap").Split(' '));
    private static readonly HashSet<string> EnWords = new(
        ("the of and or for this with that which not chapter heading headings to in are is be by as from other than whether any covers cover " +
         "section note only such including goods subheading if its these those except apply articles products being has have shall").Split(' '));

    /// <summary>Language of one line from its function words; a tie (codes, names) keeps the language of the previous line.</summary>
    internal static (Lang Lang, bool Mixed) DetectLanguage(string text, Lang previous)
    {
        int id = 0, en = 0;
        foreach (Match m in Regex.Matches(text.ToLowerInvariant(), @"[a-z]+"))
        {
            if (IdWords.Contains(m.Value)) id++;
            if (EnWords.Contains(m.Value)) en++;
        }
        if (id >= 3 && en >= 3 && Math.Min(id, en) >= 0.4 * Math.Max(id, en)) return (Lang.Id, true);
        if (id > en) return (Lang.Id, false);
        if (en > id) return (Lang.En, false);
        return (previous, false);
    }

    private static readonly HashSet<string> Romans = new(
        ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX", "XXI"]);

    /// <summary>"BAGIAN IV", "BAGIANIV", "BAGIAN Ill" (OCR l for I), "BAGIAN XVISECTION XVI"; null for ordinary text starting with BAGIAN.</summary>
    private static string? SectionNumber(string text)
    {
        var m = Regex.Match(text, @"^(?:BAGIAN|SECTION)\s*([IVXLl1]+)");
        if (!m.Success) return null;
        var rest = text[m.Length..];
        if (rest.Length > 0 && !char.IsWhiteSpace(rest[0]) && !rest.StartsWith("SECTION", StringComparison.Ordinal)) return null;
        var roman = m.Groups[1].Value.Replace('l', 'I').Replace('1', 'I');
        return Romans.Contains(roman) ? roman : null;
    }

    public static LegalNotesParseResult Parse(IEnumerable<RawLine> lines, string fileName, string checksum)
    {
        var result = new LegalNotesParseResult { SourceFileName = fileName, SourceChecksum = checksum };
        var all = lines.ToList();

        // Only Lampiran II holds the notes. Without the markers (a stand-alone notes file) everything is read.
        var from = all.FindIndex(l => Regex.IsMatch(l.Text.Trim(), @"^LAMPIRAN\s+II\s*$"));
        var to = from < 0 ? -1 : all.FindIndex(from + 1, l => Regex.IsMatch(l.Text.Trim(), @"^LAMPIRAN\s+III\b"));
        IEnumerable<RawLine> region = from < 0 ? all : all.Skip(from + 1).Take(to < 0 ? int.MaxValue : to - from - 1);

        // The two columns of the annex are read page by page, so Indonesian and English text alternate in chunks.
        // Each language therefore has its own track; a line goes to the track of the language it is written in.
        var notes = new List<Note>();
        var tracks = new Dictionary<Lang, Track> { [Lang.Id] = new(), [Lang.En] = new() };
        Kind scopeKind = Kind.Chapter; string? scope = null;
        var inNotes = false; var lastLang = Lang.Id;
        var mixedScopes = new HashSet<string>();

        void Flush(Lang l) { var t = tracks[l]; if (t.Cur is not null && t.Cur.Text.Length > 0) notes.Add(t.Cur); t.Cur = null; }
        void FlushAll() { Flush(Lang.Id); Flush(Lang.En); }
        void NewScope(Kind k, string code)
        {
            FlushAll(); scopeKind = k; scope = code; inNotes = false;
            tracks[Lang.Id].Kind = k; tracks[Lang.En].Kind = k;
        }

        foreach (var l in region)
        {
            var text = Regex.Replace(l.Text ?? "", @"\s+", " ").Trim();
            if (text.Length <= 2 || text.Contains("jdih.kemenkeu", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(text, @"^[^\p{L}\d]*$")) continue;

            if (SectionNumber(text) is { } roman)
            {
                if (text.StartsWith("BAGIAN", StringComparison.Ordinal) || scope != roman) NewScope(Kind.Section, roman);
                else { FlushAll(); inNotes = false; }
                if (text.Contains("SECTION", StringComparison.Ordinal) && text.StartsWith("BAGIAN", StringComparison.Ordinal)) mixedScopes.Add($"Section:{roman}");
                continue;
            }
            if (ChapterId.Match(text) is { Success: true } cm)
            {
                var code = int.Parse(cm.Groups[1].Value).ToString("00");
                NewScope(Kind.Chapter, code);
                if (cm.Groups["glue"].Success) mixedScopes.Add($"Chapter:{code}");
                continue;
            }
            if (ChapterEn.Match(text) is { Success: true } ce)
            {
                var code = int.Parse(ce.Groups[1].Value).ToString("00");
                if (scopeKind == Kind.Chapter && scope == code) { Flush(Lang.En); inNotes = false; }   // same chapter, English chunk starts
                else NewScope(Kind.Chapter, code);
                continue;
            }
            if (NotesSubId.Match(text) is { Success: true } ns)
            {
                Flush(Lang.Id); tracks[Lang.Id].Kind = Kind.Subheading; inNotes = true;
                if (ns.Groups["glue"].Success) { Flush(Lang.En); tracks[Lang.En].Kind = Kind.Subheading; if (scope is not null) mixedScopes.Add($"{scopeKind}:{scope}"); }
                continue;
            }
            if (NotesId.Match(text) is { Success: true } ni)
            {
                Flush(Lang.Id); tracks[Lang.Id].Kind = scopeKind; inNotes = true;
                if (ni.Groups["glue"].Success)
                {
                    Flush(Lang.En); tracks[Lang.En].Kind = scopeKind;
                    if (scope is not null) mixedScopes.Add($"{scopeKind}:{scope}");
                }
                continue;
            }
            if (NotesEn.Match(text) is { Success: true } ne)
            {
                Flush(Lang.En); tracks[Lang.En].Kind = ne.Groups["sub"].Success ? Kind.Subheading : scopeKind; inNotes = true;
                continue;
            }
            if (!inNotes || scope is null) continue;   // titles between the markers

            var (lang, mixed) = DetectLanguage(text, lastLang);
            lastLang = lang;
            if (mixed) mixedScopes.Add($"{scopeKind}:{scope}");
            var track = tracks[lang];

            var start = NoteStart.Match(text);
            if (start.Success)
            {
                Flush(lang);
                track.Cur = new Note { Kind = track.Kind, Scope = scope, Lang = lang, Number = int.Parse(start.Groups[1].Value).ToString(), FirstIndex = l.Index };
                track.Cur.Text.Append(text);
                continue;
            }
            if (track.Cur is null)
            {
                track.Cur = new Note { Kind = track.Kind, Scope = scope, Lang = lang, Number = "1", FirstIndex = l.Index };
                track.Cur.Text.Append(text);
                continue;
            }
            // continuation: list items go on their own line, wrapped text continues the line
            track.Cur.Text.Append(l.IsList || text.StartsWith("- ") ? '\n' : ' ').Append(text);
        }
        FlushAll();
        // Pair Indonesian and English notes
        string KeyOf(Note n)
        {
            var scopeKey = n.Kind == Kind.Subheading ? (HeadingOf(n.Text.ToString()) ?? n.Scope) : n.Scope;
            return $"{n.Kind}|{scopeKey}|{n.Number}";
        }
        var groups = notes.GroupBy(KeyOf).OrderBy(g => g.Min(n => n.FirstIndex)).ToList();
        foreach (var g in groups)
        {
            var id = g.FirstOrDefault(n => n.Lang == Lang.Id);
            var en = g.FirstOrDefault(n => n.Lang == Lang.En);
            var primary = id ?? en!;
            var textId = id?.Text.ToString().Trim() ?? "";
            var textEn = en?.Text.ToString().Trim();
            var lead = (textId.Length > 0 ? textId : textEn ?? "").Split('\n')[0];
            var referred = EnTextParser.HsCodes(textId + " " + textEn);
            var scopeCode = primary.Kind == Kind.Subheading ? (HeadingOf(primary.Text.ToString()) ?? primary.Scope) : primary.Scope;

            if (id is null)
                result.Issues.Add(new ParseIssue { Severity = "WARNING", Type = "ONLY_ENGLISH", Code = scopeCode,
                    Message = $"{primary.Kind} {scopeCode} catatan {primary.Number}: hanya teks Inggris yang ditemukan; mesin membaca teks Indonesia." });

            var note = new ParsedNote
            {
                ScopeCode = scopeCode,
                NoteType = primary.Kind switch { Kind.Section => "SECTION", Kind.Subheading => "SUBHEADING", _ => "CHAPTER" },
                NoteNumber = primary.Number,
                TextId = textId,
                TextEn = string.IsNullOrEmpty(textEn) ? null : textEn,
                EffectType = ClassifyEffect(lead),
                ReferredCodesJson = referred.Count > 0 ? JsonSerializer.Serialize(referred) : null,
                SourceId = $"docx:{checksum}:notes:{primary.Kind.ToString().ToLowerInvariant()}:{scopeCode}:{primary.Number}:para_{primary.FirstIndex}",
            };
            switch (primary.Kind)
            {
                case Kind.Section: result.SectionNotes.Add(note); break;
                case Kind.Subheading: result.SubheadingNotes.Add(note); break;
                default: result.ChapterNotes.Add(note); break;
            }
        }

        foreach (var s in mixedScopes.Order())
            result.Issues.Add(new ParseIssue { Severity = "INFO", Type = "MIXED_LANGUAGE", Code = s,
                Message = $"{s}: teks Indonesia dan Inggris menyatu dalam satu paragraf; disimpan utuh sebagai teks Indonesia." });
        if (result.SectionNotes.Count + result.ChapterNotes.Count + result.SubheadingNotes.Count == 0)
            result.Issues.Add(new ParseIssue { Severity = "ERROR", Type = "NO_NOTES", Message = "Tidak ada catatan yang dikenali." });
        return result;
    }

    private static string? HeadingOf(string text) =>
        SubheadingRef.Match(text) is { Success: true } m ? m.Groups[1].Value : EnTextParser.HsCodes(text).FirstOrDefault(c => c.Length == 4);

    /// <summary>The effect is read from the opening of the note, not from its list items.</summary>
    public static string ClassifyEffect(string lead)
    {
        if (Exclusion.IsMatch(lead)) return "EXCLUSION";
        if (Condition.IsMatch(lead)) return "CONDITION";
        if (Definition.IsMatch(lead)) return "DEFINITION";
        if (Referral.IsMatch(lead)) return "REFERRAL";
        if (Inclusion.IsMatch(lead)) return "INCLUSION";
        return "DEFINITION";
    }
}
