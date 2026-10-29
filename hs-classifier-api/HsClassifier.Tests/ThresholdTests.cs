using HsClassifier.Application.Classification;
using HsClassifier.Application.Classification.Rules;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Tests;

/// <summary>Numeric thresholds in subheading texts (KUM HS 6 step 9) — parsed from real BTKI wording.</summary>
public class ThresholdTests
{
    private static Constraint One(string text) => Assert.Single(Thresholds.Parse(text));

    [Fact]
    public void Range_with_mixed_units()
    {
        var c = One("- - - Dengan keluaran melebihi 750 W tetapi tidak melebihi 37,5 kW");
        Assert.Equal(Dimension.Power, c.Dimension);
        Assert.True(c.Satisfies(1000));
        Assert.False(c.Satisfies(750));      // "melebihi" = strictly greater
        Assert.True(c.Satisfies(37_500));    // "tidak melebihi" = inclusive
        Assert.False(c.Satisfies(37_501));
    }

    [Theory]
    [InlineData("- - Dengan kapasitas pendinginan tidak melebihi 21,10 kW", 21_100, true)]
    [InlineData("- - Dengan kapasitas pendinginan tidak melebihi 21.10 kW", 21_101, false)]
    [InlineData("- Mesin portabel, dengan berat tidak lebih dari 10 kg, terdiri dari", 10, true)]
    [InlineData("- - - Tipe rumah tangga, dengan kapasitas melebihi 230 l", 230_001, true)]
    [InlineData("- - Dengan kapasitas tidak melebihi 230 I", 230_000, true)]           // OCR capital I for liter
    [InlineData("- - Vibratory roller, gaya sentrifugal drum tidak melebihi 20 t berdasarkan berat", 20_000, true)]
    [InlineData("- - Dengan keluaran melebihi 750 kVA", 750_000, false)]
    public void Single_bounds(string text, double baseValue, bool satisfied) =>
        Assert.Equal(satisfied, One(text).Satisfies(baseValue));

    [Fact]
    public void Rate_units_are_not_volumes()
    {
        var all = Thresholds.Parse("- - Dengan kapasitas pendinginan melebihi 21.10 kW dengan kecepatan aliran udara lebih dari 67.96 m3/menit");
        Assert.Equal(Dimension.Power, Assert.Single(all).Dimension);
    }

    [Theory]
    [InlineData("- - Dengan keluaran tidak melebihi 7 5 kVA :")]   // OCR-split number: never guess 5 kVA
    [InlineData("- - Lain-lain")]
    [InlineData("- - Laptop termasuk notebook dan subnotebook")]
    public void Garbage_or_no_threshold_yields_nothing(string text) => Assert.Empty(Thresholds.Parse(text));

    [Theory]
    [InlineData("21 ton", Dimension.Mass, 21_000)]
    [InlineData("21000 kg", Dimension.Mass, 21_000)]
    [InlineData("1,4 kg", Dimension.Mass, 1.4)]
    [InlineData("60 kW", Dimension.Power, 60_000)]
    [InlineData("12.5 kVA", Dimension.ApparentPower, 12_500)]
    [InlineData("150 cc", Dimension.Volume, 150)]
    [InlineData("1.000 cc", Dimension.Volume, 1_000)]   // dot + 3 digits = thousands separator
    [InlineData("230 liter", Dimension.Volume, 230_000)]
    public void Quantities_from_user_answers(string text, Dimension dim, double expected)
    {
        var q = Assert.Single(Thresholds.Quantities(text));
        Assert.Equal(dim, q.Dimension);
        Assert.Equal(expected, q.Value, precision: 6);
    }

    // ─── KUM HS 6 narrowing ───────────────────────────────────────────────────

    private static CandidateHeading Motors() => new()
    {
        Code = "8501", HasHeadingText = true, Description = "Motor listrik dan generator",
        ParentDescriptions = { ["8501.7"] = "- Motor DC dan generator, fotovoltaik :" },
        ChildSubheadings = ["8501.72.10", "8501.72.20", "8501.72.30", "8501.72.40"],
        ChildDescriptions =
        {
            ["8501.72.10"] = "- - - Dengan keluaran tidak melebihi 750 W",
            ["8501.72.20"] = "- - - Dengan keluaran melebihi 750 W tetapi tidak melebihi 37,5 kW",
            ["8501.72.30"] = "- - - Dengan keluaran melebihi 37,5 kW tetapi tidak melebihi 75 kW",
            ["8501.72.40"] = "- - - Dengan keluaran melebihi 75 kW",
        },
    };

    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    [Fact]
    public void Known_value_selects_the_single_matching_branch()
    {
        var h = Motors();
        var r = Thresholds.Narrow(Facts(("power_rating", "60 kW")), h, h.ChildSubheadings, out var q);
        Assert.Null(q);
        Assert.Equal(["8501.72.30"], r.Pool);
        Assert.Contains("60 kW", Assert.Single(r.Steps).Reason);
    }

    [Fact]
    public void Value_in_specification_text_is_used()
    {
        var h = Motors();
        var r = Thresholds.Narrow(Facts(("supporting_documents", "Motor DC 3 phase, 500 W, 24V")), h, h.ChildSubheadings, out _);
        Assert.Equal(["8501.72.10"], r.Pool);
    }

    [Fact]
    public void Missing_value_asks_a_numeric_question()
    {
        var h = Motors();
        var r = Thresholds.Narrow(Facts(("generic_name", "motor DC")), h, h.ChildSubheadings, out var q);
        Assert.Equal(4, r.Pool.Count);
        Assert.Equal("power_rating", q!.FactType);
        Assert.Null(q.Choices);
    }

