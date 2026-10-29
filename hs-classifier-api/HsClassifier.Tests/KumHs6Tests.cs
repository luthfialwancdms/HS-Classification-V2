using HsClassifier.Application.Classification;
using HsClassifier.Application.Classification.Rules;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Tests;

/// <summary>KUM HS 6 must never pick a default subheading (Req 16.6, 16.7, 17.1).</summary>
public class KumHs6Tests
{
    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    private static CandidateHeading Heading(params (string Code, string Desc)[] children) => new()
    {
        Code = "8429", Description = "Ekskavator, buldoser dan mesin penggali", ChapterCode = "84",
        ChildSubheadings = children.Select(c => c.Code).ToList(),
        ChildDescriptions = children.ToDictionary(c => c.Code, c => c.Desc),
    };

    private static readonly FactSet Excavator = Facts(("generic_name", "ekskavator"), ("primary_function", "menggali tanah"));

    [Fact]
    public void Ambiguous_children_ask_from_official_texts_not_first_child()
    {
        var h = Heading(("8429.11.00", "Track laying"), ("8429.19.00", "Lain-lain"));
        var r = new RulesEngine().ApplyKumHs(Excavator, [h]);

        Assert.Equal(ResultStatus.NeedClarification, r.Status);
        Assert.Null(r.FinalCode);
        var q = Assert.Single(r.PendingQuestions);
        Assert.Equal(FactExtractionService.SubheadingChoice, q.FactType);
        // Both lines share group 8429.1, so they are compared at the 6-digit level
        Assert.Equal(["8429.11", "8429.19", RulesEngine.NoneChoice], q.Choices!.Select(c => c.Value).Order());
    }

    /// <summary>8429 shape from BTKI: one-dash groups first (same level), then lines within the chosen group.</summary>
    private static CandidateHeading Btki8429() => new()
    {
        Code = "8429", HasHeadingText = true,
        Description = "Buldoser, angledoser, grader, mesin perata, mesin pengikis, shovel mekanik, ekskavator",
        ParentDescriptions = { ["8429.1"] = "- Buldoser dan angledoser :", ["8429.5"] = "- Sekop mekanik, ekskavator dan shovel loader :" },
        ChildSubheadings = ["8429.11.00", "8429.19.00", "8429.20.00", "8429.51.00", "8429.52.00", "8429.59.00"],
        ChildDescriptions =
        {
            ["8429.11.00"] = "- Buldoser dan angledoser : | - - Track laying",
            ["8429.19.00"] = "- Buldoser dan angledoser : | - - Lain-lain",
            ["8429.20.00"] = "- Grader dan mesin perata",
            ["8429.51.00"] = "- Sekop mekanik, ekskavator dan shovel loader : | - - Front-end shovel loader",
            ["8429.52.00"] = "- Sekop mekanik, ekskavator dan shovel loader : | - - Mesin yang berputar 360° diatas dasarnya",
            ["8429.59.00"] = "- Sekop mekanik, ekskavator dan shovel loader : | - - Lain-lain",
        },
    };

    [Fact]
    public void Kum_hs_6_descends_level_by_level_with_user_choices()
    {
        var engine = new RulesEngine();

        // Round 1: compare one-dash groups only (Req 16.3), labelled with their official text
        var r1 = engine.ApplyKumHs(Facts(("commercial_name", "Excavator")), [Btki8429()]);
        var q1 = Assert.Single(r1.PendingQuestions);
        Assert.Equal(["8429.1", "8429.2", "8429.5", RulesEngine.NoneChoice], q1.Choices!.Select(c => c.Value).Order());
        Assert.Contains(q1.Choices!, c => c.Value == "8429.5" && c.Label.Contains("Sekop mekanik, ekskavator"));

        // Round 2: within 8429.5, "Lain-lain" is still offered and the context names the group
        var r2 = engine.ApplyKumHs(Facts(("commercial_name", "Excavator"), ("subheading_choice", "8429.5")), [Btki8429()]);
        var q2 = Assert.Single(r2.PendingQuestions);
        Assert.Equal(["8429.51", "8429.52", "8429.59", RulesEngine.NoneChoice], q2.Choices!.Select(c => c.Value).Order());
        Assert.Contains("Sekop mekanik", q2.QuestionText);

        // Round 3: confirmed 6-digit branch with a single line → final code, high confidence
        var r3 = engine.ApplyKumHs(Facts(("commercial_name", "Excavator"), ("subheading_choice", "8429.52")), [Btki8429()]);
        Assert.Equal("8429.52.00", r3.FinalCode);
        Assert.Equal(ConfidenceLevel.High, r3.Confidence);
    }

