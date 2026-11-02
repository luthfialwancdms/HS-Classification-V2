using HsClassifier.Application.Classification;
using HsClassifier.Application.Classification.Rules;
using HsClassifier.Domain.Classification;
using HsClassifier.Infrastructure.Services;

namespace HsClassifier.Tests;

/// <summary>
/// A heading the AI named and the database confirmed breaks a tie between headings whose text matches the goods equally
/// ("mentega": butter in 0405, cocoa butter in 1804). It never beats a heading that matches the goods better.
/// </summary>
public class AiEndorsedTests
{
    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    private static CandidateHeading Heading(string code, string description, bool endorsed, params string[] children)
    {
        var c = new CandidateHeading
        {
            Code = code, ChapterCode = code[..2], Description = description, HasHeadingText = true, SourceId = $"btki:{code}", AiEndorsed = endorsed,
            ChildSubheadings = children.ToList(),
        };
        foreach (var ch in children) c.ChildDescriptions[ch] = description;
        return c;
    }

    [Fact]
    public void Among_equal_matches_the_ai_endorsed_heading_wins_even_against_a_simpler_one()
    {
        var butter = Heading("0405", "Mentega dan lemak serta minyak lainnya yang diperoleh dari susu", endorsed: true, "0405.10.00", "0405.20.00");
        var cocoa = Heading("1804", "Mentega, lemak dan minyak kakao", endorsed: false, "1804.00.00");

        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "Mentega")), [cocoa, butter]);

        Assert.Equal("0405", r.LockedHeading);
    }

    [Fact]
    public void Without_an_endorsement_the_old_tie_break_stays()
    {
        var butter = Heading("0405", "Mentega dan lemak serta minyak lainnya yang diperoleh dari susu", endorsed: false, "0405.10.00", "0405.20.00");
        var cocoa = Heading("1804", "Mentega, lemak dan minyak kakao", endorsed: false, "1804.00.00");

        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "Mentega")), [cocoa, butter]);

        Assert.Equal("1804", r.LockedHeading);
    }

    [Fact]
    public void An_endorsement_does_not_beat_a_heading_that_matches_the_goods_much_better()
    {
        var weak = Heading("0405", "Lemak serta minyak lainnya yang diperoleh dari susu", endorsed: true, "0405.10.00", "0405.20.00");
        var strong = Heading("1804", "Mentega kakao, lemak dan minyak kakao", endorsed: false, "1804.00.00");

        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "Mentega kakao"), ("commercial_name", "Cocoa butter")), [weak, strong]);

        Assert.Equal("1804", r.LockedHeading);
    }

    [Fact]
    public async Task Headings_the_ai_listed_are_marked_endorsed_but_headings_found_only_by_its_terms_are_not()
    {
        var m = await MiniBtki.CreateAsync();

        var (candidates, accepted, _) = await m.Candidates.FromSuggestionAsync(new TermSuggestion(["Pompa vakum"], ["8471"]));

        Assert.Equal(["8471"], accepted);
        Assert.True(candidates.Single(c => c.Code == "8471").AiEndorsed);
        Assert.False(candidates.Single(c => c.Code == "8414").AiEndorsed);      // found by the term "pompa vakum" only
    }
}
