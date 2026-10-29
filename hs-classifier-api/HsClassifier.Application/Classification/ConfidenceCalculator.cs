using HsClassifier.Domain.Classification;

namespace HsClassifier.Application.Classification;

/// <summary>Inputs for the confidence model; each ratio is 0..1.</summary>
public record ConfidenceInput(
    bool DecisiveFactsComplete,
    double HeadingMatch,
    bool LegalNotesTested,
    bool EnReviewed,
    double SubheadingCertainty,
    double EliminationStrength,
    bool CandidateAmbiguityUnresolved,
    bool Gri4Used,
    bool Gri3cUsed,
    bool AiTermSuggestion);

public record ConfidenceComponent(string Name, double Weight, double Ratio)
{
    public double Points => Weight * Math.Clamp(Ratio, 0, 1);
}

public record ConfidenceReport(int Score, IReadOnlyList<ConfidenceComponent> Components,
    IReadOnlyList<string> CapsApplied, IReadOnlyList<string> RiskFlags);

/// <summary>
/// Confidence model from the V2 product logic document ("CONFIDENCE DAN CLASSIFICATION RISK"):
/// weighted components 25/20/15/15/15/10, then caps (lowest wins), plus risk flags.
/// Pure function — no data access — so the rules can be tested exactly.
/// </summary>
public static class ConfidenceCalculator
{
    public const string MissingDecisiveFact = "MISSING_DECISIVE_FACT";
    public const string LegalNotesNotReviewed = "LEGAL_NOTES_NOT_REVIEWED";
    public const string EnNotReviewed = "EXPLANATORY_NOTES_NOT_REVIEWED";
    public const string MultipleEqualCandidates = "MULTIPLE_EQUAL_CANDIDATES";
    public const string Gri4Flag = "GRI_4_USED";
    public const string AiTermFlag = "AI_TERM_SUGGESTION";

    public static ConfidenceReport Compute(ConfidenceInput i)
    {
        var components = new List<ConfidenceComponent>
        {
            new("Kelengkapan fakta penentu", 25, i.DecisiveFactsComplete ? 1 : 0),
            new("Kecocokan uraian Pos", 20, i.HeadingMatch),
            new("Dukungan Catatan Hukum", 15, i.LegalNotesTested ? 1 : 0),
            new("Kecocokan Explanatory Notes", 15, i.EnReviewed ? 1 : 0),
            new("Kepastian subpos 6/8 digit", 15, i.SubheadingCertainty),
            new("Kekuatan eliminasi kandidat", 10, i.EliminationStrength),
        };
        var score = components.Sum(c => c.Points);

        var caps = new List<(double Max, string Why)>();
        var flags = new List<string>();
        if (!i.DecisiveFactsComplete) { caps.Add((69, "Fakta penentu belum lengkap → maks 69")); flags.Add(MissingDecisiveFact); }
        if (!i.LegalNotesTested)      { caps.Add((60, "Catatan Hukum belum diuji → maks 60")); flags.Add(LegalNotesNotReviewed); }
        if (!i.EnReviewed)            { caps.Add((75, "Explanatory Notes belum tersedia → maks 75")); flags.Add(EnNotReviewed); }
        if (i.CandidateAmbiguityUnresolved) { caps.Add((59, "Ambiguitas kandidat belum selesai → maks 59")); flags.Add(MultipleEqualCandidates); }
        if (i.Gri3cUsed && !flags.Contains(MultipleEqualCandidates)) flags.Add(MultipleEqualCandidates);
        if (i.Gri4Used) flags.Add(Gri4Flag);
        if (i.AiTermSuggestion) flags.Add(AiTermFlag);

        var cap = caps.Count > 0 ? caps.Min(c => c.Max) : 100;
        var final = (int)Math.Round(Math.Min(score, cap), MidpointRounding.AwayFromZero);
        return new ConfidenceReport(final, components,
            caps.Where(c => c.Max < score).Select(c => c.Why).ToList(), flags);
    }

    /// <summary>Bands for the existing High/Medium/Low label.</summary>
    public static ConfidenceLevel Level(int score) =>
        score >= 80 ? ConfidenceLevel.High : score >= 60 ? ConfidenceLevel.Medium : ConfidenceLevel.Low;
}
