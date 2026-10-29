using System.Globalization;
using System.Text.RegularExpressions;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Application.Classification;

public enum Dimension { Mass, Power, ApparentPower, Volume, Length, Voltage }

/// <summary>A bound on one dimension, in base units (kg, W, VA, ml, mm, V).</summary>
public record Constraint(Dimension Dimension, double? Lower, bool LowerInclusive, double? Upper, bool UpperInclusive, string Unit)
{
    public bool Satisfies(double v) =>
        (Lower is null || (LowerInclusive ? v >= Lower : v > Lower)) &&
        (Upper is null || (UpperInclusive ? v <= Upper : v < Upper));
}

public record Quantity(Dimension Dimension, double Value, string Text);

public record NarrowResult(List<string> Pool, List<ReasoningStep> Steps);

/// <summary>
/// KUM HS 6 step 9 ("uji threshold pembeda"): reads numeric bounds from official subheading texts and
/// compares them with the goods' stated values. Conservative by design — an unreadable number, a
/// disjunctive text ("… kW atau … cc") or a missing value never eliminates a branch.
/// </summary>
public static class Thresholds
{
    private static readonly Dictionary<string, (Dimension Dim, double Factor)> Units = new(StringComparer.Ordinal)
    {
        ["g"] = (Dimension.Mass, 0.001), ["kg"] = (Dimension.Mass, 1), ["t"] = (Dimension.Mass, 1000),
        ["ton"] = (Dimension.Mass, 1000), ["tonne"] = (Dimension.Mass, 1000),
        ["W"] = (Dimension.Power, 1), ["kW"] = (Dimension.Power, 1000), ["MW"] = (Dimension.Power, 1e6),
        ["HP"] = (Dimension.Power, 745.7), ["hp"] = (Dimension.Power, 745.7), ["PK"] = (Dimension.Power, 735.5),
        ["VA"] = (Dimension.ApparentPower, 1), ["kVA"] = (Dimension.ApparentPower, 1000), ["kVa"] = (Dimension.ApparentPower, 1000),
        ["MVA"] = (Dimension.ApparentPower, 1e6),
        ["ml"] = (Dimension.Volume, 1), ["cc"] = (Dimension.Volume, 1), ["cm3"] = (Dimension.Volume, 1),
        ["l"] = (Dimension.Volume, 1000), ["L"] = (Dimension.Volume, 1000), ["liter"] = (Dimension.Volume, 1000),
        // ponytail: OCR renders "l" as capital "I" in BTKI capacity texts ("230 I"); accepted only as a unit token.
        ["I"] = (Dimension.Volume, 1000), ["m3"] = (Dimension.Volume, 1e6),
        ["mm"] = (Dimension.Length, 1), ["cm"] = (Dimension.Length, 10), ["m"] = (Dimension.Length, 1000),
        ["V"] = (Dimension.Voltage, 1), ["kV"] = (Dimension.Voltage, 1000),
    };

