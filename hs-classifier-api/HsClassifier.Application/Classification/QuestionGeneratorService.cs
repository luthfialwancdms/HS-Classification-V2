using HsClassifier.Domain.Classification;

namespace HsClassifier.Application.Classification;

/// <summary>
/// Task 6.2: Generates questions ONLY for discriminators actually needed by KUM HS 6
/// to choose between child subheadings of the locked heading.
/// 
/// Key principle: questions are heading-specific, not generic.
/// A laptop (8471) never needs "kapasitas m³" — that's for excavators.
/// </summary>
public class QuestionGeneratorService
{
    // Discriminators that actually matter per heading chapter
    // Key = first 2 digits of heading (chapter), Value = relevant fact types
    private static readonly Dictionary<string, string[]> _chapterDiscriminators = new()
    {
        ["84"] = ["weight", "power_rating", "ckd_skd_status", "capacity"],
        ["85"] = ["voltage", "power_rating", "ckd_skd_status"],
        ["87"] = ["weight", "power_rating", "ckd_skd_status"],        // vehicles: GVW
        ["89"] = ["weight", "capacity"],                               // vessels
        ["90"] = ["primary_use"],
        ["72"] = ["composition", "weight"],                            // steel
        ["73"] = ["weight", "composition"],
        ["74"] = ["composition"],
        ["76"] = ["composition"],
        ["39"] = ["composition", "form"],
    };

    // Human-friendly questions scoped per fact + context
    private static readonly Dictionary<string, string> _questionTemplates = new()
    {
        ["weight"]         = "Berapa berat unit ini? (contoh: 1.4 kg, 500 kg, 21 ton)",
        ["power_rating"]   = "Berapa daya output unit ini? (contoh: 100 kW, 375 kVA, 18 HP)",
        ["voltage"]        = "Berapa tegangan kerja unit ini? (contoh: 220V, 380V, 24V DC)",
        ["capacity"]       = "Berapa kapasitas angkut/bucket? (contoh: 5 ton GVW, 1.2 m³ bucket)",
        ["ckd_skd_status"] = "Apakah unit dikirim CKD (terurai penuh) atau non-CKD (utuh)?",
        ["composition"]    = "Berapa kadar/komposisi material utamanya? (contoh: 80% tembaga, 99.9% aluminium)",
        ["primary_use"]    = "Untuk kegunaan apa barang ini? (industri, medis, rumah tangga, dll)",
        ["form"]           = "Dalam bentuk apa barang ini? (lembaran, batang, serbuk, cair, dll)",
    };

    /// <summary>
    /// Generate questions ONLY for missing discriminators relevant to the given heading.
    /// Returns empty list if heading is unambiguous (e.g. 8471 laptop with no CKD ambiguity).
    /// </summary>
    public List<ClassificationQuestion> GenerateQuestions(
        FactSet facts,
        List<CandidateHeading> candidates)
    {
        // No candidates → no useful questions
        if (candidates.Count == 0) return [];

        var questions = new List<ClassificationQuestion>();

        // Collect discriminators actually needed across retained candidates
        var neededFacts = candidates
            .SelectMany(c => GetRelevantDiscriminators(c))
            .Distinct()
            .ToList();

        foreach (var factType in neededFacts)
        {
            // Skip if answered, not applicable, or captured under an alias / spec text
            if (facts.IsAnswered(factType)) continue;

            var gain = EstimateGain(factType, candidates);
            if (gain < 0.1) continue;

            questions.Add(new ClassificationQuestion
            {
                FactType = factType,
                QuestionText = GetQuestion(factType),
                InformationGain = gain,
                Impact = BuildImpact(factType, candidates),
            });
        }

        return questions.OrderByDescending(q => q.InformationGain).Take(2).ToList();
    }

    private static IEnumerable<string> GetRelevantDiscriminators(CandidateHeading c)
    {
        // Use the heading's explicit required discriminators first
        if (c.RequiredDiscriminators.Count > 0)
            return c.RequiredDiscriminators;

        // Fall back to chapter-level discriminators
        var chapter = c.ChapterCode ?? (c.Code.Length >= 2 ? c.Code[..2] : "84");
        return _chapterDiscriminators.TryGetValue(chapter, out var disc) ? disc : [];
    }

    private static double EstimateGain(string factType, List<CandidateHeading> candidates)
    {
        // Higher gain = ask first
        var baseGain = factType switch
        {
            "ckd_skd_status" => 0.8,   // CKD vs non-CKD is major split in many headings
            "weight"         => 0.7,   // GVW splits vehicles, weight splits machinery
            "power_rating"   => 0.6,
            "voltage"        => 0.6,
            "capacity"       => 0.55,
            "composition"    => 0.5,
            "primary_use"    => 0.45,
            "form"           => 0.4,
            _ => 0.2
        };

        // Boost if multiple candidates affected
        var boost = Math.Min(candidates.Count * 0.05, 0.2);
        return baseGain + boost;
    }

    private static CandidateImpact BuildImpact(string factType, List<CandidateHeading> candidates)
    {
        var half = Math.Max(candidates.Count / 2, 1);
        return new CandidateImpact
        {
            CandidatesAffected = candidates.Count,
            RetainedCodes = candidates.Take(half).Select(c => c.Code).ToList(),
            EliminatedCodes = candidates.Skip(half).Select(c => c.Code).ToList(),
        };
    }

    private static string GetQuestion(string factType)
        => _questionTemplates.TryGetValue(factType, out var q) ? q
           : $"Informasi tentang {factType.Replace("_", " ")}?";
}
