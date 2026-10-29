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
