using System.Text.RegularExpressions;
using HsClassifier.Application.Interfaces;
using HsClassifier.Application.Classification;
using HsClassifier.Domain.Classification;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Services;

public class CandidateGeneratorService(HsClassifierDbContext db, IVersionManager versionManager)
{
    // ─── Synonym map: generic_name → HS description terms ─────────────────────
    private static readonly Dictionary<string, string[]> _synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["laptop"]       = ["mesin pengolah data otomatis portabel", "portable automatic data processing"],
        ["notebook"]     = ["mesin pengolah data otomatis portabel"],
        ["komputer"]     = ["mesin pengolah data otomatis", "automatic data processing"],
        ["excavator"]    = ["mesin penggali", "shovel loader", "backhoe loader"],
        ["crane"]        = ["derek", "crane and hoist", "overhead travelling crane"],
        ["crane barge"]  = ["crane terapung", "floating crane", "crane dan kapal terapung"],
        ["dumper"]       = ["dumper off-highway", "dumper untuk penggunaan"],
        ["forklift"]     = ["truk forklift", "forklift truck", "reach stacker"],
        ["genset"]       = ["generating set", "generator set dengan motor"],
        ["traktor"]      = ["traktor pertanian", "agricultural tractor", "traktor kehutanan"],
        ["pompa"]        = ["pompa sentrifugal", "centrifugal pump", "pompa untuk"],
        ["kompresor"]    = ["kompresor udara", "air compressor", "pompa udara"],
        ["motor listrik"] = ["motor listrik", "electric motor"],
        ["transformer"]  = ["transformator", "transformer"],
        ["smartphone"]   = ["pesawat telepon", "telephone set"],
        ["kapal"]        = ["kapal pengangkut", "vessel for transport"],
    };

    // ─── Material → chapter mapping (material search scoped to these chapters) ─
    // Only used if generic_name finds nothing. Material "Aluminium" should NOT
    // pull in chapter 26 (ores) — map only to finished-goods chapters.
    private static readonly Dictionary<string, string[]> _materialChapters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aluminium"]  = ["76"],
        ["baja/besi"]  = ["72", "73"],
        ["tembaga"]    = ["74"],
        ["plastik"]    = ["39"],
        ["karet"]      = ["40"],
        ["tekstil"]    = ["50","51","52","53","54","55","61","62","63"],
        ["kimia"]      = ["28","29","38"],
        ["kayu"]       = ["44"],
        ["kertas"]     = ["48"],
    };

    public async Task<List<CandidateHeading>> GenerateCandidatesAsync(
        FactSet facts, CancellationToken ct = default)
    {
        var version = await versionManager.GetActiveVersionAsync(DateTime.UtcNow);
        if (version is null) return [];

        var candidates = new Dictionary<string, CandidateHeading>(StringComparer.OrdinalIgnoreCase);
        // Both names are evidence: "Laptop" (commercial) can hit a tariff line the generic phrase misses
        var names = new[] { facts.GetValue("generic_name"), facts.GetValue("commercial_name") }
            .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var genericName = string.Join(' ', names);
        var material    = (facts.GetValue("material") ?? "").Trim();
        var function    = (facts.GetValue("primary_function") ?? "").Trim();

        // Strategy 1: exact name search (highest priority)
        foreach (var name in names)
            await SearchByText(name, version.Id, candidates, "LEXICAL", score: 1.0, ct);

        // Strategy 2: synonym mapping
        foreach (var name in names)
            if (_synonyms.TryGetValue(name, out var synonymTerms))
                foreach (var term in synonymTerms)
                    await SearchByText(term, version.Id, candidates, "SYNONYM", score: 0.9, ct);

        // Strategy 3: function search (only if still low candidates)
        if (candidates.Count < 3 && !string.IsNullOrWhiteSpace(function))
            await SearchByText(function, version.Id, candidates, "FUNCTION", score: 0.6, ct);

        // Strategy 4: material search — ONLY if generic_name found nothing,
        // AND scoped to the correct finished-goods chapter (not raw material chapters)
        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(material))
        {
            var chapters = GetMaterialChapters(material);
            await SearchByMaterialInChapters(material, chapters, version.Id, candidates, ct);
        }

        // Fallback 1: shorter contiguous phrases of each name, longest first ("alat cukur listrik" → "alat cukur").
        // A phrase hit is much stronger evidence than scattered single words.
        if (candidates.Count == 0)
            foreach (var phrase in names.SelectMany(SubPhrases).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await SearchByText(phrase, version.Id, candidates, "PHRASE", score: 0.7, ct);
                if (candidates.Count > 0) break;
            }

        // Fallback 2: rank headings by how many distinct name words they contain.
        // (Previously each word added its first 20 hits, so one common word like "data" pulled in random headings.)
        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(genericName))
            await SearchByWordOverlap(genericName, version.Id, candidates, ct);

        // KUM HS 1 scores every candidate against its official text, so a longer list costs only queries.
        // At 10, the lexical hits of "motor listrik" (8467, 8509, 8702...) filled the list once the PDF version added
        // the missing headings, and the synonym hit 8501 (score 0.9, the right heading) was cut.
        var result = candidates.Values
            .OrderByDescending(c => c.RelevanceScore)
            .Take(25)
            .ToList();

        foreach (var c in result)
            await EnrichAsync(c, version.Id, ct);

        return result;
    }

    /// <summary>
    /// Section XVI Note 2(b): the BTKI "parts" headings whose text covers the parent machine's heading
    /// (e.g. 8431 "Bagian … mesin dari pos 84.25 sampai dengan 84.30" for an excavator in 8429).
    /// </summary>
    /// <param name="accessory">Keep only headings that also name accessories ("Bagian dan aksesori …").</param>
    public async Task<List<CandidateHeading>> PartsHeadingsForAsync(string parentHeading, bool accessory = false, CancellationToken ct = default)
    {
        var version = await versionManager.GetActiveVersionAsync(DateTime.UtcNow);
        if (version is null) return [];
        // Parts headings are in the same Section; the 2-digit chapter range keeps the scan small
        var chapter = parentHeading[..2];
        var rows = await db.BtkiTariffLines
            .Where(t => t.VersionId == version.Id && t.IsActive && t.Code.Length == 4 && t.ChapterCode == chapter)
            .Select(t => new { t.Code, t.DescriptionId, t.ChapterCode, t.SourceId })
            .ToListAsync(ct);

        var result = rows.DistinctBy(r => r.Code)
            .Where(r => PartsHeadingRef.Covers(r.DescriptionId, parentHeading))
            .Where(r => !accessory || r.DescriptionId.Contains("aksesori", StringComparison.OrdinalIgnoreCase))
            .Select(r => new CandidateHeading
            {
                Code = r.Code, Description = r.DescriptionId, ChapterCode = r.ChapterCode,
                GenerationSource = "PARTS_NOTE", RelevanceScore = 1.0, SourceId = r.SourceId,
            }).ToList();
        foreach (var c in result) await EnrichAsync(c, version.Id, ct);
        return result;
    }

    /// <summary>
    /// Waste and scrap: active headings whose text names scrap ("skrap") AND one of the material words
    /// (e.g. "besi/baja" → 7204, "elektronik" → 8549). A generic text search would pull every scrap heading.
    /// </summary>
    public async Task<List<CandidateHeading>> ScrapHeadingsAsync(string material, CancellationToken ct = default)
    {
        var version = await versionManager.GetActiveVersionAsync(DateTime.UtcNow);
        if (version is null) return [];
        string[] stop = ["dan", "dari", "atau", "yang", "lain", "lainnya", "sisa", "skrap", "bekas"];
        var words = Regex.Split(material.ToLowerInvariant(), @"[^\p{L}]+").Where(w => w.Length >= 3 && !stop.Contains(w)).ToList();
        if (words.Count == 0) return [];
        var rows = await db.BtkiTariffLines
            .Where(t => t.VersionId == version.Id && t.IsActive && t.Code.Length == 4 && t.DescriptionId.Contains("skrap"))
            .Select(t => new { t.Code, t.DescriptionId, t.ChapterCode, t.SourceId })
            .ToListAsync(ct);

        var result = rows.DistinctBy(r => r.Code)
            .Where(r => Regex.Split(r.DescriptionId.ToLowerInvariant(), @"[^\p{L}]+").Intersect(words).Any())
            .Select(r => new CandidateHeading
            {
                Code = r.Code, Description = r.DescriptionId, ChapterCode = r.ChapterCode,
                GenerationSource = "SCRAP_NOTE", RelevanceScore = 1.0, SourceId = r.SourceId,
            }).ToList();
        foreach (var c in result) await EnrichAsync(c, version.Id, ct);
        return result;
    }

    /// <summary>
    /// Candidates from an AI term suggestion. Suggested headings are kept only if they exist as an
    /// active 4-digit row in the active version; terms go through the normal DB text search.
    /// </summary>
    public async Task<(List<CandidateHeading> Candidates, List<string> Accepted, List<string> Rejected)>
        FromSuggestionAsync(TermSuggestion s, CancellationToken ct = default)
    {
        var version = await versionManager.GetActiveVersionAsync(DateTime.UtcNow);
        if (version is null) return ([], [], s.Headings);

        var rows = await db.BtkiTariffLines
            .Where(t => t.VersionId == version.Id && t.IsActive && t.Code.Length == 4 && s.Headings.Contains(t.Code))
            .Select(t => new { t.Code, t.DescriptionId, t.ChapterCode, t.SourceId })
            .ToListAsync(ct);

        var dict = rows.DistinctBy(r => r.Code).ToDictionary(r => r.Code, r => new CandidateHeading
        {
            Code = r.Code, Description = r.DescriptionId, ChapterCode = r.ChapterCode,
            GenerationSource = "AI_SUGGESTION", RelevanceScore = 0.5, SourceId = r.SourceId,
        }, StringComparer.OrdinalIgnoreCase);
        var accepted = dict.Keys.ToList();

        foreach (var term in s.Terms)
            await SearchByText(term, version.Id, dict, "AI_SUGGESTION", score: 0.5, ct);

        var result = dict.Values.Take(10).ToList();
        foreach (var c in result)
            await EnrichAsync(c, version.Id, ct);

        return (result, accepted, s.Headings.Except(accepted).ToList());
    }

    // ─── Search helpers ────────────────────────────────────────────────────────

    /// <summary>Contiguous sub-phrases of 2+ words, longest first, excluding the full name (already searched).</summary>
    public static IEnumerable<string> SubPhrases(string name)
    {
        var w = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int n = w.Length - 1; n >= 2; n--)
            for (int i = 0; i + n <= w.Length; i++)
                yield return string.Join(' ', w[i..(i + n)]);
    }

    private async Task SearchByText(string term, Guid versionId,
        Dictionary<string, CandidateHeading> dict, string source, double score, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(term)) return;

        var matches = await db.BtkiTariffLines
            .Where(t => t.VersionId == versionId && t.IsActive
                && (t.DescriptionId.Contains(term)
                    || (t.DescriptionEn != null && t.DescriptionEn.Contains(term))))
            .Select(t => new { t.HeadingCode, t.DescriptionId, t.ChapterCode, t.Code, t.SourceId })
            // A heading whose own text matches is far stronger evidence than a line that merely
            // mentions the term ("...for use with automatic data processing machines"), so headings
            // first, then subheadings, then lines. Without an order, Take(20) dropped 8471 itself.
            .OrderBy(t => t.Code.Length).ThenBy(t => t.Code)
            .Take(20)
            .ToListAsync(ct);

        foreach (var m in matches)
        {
            var heading = m.HeadingCode ?? m.Code.Substring(0, 4);
            // Only add if not already present (first match wins for same heading)
            if (!dict.ContainsKey(heading))
                dict[heading] = new CandidateHeading
                {
                    Code = heading, Description = m.DescriptionId,
                    ChapterCode = m.ChapterCode, GenerationSource = source,
                    RelevanceScore = score,
                    SourceId = m.SourceId,
                };
        }
    }

    /// <summary>
    /// Keeps only headings matching the most distinct words (min 2, or all words for a 1-word name).
    /// ponytail: substring LIKE per word, one query per word; fine for short names. Upgrade: full-text index.
    /// </summary>
    private async Task SearchByWordOverlap(string text, Guid versionId,
        Dictionary<string, CandidateHeading> dict, CancellationToken ct)
    {
        var words = text.ToLowerInvariant().Split([' ', ',', '/', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4).Distinct().Take(8).ToList();
        if (words.Count == 0) return;

        var hits = new Dictionary<string, int>();
        foreach (var w in words)
        {
            var headings = await db.BtkiTariffLines
                .Where(t => t.VersionId == versionId && t.IsActive && t.HeadingCode != null
                    && (t.DescriptionId.Contains(w) || (t.DescriptionEn != null && t.DescriptionEn.Contains(w))))
                .Select(t => t.HeadingCode!).Distinct().ToListAsync(ct);
            foreach (var h in headings) hits[h] = hits.GetValueOrDefault(h) + 1;
        }
        if (hits.Count == 0) return;

        var best = hits.Values.Max();
        if (best < Math.Min(2, words.Count)) return; // a single shared word is not evidence

        foreach (var heading in hits.Where(h => h.Value == best).Select(h => h.Key).Take(10))
        {
            // Representative line = the one in this heading matching the most words
            var lines = await db.BtkiTariffLines
                .Where(t => t.VersionId == versionId && t.IsActive && t.HeadingCode == heading)
                .Select(t => new { t.DescriptionId, t.DescriptionEn, t.ChapterCode, t.SourceId })
                .ToListAsync(ct);
            var rep = lines.OrderByDescending(l => words.Count(w =>
                $"{l.DescriptionId} {l.DescriptionEn}".Contains(w, StringComparison.OrdinalIgnoreCase))).First();
            dict[heading] = new CandidateHeading
            {
                Code = heading, Description = rep.DescriptionId, ChapterCode = rep.ChapterCode,
                GenerationSource = "PARTIAL", RelevanceScore = 0.4 * best / words.Count, SourceId = rep.SourceId,
            };
        }
    }

    private async Task SearchByMaterialInChapters(string material, string[] chapters,
        Guid versionId, Dictionary<string, CandidateHeading> dict, CancellationToken ct)
    {
        if (chapters.Length == 0) return;

        var matches = await db.BtkiTariffLines
            .Where(t => t.VersionId == versionId && t.IsActive
                && chapters.Contains(t.ChapterCode)
                && (t.DescriptionId.Contains(material)
                    || (t.DescriptionEn != null && t.DescriptionEn.Contains(material))))
            .Select(t => new { t.HeadingCode, t.DescriptionId, t.ChapterCode, t.Code, t.SourceId })
            .Take(10)
            .ToListAsync(ct);

        foreach (var m in matches)
        {
            var heading = m.HeadingCode ?? m.Code.Substring(0, 4);
            if (!dict.ContainsKey(heading))
                dict[heading] = new CandidateHeading
                {
                    Code = heading, Description = m.DescriptionId,
                    ChapterCode = m.ChapterCode, GenerationSource = "MATERIAL",
                    RelevanceScore = 0.5, SourceId = m.SourceId,
                };
        }
    }

    private static string[] GetMaterialChapters(string material)
    {
        foreach (var (key, chapters) in _materialChapters)
            if (material.Contains(key, StringComparison.OrdinalIgnoreCase))
                return chapters;
        return [];
    }

    // ─── Enrichment ────────────────────────────────────────────────────────────

    private async Task EnrichAsync(CandidateHeading c, Guid versionId, CancellationToken ct)
    {
        if (c.ChapterCode is not null)
            c.ChapterNotesText = await db.ChapterNotes
                .Where(n => n.VersionId == versionId && n.ChapterNumber == c.ChapterCode)
                .Select(n => "[" + n.EffectType + "] " + n.TextId)
                .Take(5).ToListAsync(ct);

        var enRaw = await db.ExplanatoryNotes
            .Where(n => n.VersionId == versionId && n.HeadingCode == c.Code)
            .OrderBy(n => n.Id)
            .Select(n => new { n.ParagraphType, n.TextContent })
            .Take(10).ToListAsync(ct);

        c.EnParagraphs = enRaw
            .Select(n => "[" + n.ParagraphType + "] " + n.TextContent.Substring(0, Math.Min(200, n.TextContent.Length)))
            .ToList();

        // Whole hierarchy of the heading: 4-digit heading text, 6-digit subheadings, 8-digit lines.
        // ponytail: capped at 150 rows per heading; raise if a heading has more nodes.
        var nodes = await db.BtkiTariffLines
            .Where(t => t.VersionId == versionId && t.IsActive && t.HeadingCode == c.Code)
            .OrderBy(t => t.Code)
            .Select(t => new { t.Code, t.DescriptionId, t.DescriptionEn, t.SourceId })
            .Take(150).ToListAsync(ct);
        // Source CSV contains duplicate rows for some codes (e.g. 8504.50.20); first one wins
        nodes = nodes.DistinctBy(n => n.Code).ToList();
        static string Text(string id, string? en) => string.IsNullOrWhiteSpace(en) ? id : $"{id} / {en}";

        // Official heading text (KUM HS 1 compares against this, not against a random child line)
        if (nodes.FirstOrDefault(n => n.Code.Length == 4) is { } head)
        {
            c.Description = Text(head.DescriptionId, head.DescriptionEn);
            c.SourceId = head.SourceId;
            c.HasHeadingText = true;
        }

        // Only 8-digit tariff lines (XXXX.XX.XX) can be a final code. Each carries the text of its
        // one-dash group and 6-digit subheading, since "- - Lain-lain" only makes sense under its parent.
        c.ParentDescriptions = nodes.Where(n => n.Code.Length is 6 or 7)
            .ToDictionary(n => n.Code, n => Text(n.DescriptionId, n.DescriptionEn));
        var children = nodes.Where(n => n.Code.Length == 10).ToList();
        c.ChildSubheadings = children.Select(t => t.Code).ToList();
        c.ChildDescriptions = children.ToDictionary(t => t.Code, t => string.Join(" | ",
            new[] { t.Code[..6], t.Code[..7] }
                .Select(p => c.ParentDescriptions.GetValueOrDefault(p))
                .Append(Text(t.DescriptionId, t.DescriptionEn))
                .Where(s => !string.IsNullOrEmpty(s)).Distinct()));

        // Discriminators are no longer guessed per chapter: KUM HS 6 asks directly from the
        // official texts of the subheadings it is comparing (RulesEngine.BuildSubheadingQuestion).
        c.RequiredDiscriminators = [];
    }
}