    [Fact]
    public void Identical_parent_texts_fall_back_to_tariff_line_labels()
    {
        const string dup = "Perangkat telepon, termasuk smartphone dan telepon lainnya";
        var h = new CandidateHeading
        {
            Code = "8517", HasHeadingText = true, Description = "Perangkat telepon",
            ParentDescriptions = { ["8517.1"] = "- Perangkat telepon :", ["8517.13"] = dup, ["8517.14"] = dup },
            ChildSubheadings = ["8517.13.00", "8517.14.00"],
            ChildDescriptions = { ["8517.13.00"] = $"{dup} | - - Smartphone", ["8517.14.00"] = $"{dup} | - - Telepon lainnya untuk jaringan seluler" },
        };
        var q = RulesEngine.BuildSubheadingQuestion(Facts(("commercial_name", "Handphone")), h, h.ChildSubheadings);
        Assert.Contains(q.Choices!, c => c.Value == "8517.13" && c.Label.EndsWith("Smartphone"));
        Assert.Contains(q.Choices!, c => c.Value == "8517.14" && c.Label.Contains("Telepon lainnya"));
    }

    [Fact]
    public void Weak_kum_hs_1_match_still_determines_on_confirmed_subheading()
    {
        // AI path: heading text didn't lexically match (NO_MATCH), but the user confirms the subheading.
        // Result must be DETERMINED, not stuck on expert review.
        var h = new CandidateHeading
        {
            Code = "8517", HasHeadingText = true, Description = "Perangkat telepon untuk jaringan seluler",
            ParentDescriptions = { ["8517.1"] = "- Perangkat telepon :" },
            ChildSubheadings = ["8517.13.00", "8517.14.00"],
            ChildDescriptions = { ["8517.13.00"] = "- - Smartphone", ["8517.14.00"] = "- - Telepon lainnya" },
        };
        // No name overlap with the heading → KUM HS 1 records KUM_HS_1_NO_MATCH
        var r = new RulesEngine().ApplyKumHs(Facts(("commercial_name", "zzz"), ("subheading_choice", "8517.13")), [h]);
        Assert.Equal(ResultStatus.Determined, r.Status);
        Assert.Equal("8517.13.00", r.FinalCode);
        Assert.Contains(r.ReasoningChain, s => s.Rule == RulesEngine.FallbackRule);
    }

    [Fact]
    public void None_of_these_goes_to_expert_review()
    {
        var r = new RulesEngine().ApplyKumHs(Facts(("commercial_name", "Excavator"), ("subheading_choice", "NONE")), [Btki8429()]);
        Assert.Equal(ResultStatus.NeedExpertReview, r.Status);
        Assert.Null(r.FinalCode);
    }

    [Fact]
    public void Choice_from_another_heading_is_ignored()
    {
        var r = new RulesEngine().ApplyKumHs(Facts(("commercial_name", "Excavator"), ("subheading_choice", "8471.30")), [Btki8429()]);
        Assert.Equal(ResultStatus.NeedClarification, r.Status);
        Assert.Equal(6, r.PendingQuestions[0].Impact.CandidatesAffected);
    }

    [Fact]
    public void Unique_description_match_is_selected()
    {
        var h = Heading(("8429.11.00", "Buldoser track laying"), ("8429.52.00", "Ekskavator dengan superstruktur berputar 360°"));
        var r = new RulesEngine().ApplyKumHs(Excavator, [h]);

        Assert.Equal(ResultStatus.Determined, r.Status);
        Assert.Equal("8429.52.00", r.FinalCode);
    }

    [Fact]
    public void Single_tariff_line_is_deterministic()
    {
        var r = new RulesEngine().ApplyKumHs(Excavator, [Heading(("8429.20.00", "Grader"))]);
        Assert.Equal("8429.20.00", r.FinalCode);
    }

    [Fact]
    public void Heading_without_tariff_lines_never_fabricates_a_code()
    {
        var r = new RulesEngine().ApplyKumHs(Excavator, [Heading()]);
        Assert.Equal(ResultStatus.NeedExpertReview, r.Status);
        Assert.Null(r.FinalCode);
    }

