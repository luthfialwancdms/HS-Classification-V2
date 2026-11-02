using HsClassifier.Infrastructure.Services;

namespace HsClassifier.Tests;

/// <summary>AI output is untrusted: only generic terms and 4-digit headings may pass.</summary>
public class GroqParseTests
{
    [Fact]
    public void Valid_json_is_parsed_and_heading_dots_normalized()
    {
        var s = GroqTermSuggester.Parse("""{"terms":["pesawat telepon","jaringan seluler"],"headings":["85.17","8517"]}""");
        Assert.NotNull(s);
        Assert.Equal(["pesawat telepon", "jaringan seluler"], s!.Terms);
        Assert.Equal(["8517"], s.Headings);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("""{"terms":[],"headings":[]}""")]
    public void Broken_or_empty_output_returns_null(string content) => Assert.Null(GroqTermSuggester.Parse(content));

    [Theory]
    [InlineData("Your quota is exhausted.\n\nAPI key status: exceeded\nQuota: 0/5000000 tokens (0.0%)")]
    [InlineData("Maaf, saya tidak bisa membantu.")]
    [InlineData("{bukan json")]
    public void Text_that_is_not_the_requested_json_is_unreadable_not_just_empty(string content)
    {
        // A proxy can answer HTTP 200 with a notice (quota, rate limit). That is a failed AI call, not "the AI had no idea".
        var (s, unreadable) = TermSuggesterPrompt.Interpret(content);

        Assert.Null(s);
        Assert.True(unreadable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("""{"terms":[],"headings":[]}""")]
    public void An_empty_answer_is_nothing_usable_but_not_a_failure(string content)
    {
        var (s, unreadable) = TermSuggesterPrompt.Interpret(content);

        Assert.Null(s);
        Assert.False(unreadable);
    }

    [Fact]
    public void Json_inside_a_code_fence_or_after_a_sentence_is_still_read()
    {
        var (s, unreadable) = TermSuggesterPrompt.Interpret("Berikut hasilnya:\n```json\n{\"terms\":[\"pengeras suara\"],\"headings\":[\"8518\"]}\n```");

        Assert.False(unreadable);
        Assert.Equal(["pengeras suara"], s!.Terms);
        Assert.Equal(["8518"], s.Headings);
    }

    [Fact]
    public void Six_and_eight_digit_codes_are_rejected()
    {
        var s = GroqTermSuggester.Parse("""{"terms":["telepon"],"headings":["8517.13.00","851713","85171300"]}""");
        Assert.Empty(s!.Headings);
    }

    [Fact]
    public void Caps_dedupe_and_short_terms_are_enforced()
    {
        var s = GroqTermSuggester.Parse(
            """{"terms":["a","ab","t1x","T1X","t2x","t3x","t4x","t5x","t6x"],"headings":["0101","0102","0103","0104"]}""");
        Assert.Equal(["t1x", "t2x", "t3x", "t4x", "t5x"], s!.Terms);
        Assert.Equal(3, s.Headings.Count);
    }
}

/// <summary>Fallback 3c: ask the generic name once, then expert review.</summary>
public class NoCandidateFallbackTests
{
    [Fact]
    public void Without_generic_name_asks_for_it_once()
    {
        var (status, message, q) = ClassificationOrchestrator.DecideNoCandidate(hasGenericName: false);
        Assert.Equal(HsClassifier.Domain.Classification.ResultStatus.NeedClarification, status);
        Assert.Equal("generic_name", q!.FactType);
        Assert.Null(q.Choices);
        Assert.Contains("ahli kepabeanan", message);
    }

    [Fact]
    public void With_generic_name_goes_to_expert_review()
    {
        var (status, message, q) = ClassificationOrchestrator.DecideNoCandidate(hasGenericName: true);
        Assert.Equal(HsClassifier.Domain.Classification.ResultStatus.NeedExpertReview, status);
        Assert.Null(q);
        Assert.Contains("ahli kepabeanan", message);
    }
}
