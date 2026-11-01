using System.Globalization;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Application.Classification.Rules;

/// <summary>
/// KUM HS 3(b) settings. Weights are the operational weights from WORKFLOW_END_TO_END (function 30, material 15,
/// bulk 15, value 15, necessity 15, consumer perception 10). <c>MinMargin</c> (points) and <c>MinFactorWins</c>
/// are engine choices, not from the documents: the leader must beat the runner-up by that margin and win at least
/// that many factors, so no single factor decides alone.
/// </summary>
public record EssentialCharacterOptions(IReadOnlyDictionary<string, double> Weights, double MinMargin, int MinFactorWins)
{
    public static readonly EssentialCharacterOptions Default = new(
        new Dictionary<string, double>
        {
            ["function"] = 30, ["material"] = 15, ["bulk"] = 15, ["value"] = 15, ["necessity"] = 15, ["perception"] = 10,
        }, 20, 2);
}

public record EssentialCharacterOutcome(List<ClassificationQuestion> Questions, CandidateHeading? Winner, ReasoningStep? Step);

/// <summary>
/// KUM HS 3(b) essential character between headings that KUM HS 3(a) could not separate, for mixtures and retail
/// sets. The evidence per factor is the user's answer (which tied heading's component dominates, or "equal"); the
/// engine only adds up the weights. It never infers a factor from the goods' text and never lets one factor decide.
/// </summary>
public static class EssentialCharacter
{
    public const string Equal = "EQUAL";

    public static readonly (string Key, string Label)[] Factors =
    [
        ("function", "Fungsi / peran komponen"), ("material", "Material"), ("bulk", "Bulk / jumlah"),
        ("value", "Nilai"), ("necessity", "Kebutuhan terhadap penggunaan barang"), ("perception", "Persepsi konsumen"),
    ];

    public static string FactType(string key) => "ec_" + key;

    /// <summary>3(b) applies to mixtures and retail sets (master prompt KUM HS 3(b) trigger), not to any tie.</summary>
    public static bool IsComposite(FactSet f)
    {
        var mix = (f.GetValue("mixture_composite") ?? "").ToLowerInvariant();
        return mix.Contains("mixture") || mix.Contains("campuran") || mix.Contains("composite")
            || f.GetValue("set_kit") == "retail_set";
    }

    public static EssentialCharacterOutcome Evaluate(FactSet facts, List<CandidateHeading> tied,
        EssentialCharacterOptions options, Func<CandidateHeading, string> label)
    {
        var codes = tied.Select(c => c.Code).ToList();
        string? Answer(string key)
        {
            var v = facts.GetValue(FactType(key))?.Trim();
            if (v is null) return null;
            if (v.Equals(Equal, StringComparison.OrdinalIgnoreCase)) return Equal;
            return codes.Contains(v) ? v : null;   // an answer naming some other heading counts as unanswered
        }
        double Weight(string key) => options.Weights.GetValueOrDefault(key);
        var sources = tied.Select(c => c.SourceId ?? c.Code).ToList();

        // Ask every factor that has no valid answer, all in one round
        var questions = Factors.Where(f => Answer(f.Key) is null).Select(f => new ClassificationQuestion
        {
            FactType = FactType(f.Key),
            QuestionText = $"Karakter pokok (KUM HS 3(b)): dari sisi {f.Label.ToLowerInvariant()}, komponen atau pos mana yang paling berperan pada barang Anda?",
            Choices = [.. tied.Select(c => new QuestionChoice(c.Code, label(c))),
                       new QuestionChoice(Equal, "Setara / tidak dapat dibedakan")],
            InformationGain = 1.0,
            Impact = new CandidateImpact { CandidatesAffected = tied.Count, RetainedCodes = codes },
        }).ToList();
        if (questions.Count > 0) return new(questions, null, null);

        var score = codes.ToDictionary(c => c, _ => 0.0);
        var wins = codes.ToDictionary(c => c, _ => 0);
        var lines = new List<string>();
        foreach (var (key, name) in Factors)
        {
            var a = Answer(key)!;
            if (a == Equal)
            {
                foreach (var c in codes) score[c] += Weight(key) / codes.Count;
                lines.Add($"{name} (bobot {N(Weight(key))}): setara");
            }
            else
            {
                score[a] += Weight(key);
                wins[a]++;
                lines.Add($"{name} (bobot {N(Weight(key))}): {a}");
            }
        }

        var ranked = codes.OrderByDescending(c => score[c]).ToList();
        var margin = score[ranked[0]] - score[ranked[1]];
        var decided = margin >= options.MinMargin && wins[ranked[0]] >= options.MinFactorWins;
        var winner = decided ? tied.First(c => c.Code == ranked[0]) : null;

        var summary = $"Skor: {string.Join(", ", ranked.Select(c => $"{c} = {N(score[c])}"))}. " +
                      $"Selisih {N(margin)} (minimal {N(options.MinMargin)}); {ranked[0]} menang di {wins[ranked[0]]} faktor (minimal {options.MinFactorWins}).";
        var step = new ReasoningStep
        {
            Rule = "KUM_HS_3B",
            CandidateCode = winner?.Code ?? "",
            Decision = decided ? CandidateDecision.Retained : CandidateDecision.Unresolved,
            Reason = $"Karakter pokok dinilai dari enam faktor berbobot. {string.Join("; ", lines)}. {summary} " +
                     (decided ? $"Karakter pokok ada pada pos {winner!.Code}."
                              : "Karakter pokok tidak dapat ditentukan; dilanjutkan ke KUM HS 3(c)."),
            SourceIds = sources,
        };
        return new([], winner, step);
    }

    private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
