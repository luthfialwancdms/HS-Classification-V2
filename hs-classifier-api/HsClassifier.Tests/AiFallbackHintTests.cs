using HsClassifier.Infrastructure.Services;

namespace HsClassifier.Tests;

/// <summary>
/// When the heading text matches nothing and the AI gives nothing usable, the rules still have a best guess from the
/// database (KUM HS 6). It is shown to the expert as a hint, never as the final code.
/// </summary>
public class AiFallbackHintTests
{
    // "Komputer personal" appears only in a child line of 8471, so KUM HS 1 finds no heading match and keeps 8471 as the fallback
    private static readonly (string, string)[] Dumper = [("generic_name", "Komputer personal")];

    [Fact]
    public async Task Best_database_guess_is_given_to_the_expert_as_a_hint_when_the_ai_has_nothing()
    {
        var m = await MiniBtki.CreateAsync(new FakeSuggester(suggestion: null));
        var r = await m.ClassifyAsync(Dumper);

        Assert.Equal("NEED_EXPERT_REVIEW", r.Status);
        Assert.Null(r.FinalCode);                              // a hint is not a decision
        Assert.Contains("8471.41.10", r.Message);
        Assert.Contains("belum pasti", r.Message);
        Assert.Contains("8471", r.CandidateCodes ?? []);
        Assert.DoesNotContain("tidak tersedia", r.Message);
    }

    [Fact]
    public async Task Unavailable_ai_is_reported_and_the_hint_is_still_given()
    {
        var m = await MiniBtki.CreateAsync(new FakeSuggester(suggestion: null, unavailable: true));
        var r = await m.ClassifyAsync(Dumper);

        Assert.Null(r.FinalCode);
        Assert.StartsWith(ClassificationOrchestrator.AiUnavailableNotice, r.Message);
        Assert.Contains("8471.41.10", r.Message);
    }

    [Fact]
    public async Task Without_any_database_guess_the_message_has_no_hint()
    {
        var m = await MiniBtki.CreateAsync(new FakeSuggester(suggestion: null));
        var r = await m.ClassifyAsync(("generic_name", "zzz qqq"));

        Assert.Equal("NEED_EXPERT_REVIEW", r.Status);
        Assert.DoesNotContain("Saran sementara", r.Message);
    }
}