    [Fact]
    public void Kum_hs_1_matches_whole_words_only()
    {
        // "mesin" must not match "permesinan" (was returning 4906 drawings for a laptop)
        var drawings = new CandidateHeading
        {
            Code = "4906", Description = "Rancangan dan gambar untuk arsitektur, permesinan, industri",
            ChildSubheadings = ["4906.00.00"], ChildDescriptions = { ["4906.00.00"] = "Rancangan dan gambar" },
        };
        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "mesin pengolah data")), [drawings]);
        // The heading is eliminated by KUM HS 1 and only kept as the NO_MATCH fallback candidate
        Assert.Contains(r.ReasoningChain, s => s.Rule == "KUM_HS_1" && s.Decision == CandidateDecision.Eliminated);
        Assert.Contains(r.ReasoningChain, s => s.Rule == RulesEngine.FallbackRule);
    }

    [Fact]
    public void Kum_hs_1_uses_heading_text_not_child_line_mentions()
    {
        // 4011 tyres: a child line mentions "forklift", but the heading itself is about tyres
        var tyres = new CandidateHeading
        {
            Code = "4011", Description = "Ban bertekanan, baru, dari karet. / New pneumatic tyres, of rubber.",
            HasHeadingText = true, ChildSubheadings = ["4011.80.11"],
            ChildDescriptions = { ["4011.80.11"] = "Dari jenis yang digunakan pada forklift" },
        };
        var forklifts = new CandidateHeading
        {
            Code = "8427", Description = "Truk forklift / Fork-lift trucks; other works trucks fitted with lifting equipment",
            HasHeadingText = true, ChildSubheadings = ["8427.10.00"],
            ChildDescriptions = { ["8427.10.00"] = "Truk bertenaga motor listrik" },
        };
        var r = new RulesEngine().ApplyKumHs(Facts(("commercial_name", "Forklift")), [tyres, forklifts]);
        Assert.Equal("8427.10.00", r.FinalCode);
    }

    [Fact]
    public void Kum_hs_6_ignores_terms_inside_exclusion_clauses()
    {
        var adp = new CandidateHeading
        {
            Code = "8471", Description = "Mesin pengolah data otomatis dan unitnya", HasHeadingText = true,
            ChildSubheadings = ["8471.30.20", "8471.41.10"],
            ChildDescriptions =
            {
                ["8471.30.20"] = "Laptop termasuk notebook dan subnotebook",
                ["8471.41.10"] = "Komputer personal tidak termasuk komputer portabel dari subpos 8471.30",
            },
        };
        var r = new RulesEngine().ApplyKumHs(
            Facts(("commercial_name", "Laptop"), ("generic_name", "mesin pengolah data otomatis portabel")), [adp]);
        Assert.Equal("8471.30.20", r.FinalCode);
    }

}

/// <summary>KUM HS 3(a): a heading about the goods beats one that only mentions them.</summary>
public class HeadingFocusTests
{
    private static HsClassifier.Domain.Classification.CandidateHeading Heading(string code, string text, string children) => new()
    {
        Code = code, Description = text, HasHeadingText = true, ChapterCode = code[..2],
        ChildSubheadings = ["x"], ChildDescriptions = { ["x"] = children },
    };

    [Fact]
    public void Heading_about_electric_motors_beats_hand_tools_that_mention_them_even_if_child_lines_share_more_words()
    {
        var facts = new HsClassifier.Application.Classification.FactExtractionService()
            .ExtractFromInput(new() { ["generic_name"] = "motor listrik" });
        var motors = Heading("8501", "Motor dan generator listrik (tidak termasuk perangkat pembangkit tenaga listrik). / Electric motors and generators.",
            "- Motor dengan keluaran tidak melebihi 37,5 W");               // child lines never say "listrik"
        var tools = Heading("8467", "Perkakas tangan, digerakkan secara pneumatik, hidraulik atau dengan motor listrik atau bukan listrik terpasang. / Tools for working in the hand.",
            "- Dengan motor listrik terpasang");                              // child lines say both words

        var r = new HsClassifier.Application.Classification.Rules.RulesEngine().ApplyKumHs(facts, [tools, motors]);

        Assert.Equal("8501", r.LockedHeading);
    }
}