    [Fact]
    public void Unparseable_answer_falls_back_to_choosing_texts()
    {
        var h = Motors();
        var r = Thresholds.Narrow(Facts(("power_rating", "tidak tahu")), h, h.ChildSubheadings, out var q);
        Assert.Null(q);                 // don't ask again
        Assert.Equal(4, r.Pool.Count);  // user picks from official texts instead
    }

    [Fact]
    public void Unconstrained_siblings_are_kept_conflicting_ones_dropped()
    {
        var h = new CandidateHeading
        {
            Code = "8418", HasHeadingText = true, Description = "Lemari pendingin",
            ChildSubheadings = ["8418.10.31", "8418.10.32", "8418.10.90"],
            ChildDescriptions =
            {
                ["8418.10.31"] = "- - - Tipe rumah tangga, dengan kapasitas tidak melebihi 230 l",
                ["8418.10.32"] = "- - - Tipe rumah tangga, dengan kapasitas melebihi 230 l",
                ["8418.10.90"] = "- - - Lain-lain",
            },
        };
        var r = Thresholds.Narrow(Facts(("capacity", "300 liter")), h, h.ChildSubheadings, out _);
        Assert.Equal(["8418.10.32", "8418.10.90"], r.Pool);
    }

    [Fact]
    public void Branch_without_own_text_uses_its_single_tariff_line()
    {
        // Live: 8502.11 has no 6-digit row, only 8502.11.00 "≤ 75 kVA" — it must still be eliminated for 100 kVA
        var h = new CandidateHeading
        {
            Code = "8502", HasHeadingText = true, Description = "Perangkat pembangkit tenaga listrik",
            ParentDescriptions = { ["8502.12"] = "- - Dengan keluaran melebihi 75 kVA tetapi tidak melebihi 375 kVA :" },
            ChildSubheadings = ["8502.11.00", "8502.12.10", "8502.12.20", "8502.13.00"],
            ChildDescriptions =
            {
                ["8502.11.00"] = "- - Dengan keluaran tidak melebihi 75 kVA",
                ["8502.12.10"] = "- - - Dengan keluaran melebihi 75 kVA tetapi tidak melebihi 125 kVA",
                ["8502.12.20"] = "- - - Dengan keluaran melebihi 125 kVA tetapi tidak melebihi 375 kVA",
                ["8502.13.00"] = "- - Dengan keluaran melebihi 375 kVA",
            },
        };
        var r = Thresholds.Narrow(Facts(("power_rating", "100 kVA")), h, h.ChildSubheadings, out _);
        Assert.Equal(["8502.12.10"], r.Pool);
    }

    [Fact]
    public void Group_text_contradicting_its_own_lines_is_not_trusted()
    {
        // Live data: OCR put "750 W – 37,5 kW" on group 8501.3 whose lines are "≤ 750 W" and "> 375 kW"
        var h = new CandidateHeading
        {
            Code = "8501", HasHeadingText = true, Description = "Motor dan generator listrik",
            ParentDescriptions =
            {
                ["8501.2"] = "- Motor universal AC/DC dengan keluaran melebihi 37,5 W :",
                ["8501.3"] = "- - - Dengan keluaran melebihi 750 W tetapi tidak melebihi 37,5 kW :",
            },
            ChildSubheadings = ["8501.20.11", "8501.31.10", "8501.34.00"],
            ChildDescriptions =
            {
                ["8501.20.11"] = "- - - Dengan keluaran tidak melebihi 1 kW",
                ["8501.31.10"] = "- - - Dengan keluaran tidak melebihi 750 W",
                ["8501.34.00"] = "- - Dengan keluaran melebihi 375 kW",
            },
        };
        var r = Thresholds.Narrow(Facts(("power_rating", "400 kW")), h, h.ChildSubheadings, out _);
        Assert.Contains("8501.34.00", r.Pool);   // the right line must survive the bad group text
    }

    [Fact]
    public void Group_text_with_too_many_dashes_is_misplaced_and_ignored()
    {
        // Live: 8501.5 (AC multi-phase motors) carries "- - - dengan keluaran tidak melebihi 1 kW" from a deeper row;
        // its lines have no bound to contradict it. A 400 kW motor must not lose this branch.
        var h = new CandidateHeading
        {
            Code = "8501", HasHeadingText = true, Description = "Motor dan generator listrik",
            ParentDescriptions =
            {
                ["8501.5"] = "- - - dengan keluaran tidak melebihi 1 kW :",
                ["8501.6"] = "- Generator AC ( alternator ) :",
                ["8501.7"] = "- Motor DC dan generator, fotovoltaik, dengan keluaran tidak melebihi 50 W :",
            },
            ChildSubheadings = ["8501.53.90", "8501.61.10", "8501.71.00"],
            ChildDescriptions =
            {
                ["8501.53.90"] = "- - - Lain-lain", ["8501.61.10"] = "- - - Dengan keluaran tidak melebihi 12,5 kVA",
                ["8501.71.00"] = "- - Dengan keluaran tidak melebihi 50 W",
            },
        };
        var r = Thresholds.Narrow(Facts(("power_rating", "400 kW")), h, h.ChildSubheadings, out _);
        Assert.Contains("8501.53.90", r.Pool);
        Assert.DoesNotContain("8501.71.00", r.Pool); // a correctly written one-dash group still filters
    }

    [Fact]
    public void Engine_uses_thresholds_before_asking_for_a_choice()
    {
        var h = Motors();
        var res = new RulesEngine().ApplyKumHs(Facts(("generic_name", "motor"), ("power_rating", "60 kW")), [h]);
        Assert.Equal("8501.72.30", res.FinalCode);

        var ask = new RulesEngine().ApplyKumHs(Facts(("generic_name", "motor")), [h]);
        Assert.Equal("power_rating", Assert.Single(ask.PendingQuestions).FactType);
    }
}
