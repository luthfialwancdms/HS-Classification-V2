using HsClassifier.Application.Classification;
using HsClassifier.Application.Classification.Rules;
using HsClassifier.Domain.Classification;
using HsClassifier.Infrastructure.Services;

namespace HsClassifier.Tests;

/// <summary>
/// Case #262: "Sepeda Fixie Wilson" was sent to the motorcycle heading (8711) and asked for the engine capacity, because
/// "Cara kerja" was left empty and nothing else told the engine the bicycle has no motor. Now the engine reads the hint
/// from the text when it is there ("kayuh", "fixie"), and asks one plain question when it is not.
/// </summary>
public class MotorQuestionTests
{
    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    // ─── the hint ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Sepeda fixie", "", "Manual / Tanpa tenaga")]
    [InlineData("Sepeda", "Sepeda Fixie Wilson", "Manual / Tanpa tenaga")]
    [InlineData("Sepeda kayuh", "", "Manual / Tanpa tenaga")]
    [InlineData("Sepeda gowes", "", "Manual / Tanpa tenaga")]
    [InlineData("Sepeda tanpa motor", "", "Manual / Tanpa tenaga")]
    [InlineData("Sepeda listrik", "", "Elektrik / Motor listrik")]
    [InlineData("Sepeda motor", "", "Elektrik / Motor listrik")]
    [InlineData("Sepeda", "Polygon Xtrada", "")]
    [InlineData("Sepeda tanpa motor tapi listrik", "", "")]
    public void The_hint_comes_from_the_names_and_is_empty_when_the_text_says_nothing_or_contradicts_itself(string generic, string commercial, string expected)
    {
        var facts = commercial.Length == 0 ? Facts(("generic_name", generic)) : Facts(("generic_name", generic), ("commercial_name", commercial));

        Assert.Equal(expected, RulesEngine.MotorHint(facts));
    }

    [Fact]
    public void An_answer_in_Cara_kerja_beats_any_guess_from_the_text()
    {
        var facts = Facts(("generic_name", "Sepeda kayuh"), ("power_source", "Elektrik / Motor listrik"));

        Assert.Equal("Elektrik / Motor listrik", RulesEngine.MotorHint(facts));
    }

    [Fact]
    public void A_free_text_answer_to_the_capacity_question_counts_as_a_hint()
    {
        // case #262: the user answered "Sepeda Kayuh" to the engine-capacity question
        Assert.Equal("Manual / Tanpa tenaga", RulesEngine.MotorHint(Facts(("generic_name", "Sepeda"), ("capacity", "Sepeda Kayuh"))));
    }

    // ─── the pipeline ─────────────────────────────────────────────────────────

    private static readonly (string, string)[] Bike =
    [
        ("generic_name", "Sepeda"), ("material", "Aluminium"), ("primary_function", "Mengangkut orang (transportasi darat)"),
        ("mobility", "Tidak relevan"), ("assembled_status", "complete"), ("part_accessory", "complete"),
    ];

    private static (string, string)[] With(params (string, string)[] extra) => Bike.Concat(extra).ToArray();

    private static bool NeverMotorcycle(ClassificationSessionResult r) =>
        r.FinalCode?.StartsWith("8711") != true && !(r.CandidateCodes ?? []).Contains("8711")
        && !(r.Questions ?? []).Any(q => q.QuestionText.Contains("8711") || (q.Choices ?? []).Any(c => c.Value.StartsWith("8711")));

    [Fact]
    public async Task Fixie_named_in_the_trade_name_goes_to_the_bicycle_heading_without_a_motor_question()
    {
        var m = await MiniBtki.CreateAsync();

        var r = await m.ClassifyAsync(With(("commercial_name", "Sepeda Fixie Wilson")));

        Assert.True(NeverMotorcycle(r), $"status={r.Status} final={r.FinalCode} cand={string.Join(",", r.CandidateCodes ?? [])}");
        Assert.DoesNotContain(r.Questions ?? [], q => q.FactType is "power_source" or "capacity");
        Assert.True(r.FinalCode?.StartsWith("8712") == true || (r.CandidateCodes ?? []).Contains("8712"));
    }

