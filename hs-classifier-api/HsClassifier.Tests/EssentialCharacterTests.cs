using HsClassifier.Application.Classification;
using HsClassifier.Application.Classification.Rules;
using HsClassifier.Domain.Classification;
using Microsoft.Extensions.Configuration;

namespace HsClassifier.Tests;

/// <summary>KUM HS 3(b): six weighted factors, never one factor alone; undecided goes to 3(c). Plus the 2(b) and 4 evidence steps.</summary>
public class EssentialCharacterTests
{
    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    private static CandidateHeading H(string code, string text, string child, string childText) => new()
    {
        Code = code, Description = text, HasHeadingText = true, ChapterCode = code[..2],
        ChildSubheadings = [child], ChildDescriptions = { [child] = childText },
    };

    // Identical wording => KUM HS 3(a) cannot separate them. 3(c) alone would pick 8516 (last in numerical order).
    private static List<CandidateHeading> Tied() =>
    [
        H("8419", "Perangkat pemanas air", "8419.11.00", "Pemanas air"),
        H("8516", "Perangkat pemanas air", "8516.10.00", "Pemanas air listrik"),
    ];

    private static readonly (string, string) Composite = ("mixture_composite", "mixture");

    private static (string, string)[] Answers(string function, string material, string bulk, string value, string necessity, string perception) =>
    [
        ("generic_name", "pemanas air"), Composite,
        ("ec_function", function), ("ec_material", material), ("ec_bulk", bulk),
        ("ec_value", value), ("ec_necessity", necessity), ("ec_perception", perception),
    ];

    [Fact]
    public void Default_weights_are_the_documented_30_15_15_15_15_10()
    {
        var w = EssentialCharacterOptions.Default.Weights;
        Assert.Equal([30, 15, 15, 15, 15, 10], new[] { "function", "material", "bulk", "value", "necessity", "perception" }.Select(k => w[k]));
        Assert.Equal(100, w.Values.Sum());
    }

