namespace HsClassifier.Application.Services;

public record LineText(string Code, string DescriptionId, string? DescriptionEn);

public record DiffEntry(string Code, string? Before, string? After)
{
    public string Level => Code.Length switch { 4 => "pos", 6 => "grup", 7 => "subpos", _ => "pos tarif" };
    public string Chapter => Code[..2];
}

public class VersionDiffResult
{
    public int BaselineCount { get; init; }
    public int CandidateCount { get; init; }
    public int Unchanged { get; init; }
    /// <summary>In the candidate only.</summary>
    public List<DiffEntry> Added { get; init; } = [];
    /// <summary>In the baseline only.</summary>
    public List<DiffEntry> Removed { get; init; } = [];
    /// <summary>Same code, different Indonesian wording.</summary>
    public List<DiffEntry> Changed { get; init; } = [];
    /// <summary>Same code, the texts differ only by characters an OCR confuses (l / I / 1, 0 / O): a typo fixed or introduced.</summary>
    public List<DiffEntry> OcrOnly { get; init; } = [];
}

/// <summary>
/// Compares two versions of the tariff by code, so an admin sees what activating a new version would change.
/// Differences in spacing, punctuation and case are not changes; l/I/1 and 0/O confusions are reported apart from real rewording.
/// </summary>
public static class VersionDiff
{
    public static VersionDiffResult Compare(IEnumerable<LineText> baseline, IEnumerable<LineText> candidate)
    {
        var before = baseline.GroupBy(l => l.Code).ToDictionary(g => g.Key, g => g.First());
        var after = candidate.GroupBy(l => l.Code).ToDictionary(g => g.Key, g => g.First());

        var added = new List<DiffEntry>(); var removed = new List<DiffEntry>();
        var changed = new List<DiffEntry>(); var ocr = new List<DiffEntry>();
        var unchanged = 0;

        foreach (var (code, a) in after.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!before.TryGetValue(code, out var b)) { added.Add(new(code, null, a.DescriptionId)); continue; }
            var (x, y) = (Key(b.DescriptionId), Key(a.DescriptionId));
            if (x == y) unchanged++;
            else if (Fold(x) == Fold(y)) ocr.Add(new(code, b.DescriptionId, a.DescriptionId));
            else changed.Add(new(code, b.DescriptionId, a.DescriptionId));
        }
        foreach (var (code, b) in before.OrderBy(x => x.Key, StringComparer.Ordinal))
            if (!after.ContainsKey(code)) removed.Add(new(code, b.DescriptionId, null));

        return new VersionDiffResult
        {
            BaselineCount = before.Count, CandidateCount = after.Count, Unchanged = unchanged,
            Added = added, Removed = removed, Changed = changed, OcrOnly = ocr,
        };
    }

    /// <summary>Letters and digits only, lower case: spacing, punctuation and case are not wording.</summary>
    private static string Key(string s) => string.Concat(s.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static string Fold(string key) => key.Replace('l', 'i').Replace('1', 'i').Replace('0', 'o');
}