    [Fact]
    public async Task Without_any_hint_the_engine_asks_whether_the_goods_have_a_motor_before_choosing_a_heading()
    {
        var m = await MiniBtki.CreateAsync();

        var r = await m.ClassifyAsync(With(("commercial_name", "Polygon Xtrada")));

        Assert.Equal("NEED_CLARIFICATION", r.Status);
        Assert.Null(r.FinalCode);
        var q = Assert.Single(r.Questions!);
        Assert.Equal("power_source", q.FactType);
        Assert.Equal(3, q.Choices!.Count);
        Assert.Contains(q.Choices, c => c.Value == "Manual / Tanpa tenaga");
        Assert.Contains(q.Choices, c => c.Value.Contains("listrik", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("cc", q.QuestionText);
    }

    [Theory]
    [InlineData("Manual / Tanpa tenaga", "8712")]
    [InlineData("Elektrik / Motor listrik", "8711")]
    [InlineData("Bensin / Diesel (motor bakar)", "8711")]
    public async Task The_answer_to_the_motor_question_picks_the_matching_heading(string answer, string expected)
    {
        var m = await MiniBtki.CreateAsync();

        var r = await m.ClassifyAsync(With(("commercial_name", "Polygon Xtrada"), ("power_source", answer)));

        Assert.DoesNotContain(r.Questions ?? [], q => q.FactType == "power_source");
        Assert.True(r.FinalCode?.StartsWith(expected) == true || (r.CandidateCodes ?? []).Contains(expected),
            $"status={r.Status} final={r.FinalCode} cand={string.Join(",", r.CandidateCodes ?? [])}");
    }

    [Fact]
    public async Task Case_262_exactly_as_reported_ends_at_the_bicycle_heading_and_never_asks_for_cc()
    {
        var m = await MiniBtki.CreateAsync();

        var r = await m.ClassifyAsync(
            ("commercial_name", "Sepeda Fixie Wilson"), ("generic_name", "Sepeda"), ("material", "Aluminium"),
            ("primary_function", "Mengangkut orang (transportasi darat)"), ("mobility", "Tidak relevan"),
            ("assembled_status", "incomplete"), ("part_accessory", "complete"), ("set_kit", "retail_set"),
            ("essential_character_present", "yes"), ("set_single_purpose", "no"), ("set_retail_ready", "yes"),
            ("set_essential_item", "Sepeda"), ("capacity", "Sepeda Kayuh"));

        Assert.True(NeverMotorcycle(r), $"status={r.Status} final={r.FinalCode} cand={string.Join(",", r.CandidateCodes ?? [])} msg={r.Message}");
        Assert.DoesNotContain(r.Questions ?? [], q => q.FactType == "capacity");
    }

    [Fact]
    public async Task Electric_bicycle_named_as_such_goes_to_the_motorized_heading_without_a_question()
    {
        var m = await MiniBtki.CreateAsync();

        var r = await m.ClassifyAsync(("generic_name", "Sepeda listrik"), ("primary_function", "Mengangkut orang (transportasi darat)"));

        Assert.DoesNotContain(r.Questions ?? [], q => q.FactType == "power_source");
        Assert.True(r.FinalCode?.StartsWith("8711") == true || (r.CandidateCodes ?? []).Contains("8711"));
    }

    [Fact]
    public async Task Goods_whose_candidates_do_not_differ_by_a_motor_get_no_motor_question()
    {
        var m = await MiniBtki.CreateAsync();

        var r = await m.ClassifyAsync(("generic_name", "Pompa"));

        Assert.DoesNotContain(r.Questions ?? [], q => q.FactType == "power_source");
    }
}