    // Longest units first so "kVA" is not read as "kV"
    private static readonly string UnitAlt = string.Join("|", Units.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape));
    // number then unit; the unit must not continue into a word or a rate ("m3/menit")
    private const string Num = @"(\d+(?:[.,]\d+)*)";
    private static readonly string Qty = Num + @"\s*(" + UnitAlt + @")(?![\p{L}\d/])";

    private static readonly Regex Between = new(@"(?<!tidak\s)melebihi\s+" + Qty + @"\s+tetapi\s+tidak\s+melebihi\s+" + Qty, RegexOptions.IgnoreCase);
    private static readonly Regex AtMost = new(@"(?:tidak\s+melebihi|tidak\s+lebih\s+dari|maksimum|maksimal)\s+" + Qty, RegexOptions.IgnoreCase);
    private static readonly Regex Above = new(@"(?<!tidak\s)(?:melebihi|lebih\s+dari)\s+" + Qty, RegexOptions.IgnoreCase);
    private static readonly Regex Below = new(@"(?<!tidak\s)kurang\s+dari\s+" + Qty, RegexOptions.IgnoreCase);
    private static readonly Regex AtLeast = new(@"(?:tidak\s+kurang\s+dari|minimal|minimum)\s+" + Qty, RegexOptions.IgnoreCase);
    private static readonly Regex OrMore = new(Qty + @"\s+atau\s+lebih", RegexOptions.IgnoreCase);
    private static readonly Regex AnyQty = new(@"(?<![\d.,])" + Qty, RegexOptions.None);

    /// <summary>"21,10" and "21.10" are decimals; "1.000" (separator + exactly 3 digits, last group) is thousands.</summary>
    public static double? Number(string s)
    {
        if (Regex.IsMatch(s, @"^\d{1,3}([.,]\d{3})+$") && !Regex.IsMatch(s, @"^\d{1,3}[.,]\d{3}$|^0"))
            return double.Parse(Regex.Replace(s, "[.,]", ""), CultureInfo.InvariantCulture);
        if (Regex.IsMatch(s, @"^\d{1,3}[.,]\d{3}$") && !s.StartsWith('0'))
            return double.Parse(Regex.Replace(s, "[.,]", ""), CultureInfo.InvariantCulture);
        var parts = s.Split('.', ',');
        if (parts.Length > 2) return null;
        return double.TryParse(parts.Length == 2 ? $"{parts[0]}.{parts[1]}" : parts[0],
            NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static (Dimension Dim, double Value)? Base(string number, string unit) =>
        Number(number) is { } n && Units.TryGetValue(unit, out var u) ? (u.Dim, n * u.Factor) : null;

    /// <summary>Leading "- - -" indent dashes of a BTKI text.</summary>
    public static int Dashes(string text) => Regex.Match(text, @"^\s*((?:[-•]\s*)*)").Groups[1].Value.Count(c => c is '-' or '•');

    /// <summary>True when two bounds on the same dimension admit at least one common value.</summary>
    public static bool Overlaps(Constraint a, Constraint b)
    {
        var lo = Math.Max(a.Lower ?? double.NegativeInfinity, b.Lower ?? double.NegativeInfinity);
        var hi = Math.Min(a.Upper ?? double.PositiveInfinity, b.Upper ?? double.PositiveInfinity);
        if (lo < hi) return true;
        return lo == hi && a.Satisfies(lo) && b.Satisfies(lo);
    }

    /// <summary>All bounds stated in a subheading text, at most one per dimension; texts mixing dimensions with "atau" yield nothing.</summary>
    public static List<Constraint> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var found = new List<Constraint>();
        var rest = text;

        foreach (Match m in Between.Matches(rest))
            if (Base(m.Groups[1].Value, m.Groups[2].Value) is { } lo && Base(m.Groups[3].Value, m.Groups[4].Value) is { } hi && lo.Dim == hi.Dim)
            {
                found.Add(new(lo.Dim, lo.Value, false, hi.Value, true, m.Groups[4].Value));
                rest = rest.Replace(m.Value, " ");
            }

        void One(Regex r, bool upper, bool inclusive)
        {
            foreach (Match m in r.Matches(rest))
                if (Base(m.Groups[1].Value, m.Groups[2].Value) is { } b)
                {
                    found.Add(upper ? new(b.Dim, null, false, b.Value, inclusive, m.Groups[2].Value)
                                    : new(b.Dim, b.Value, inclusive, null, false, m.Groups[2].Value));
                    rest = rest.Replace(m.Value, " ");
                }
        }
        One(AtMost, upper: true, inclusive: true);
        One(AtLeast, upper: false, inclusive: true);
        One(Below, upper: true, inclusive: false);
        One(OrMore, upper: false, inclusive: true);
        One(Above, upper: false, inclusive: false);

        // Two dimensions joined by "atau" are alternatives, not a conjunction — can't evaluate safely
        if (found.Select(c => c.Dimension).Distinct().Count() > 1 && Regex.IsMatch(text, @"\batau\b", RegexOptions.IgnoreCase))
            return [];
        return found.GroupBy(c => c.Dimension).Where(g => g.Count() == 1).Select(g => g.Single()).ToList();
    }

    /// <summary>Quantities written by the user ("21 ton", "60 kW", "Motor 3 phase, 500 W, 24V").</summary>
    public static List<Quantity> Quantities(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return AnyQty.Matches(text)
            .Select(m => Base(m.Groups[1].Value, m.Groups[2].Value) is { } b ? new Quantity(b.Dim, b.Value, m.Value) : null)
            .OfType<Quantity>().ToList();
    }

    private static readonly Dictionary<Dimension, (string Fact, string Question)> Ask = new()
    {
        [Dimension.Mass]          = ("weight", "Berapa berat barang? (mis. 1,4 kg atau 21 ton)"),
        [Dimension.Power]         = ("power_rating", "Berapa daya keluaran barang? Sertakan satuan (mis. 750 W, 60 kW, 18 HP)."),
        [Dimension.ApparentPower] = ("power_rating", "Berapa daya keluaran barang dalam kVA? (mis. 12,5 kVA)"),
        [Dimension.Volume]        = ("capacity", "Berapa kapasitas/volume barang? Sertakan satuan (mis. 230 liter, 150 cc)."),
        [Dimension.Length]        = ("dimensions", "Berapa ukuran yang dimaksud? Sertakan satuan (mis. 120 cm)."),
        [Dimension.Voltage]       = ("voltage", "Berapa tegangan kerja barang? (mis. 220 V, 20 kV)"),
    };

    public static Quantity? ValueOf(FactSet facts, Dimension dim) => ValueFor(facts, dim).Value;

    /// <summary>
    /// The user's value for a dimension: the dedicated fact, then the bulk net weight/volume,
    /// then the specification text. Null if absent or ambiguous.
    /// </summary>
    private static (Quantity? Value, bool Answered) ValueFor(FactSet facts, Dimension dim)
    {
        var fact = facts.GetValue(Ask[dim].Fact);
        var fromFact = Quantities(fact).Where(q => q.Dimension == dim).ToList();
        if (fromFact.Count == 1) return (fromFact[0], true);
        var fromBulk = Quantities(facts.GetValue("bulk_net_quantity")).Where(q => q.Dimension == dim).ToList();
        if (fromBulk.Count == 1) return (fromBulk[0], true);
        var fromSpec = Quantities(facts.GetValue("supporting_documents")).Where(q => q.Dimension == dim)
            .DistinctBy(q => q.Value).ToList();
        if (fromSpec.Count == 1) return (fromSpec[0], true);
        // A written answer we cannot read (e.g. "tidak tahu") still counts as answered: don't ask again
        return (null, !string.IsNullOrWhiteSpace(fact));
    }

    /// <summary>
    /// Narrow same-level branches by numeric bounds, level by level. Returns a question when a level is
    /// split by a dimension whose value the user has not given yet.
    /// </summary>
    public static NarrowResult Narrow(FactSet facts, CandidateHeading h, List<string> pool, out ClassificationQuestion? question,
        Func<string, string>? labelOf = null)
    {
        question = null;
        var steps = new List<ReasoningStep>();
        labelOf ??= code => h.ChildDescriptions.GetValueOrDefault(code)?.Split(" | ").Last() ?? "";

        while (pool.Count > 1)
        {
            var level = new[] { 6, 7, 10 }.First(l => pool.Select(c => c[..l]).Distinct().Count() > 1);
            var branches = pool.Select(c => c[..level]).Distinct().ToList();
            var linesOf = branches.ToDictionary(b => b, b => pool.Where(c => c.StartsWith(b)).ToList());
            string Text(string p) => level < 10
                ? h.ParentDescriptions.GetValueOrDefault(p) ?? (linesOf[p].Count == 1 ? labelOf(linesOf[p][0]) : "")
                : labelOf(p);
            var constraints = branches.ToDictionary(b => b, b => level < 10 ? GroupConstraints(b) : Parse(labelOf(b)));

            // A branch's bound: its own text; with no own text, its single tariff line. A group bound that is
            // disjoint from one of its own lines' bounds is an OCR/source error and is not trusted.
            List<Constraint> GroupConstraints(string p)
            {
                if (!h.ParentDescriptions.TryGetValue(p, out var own))
                    return linesOf[p].Count == 1 ? Parse(labelOf(linesOf[p][0])) : [];
                // A one-dash group ("8501.5") is written with one dash, a 6-digit subheading with at most two.
                // More dashes means the source row carries a deeper line's text (OCR misplacement) — not its bound.
                if (Dashes(own) > (p.Length == 6 ? 1 : 2)) return [];
                return Parse(own).Where(g => linesOf[p].All(l =>
                    Parse(labelOf(l)).FirstOrDefault(x => x.Dimension == g.Dimension) is not { } lc || Overlaps(g, lc))).ToList();
            }

            // A known value may test any bounded dimension; a question is only worth asking when the
            // dimension really splits the level (at least two branches bound it).
            var bounded = constraints.Values.SelectMany(c => c).GroupBy(c => c.Dimension)
                .ToDictionary(g => g.Key, g => g.Count());
            Dimension? dim = null;
            Quantity? value = null;
            foreach (var d in bounded.Keys)
                if (ValueFor(facts, d).Value is { } v) { dim = d; value = v; break; }

            if (value is null)
            {
                var split = bounded.Where(b => b.Value >= 2).Select(b => (Dimension?)b.Key).FirstOrDefault();
                if (split is not null && !ValueFor(facts, split.Value).Answered)
                    question = new ClassificationQuestion
                    {
                        FactType = Ask[split.Value].Fact, QuestionText = Ask[split.Value].Question, InformationGain = 1.0,
                        Impact = new CandidateImpact { CandidatesAffected = pool.Count, RetainedCodes = pool },
                    };
                break;
            }

            var keep = branches.Where(b => constraints[b].FirstOrDefault(c => c.Dimension == dim) is not { } c || c.Satisfies(value.Value)).ToList();
            var dropped = branches.Except(keep).ToList();
            if (keep.Count == 0 || dropped.Count == 0) break;

            pool = pool.Where(c => keep.Any(k => c.StartsWith(k))).ToList();
            steps.Add(new ReasoningStep
            {
                Rule = "KUM_HS_6", CandidateCode = keep.Count == 1 ? keep[0] : h.Code, Decision = CandidateDecision.Retained,
                Reason = $"Uji batas angka: nilai barang {value.Text} " +
                         (keep.Count == 1 ? $"memenuhi uraian subpos {keep[0]} (\"{Text(keep[0]).Trim().TrimStart('-', ' ').TrimEnd(':', ' ')}\")"
                                          : $"tetap memungkinkan subpos {string.Join(", ", keep)}") +
                         $"; subpos {string.Join(", ", dropped)} tidak memenuhi batasnya.",
                SourceIds = [h.SourceId ?? h.Code],
            });
        }
        return new NarrowResult(pool, steps);
    }
}
