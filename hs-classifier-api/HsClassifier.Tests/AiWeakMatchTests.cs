using HsClassifier.Infrastructure.Services;

namespace HsClassifier.Tests;

/// <summary>
/// The AI helper is consulted when the database found nothing, and also when it found only weak matches (the first word,
/// a shorter phrase, the function or material). Whole-name matches are trusted and never need the AI.
/// </summary>
public class AiWeakMatchTests
{
    [Fact]
    public async Task Candidates_found_only_by_the_head_noun_are_checked_with_the_ai_and_its_validated_heading_wins()
    {
        var ai = new FakeSuggester(new TermSuggestion(["pompa vakum"], ["8414"]));
        var m = await MiniBtki.CreateAsync(ai);

        var r = await m.ClassifyAsync(("generic_name", "Pompa air"));

        Assert.Equal(1, ai.Calls);
        Assert.Contains("8414", r.CandidateCodes ?? []);
        Assert.DoesNotContain("8413", r.CandidateCodes ?? []);
    }

    [Fact]
    public async Task A_whole_name_match_is_trusted_and_the_ai_is_not_called()
    {
        var ai = new FakeSuggester(new TermSuggestion(["x"], ["8414"]));
        var m = await MiniBtki.CreateAsync(ai);

        await m.ClassifyAsync(("generic_name", "Pompa vakum"));

        Assert.Equal(0, ai.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_the_ai_has_nothing_or_is_unavailable_the_weak_database_result_is_kept(bool unavailable)
    {
        var ai = new FakeSuggester(suggestion: null, unavailable: unavailable);
        var m = await MiniBtki.CreateAsync(ai);

        var r = await m.ClassifyAsync(("generic_name", "Pompa air"));

        Assert.Equal(1, ai.Calls);
        Assert.Equal("NEED_CLARIFICATION", r.Status);           // asks about the pump subheading as before, not a dead end
        Assert.NotEmpty(r.Questions!);
        Assert.Equal(unavailable, r.Message?.StartsWith(ClassificationOrchestrator.AiUnavailableNotice) ?? false);
    }
}

