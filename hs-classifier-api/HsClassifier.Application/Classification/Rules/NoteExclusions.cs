using System.Text.RegularExpressions;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Application.Classification.Rules;

/// <summary>
/// Decides whether a legal note (or EN paragraph) takes the goods out of a candidate heading. A clause counts only when
/// (1) the goods' name is the excluded item (not a word in parentheses, which are examples) and (2) the clause sends the
/// goods to a heading or chapter that does not cover the candidate. Anything less is not evidence of an exclusion.
/// </summary>
public static class NoteExclusions
{
    private static readonly Regex Split = new(@";|\r?\n|(?=\([a-z]{1,3}\)\s)|(?=\(\d{1,2}\)\s)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Marker = new(@"^\s*(\([a-z0-9]{1,3}\)|[a-z0-9]{1,3}[.)])\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Parenthetical = new(@"\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex Redirect = new(@"\b(lihat|vide|see|pos|bab|chapter|heading)\b.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ItemSplit = new(@",|\bdan\b|\batau\b|\band\b|\bor\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HeadingRange = new(@"\b(\d{2})\.(\d{2})\s*(?:sampai(?:\s+dengan)?|hingga|s\.?d\.?|to|-|–)\s*(\d{2})\.(\d{2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Heading = new(@"\b(\d{2})\.(\d{2})\b|\b(?:pos|nomor|heading)\s+(\d{2})(\d{2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Chapter = new(@"\b(?:bab|chapter)\s+(\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <returns>The excluding clause (short), or null when this note does not exclude these goods from the candidate.</returns>
    public static string? Find(IEnumerable<string> goodsNames, ExclusionNote note, CandidateHeading candidate)
    {
        var names = goodsNames.Select(Normalize).Where(n => n.Length >= 3).Distinct().ToList();
        if (names.Count == 0) return null;

        foreach (var raw in Split.Split(note.Text))
        {
            var clause = Regex.Replace(raw, @"\s+", " ").Trim();
            if (clause.Length == 0) continue;

            var items = ItemsOf(clause);
            if (!names.Any(n => items.Any(i => IsSameItem(i, n)))) continue;

            var (headings, chapters) = Targets(clause);
            if (headings.Count + chapters.Count == 0) continue;                       // sends the goods nowhere
            if (headings.Contains(candidate.Code) || (candidate.ChapterCode is not null && chapters.Contains(candidate.ChapterCode)))
                continue;                                                              // sends the goods to this candidate
            return clause.Length <= 160 ? clause : clause[..159] + "…";
        }
        return null;
    }

    private static List<string> ItemsOf(string clause)
    {
        var text = clause.Contains(':') ? clause[(clause.LastIndexOf(':') + 1)..] : clause;
        text = Marker.Replace(text, "");
        text = Parenthetical.Replace(text, " ");
        text = Redirect.Replace(text, "");
        return ItemSplit.Split(text).Select(Normalize).Where(i => i.Length > 0).ToList();
    }

    // ponytail: a one-word item ("pompa") only matches the same one word, so a note that lists "pompa" never removes
    // "pompa air" by accident; a multi-word item matches inside a longer name. Upgrade: a curated head-noun check.
    private static bool IsSameItem(string item, string name) =>
        item == name || (item.Contains(' ') && $" {name} ".Contains($" {item} "));

    private static string Normalize(string s) =>
        Regex.Replace(Regex.Replace((s ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]+", " "), @"\s+", " ").Trim();

    private static (HashSet<string> Headings, HashSet<string> Chapters) Targets(string clause)
    {
        var headings = new HashSet<string>();
        foreach (Match m in HeadingRange.Matches(clause))
            if (m.Groups[1].Value == m.Groups[3].Value
                && int.Parse(m.Groups[2].Value) is var a && int.Parse(m.Groups[4].Value) is var b && a <= b)
                for (var h = a; h <= b; h++) headings.Add($"{m.Groups[1].Value}{h:00}");
        foreach (Match m in Heading.Matches(clause))
            headings.Add(m.Groups[1].Success ? m.Groups[1].Value + m.Groups[2].Value : m.Groups[3].Value + m.Groups[4].Value);

        var chapters = Chapter.Matches(clause).Select(m => int.Parse(m.Groups[1].Value).ToString("00")).ToHashSet();
        return (headings, chapters);
    }
}