    [Fact]
    public void Plain_tie_without_composite_goods_goes_straight_to_3c()
    {
        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "pemanas air")), Tied());

        Assert.Equal("8516.10.00", r.FinalCode);
        Assert.Empty(r.PendingQuestions);
        Assert.DoesNotContain(r.ReasoningChain, s => s.Rule == "KUM_HS_3B");
        Assert.Contains(r.ReasoningChain, s => s.Rule == "KUM_HS_3C");
    }

    [Fact]
    public void Composite_tie_asks_one_question_per_factor_with_the_tied_headings_as_choices()
    {
        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "pemanas air"), Composite), Tied());

        Assert.Equal(ResultStatus.NeedClarification, r.Status);
        Assert.Null(r.FinalCode);
        Assert.Equal(["ec_function", "ec_material", "ec_bulk", "ec_value", "ec_necessity", "ec_perception"],
            r.PendingQuestions.Select(q => q.FactType));
        Assert.All(r.PendingQuestions, q =>
            Assert.Equal(["8419", "8516", EssentialCharacter.Equal], q.Choices!.Select(c => c.Value)));
        Assert.Equal(r.PendingQuestions.Select(q => q.FactType), r.MissingFacts);
    }

    [Fact]
    public void Heading_that_wins_enough_weight_and_at_least_two_factors_is_chosen()
    {
        // 8419: function 30 + value 15 + necessity 15 = 60. 8516: material 15 + bulk 15 + perception 10 = 40. Margin 20.
        var r = new RulesEngine().ApplyKumHs(Facts(Answers("8419", "8516", "8516", "8419", "8419", "8516")), Tied());

        Assert.Equal("8419.11.00", r.FinalCode);
        var step = Assert.Single(r.ReasoningChain, s => s.Rule == "KUM_HS_3B");
        Assert.Equal(CandidateDecision.Retained, step.Decision);
        Assert.Equal("8419", step.CandidateCode);
        Assert.Contains("60", step.Reason);          // the score and every factor are recorded, not just the winner
        Assert.Contains("40", step.Reason);
        Assert.Contains("Fungsi", step.Reason);
        Assert.Contains("Persepsi", step.Reason);
        Assert.DoesNotContain(r.ReasoningChain, s => s.Rule == "KUM_HS_3C");
    }

    [Fact]
    public void One_factor_alone_never_decides_even_with_a_big_margin()
    {
        // function (30) for 8419, everything else equal: 8419 = 65 vs 35, but it only wins one factor
        var e = EssentialCharacter.Equal;
        var r = new RulesEngine().ApplyKumHs(Facts(Answers("8419", e, e, e, e, e)), Tied());

        var step = Assert.Single(r.ReasoningChain, s => s.Rule == "KUM_HS_3B");
        Assert.Equal(CandidateDecision.Unresolved, step.Decision);
        Assert.Contains(r.ReasoningChain, s => s.Rule == "KUM_HS_3C");
        Assert.Equal("8516.10.00", r.FinalCode);      // fell through to numerical order
    }

    [Fact]
    public void Margin_below_the_minimum_is_not_enough()
    {
        // 8419: function + value = 45; 8516: material + bulk + necessity = 45; perception split 5/5 -> 50 vs 50
        var r = new RulesEngine().ApplyKumHs(
            Facts(Answers("8419", "8516", "8516", "8419", "8516", EssentialCharacter.Equal)), Tied());

        Assert.Equal(CandidateDecision.Unresolved, Assert.Single(r.ReasoningChain, s => s.Rule == "KUM_HS_3B").Decision);
        Assert.Contains(r.ReasoningChain, s => s.Rule == "KUM_HS_3C");
    }

    [Fact]
    public void Weights_come_from_the_options()
    {
        // 8419 wins material + bulk, 8516 wins the other four. Default weights: 8516 = 70 wins.
        var answers = Facts(Answers("8516", "8419", "8419", "8516", "8516", "8516"));
        Assert.Equal("8516.10.00", new RulesEngine().ApplyKumHs(answers, Tied()).FinalCode);

        // Material and bulk weigh 30 each: 8419 = 60 vs 40 and wins, although 3(c) alone would have said 8516
        var heavyMaterial = new EssentialCharacterOptions(new Dictionary<string, double>
            { ["function"] = 10, ["material"] = 30, ["bulk"] = 30, ["value"] = 10, ["necessity"] = 10, ["perception"] = 10 }, 20, 2);
        var r = new RulesEngine(heavyMaterial).ApplyKumHs(answers, Tied());
        Assert.Equal("8419.11.00", r.FinalCode);
        Assert.Contains(r.ReasoningChain, s => s.Rule == "KUM_HS_3B" && s.Decision == CandidateDecision.Retained);
    }

    [Fact]
    public void An_answer_that_is_not_one_of_the_tied_headings_counts_as_unanswered()
    {
        var r = new RulesEngine().ApplyKumHs(Facts(Answers("8419", "9999", "8419", "8419", "8419", "8419")), Tied());

        Assert.Equal(ResultStatus.NeedClarification, r.Status);
        Assert.Equal(["ec_material"], r.PendingQuestions.Select(q => q.FactType));
    }

    [Fact]
    public void Answered_factors_are_not_asked_again()
    {
        var r = new RulesEngine().ApplyKumHs(
            Facts(("generic_name", "pemanas air"), Composite, ("ec_function", "8419"), ("ec_value", "EQUAL")), Tied());

        Assert.Equal(["ec_material", "ec_bulk", "ec_necessity", "ec_perception"], r.PendingQuestions.Select(q => q.FactType));
    }

    [Fact]
    public void Retail_set_also_triggers_3b()
    {
        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "pemanas air"), ("set_kit", "retail_set")), Tied());
        Assert.Equal(ResultStatus.NeedClarification, r.Status);
        Assert.Equal(6, r.PendingQuestions.Count);
    }

    [Fact]
    public void Kum_hs_2b_records_which_headings_name_the_material_and_keeps_all_candidates()
    {
        var steel = H("7310", "Wadah dari baja", "7310.10.00", "Wadah");
        var plastic = H("3923", "Wadah dari plastik", "3923.10.00", "Wadah");
        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "wadah"), ("material", "baja"), Composite), [steel, plastic]);

        var step = Assert.Single(r.ReasoningChain, s => s.Rule == "KUM_HS_2B");
        Assert.Contains("7310", step.Reason);
        Assert.DoesNotContain("3923", step.Reason);
        Assert.Equal(2, r.ReasoningChain.Count(s => s.Rule == "KUM_HS_1" && s.Decision == CandidateDecision.Retained));
    }

    [Fact]
    public void No_match_adds_the_most_akin_headings_as_evidence_without_the_gri4_flag()
    {
        // Heading text says nothing about the goods' name, but it names their use: KUM HS 1 finds no match
        var roads = H("8479", "Mesin untuk konstruksi jalan", "8479.10.00", "Mesin jalan");
        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "zzz"), ("primary_use", "konstruksi jalan")), [roads]);

        Assert.Contains(r.ReasoningChain, s => s.Rule == RulesEngine.FallbackRule);
        var akin = Assert.Single(r.ReasoningChain, s => s.Rule == "KUM_HS_4_COMPARE");
        Assert.Equal("8479", akin.CandidateCode);
        Assert.Contains("penggunaan", akin.Reason);
        Assert.DoesNotContain(r.ReasoningChain, s => s.Rule == "KUM_HS_4");   // not counted as GRI 4 applied
    }
}

public class EssentialCharacterConfigTests
{
    private static Microsoft.Extensions.Configuration.IConfiguration Cfg(Dictionary<string, string?> kv) =>
        new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(kv).Build();

    [Fact]
    public void Missing_config_keeps_the_documented_defaults()
    {
        var o = HsClassifier.Infrastructure.Services.ClassificationOrchestrator.ReadEssentialCharacter(Cfg([]));
        Assert.Equal(EssentialCharacterOptions.Default.Weights, o.Weights);
        Assert.Equal(20, o.MinMargin);
        Assert.Equal(2, o.MinFactorWins);
    }

    [Fact]
    public void Config_overrides_single_weights_and_thresholds_and_ignores_garbage()
    {
        var o = HsClassifier.Infrastructure.Services.ClassificationOrchestrator.ReadEssentialCharacter(Cfg(new()
        {
            ["Rules:EssentialCharacter:Weights:Function"] = "40",
            ["Rules:EssentialCharacter:Weights:Material"] = "abc",
            ["Rules:EssentialCharacter:MinMargin"] = "25.5",
            ["Rules:EssentialCharacter:MinFactorWins"] = "3",
        }));
        Assert.Equal(40, o.Weights["function"]);
        Assert.Equal(15, o.Weights["material"]);     // unparseable -> default
        Assert.Equal(25.5, o.MinMargin);
        Assert.Equal(3, o.MinFactorWins);
    }
}
