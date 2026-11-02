using HsClassifier.Application.Classification;
using HsClassifier.Application.Classification.Rules;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Tests;

/// <summary>
/// An EN paragraph "this heading includes ..." keeps a heading in KUM HS 1 only when it names the goods. With the EN loaded,
/// most headings have such a paragraph, so keeping a heading for any of them let unrelated headings through
/// (soap ended up as a vacuum cleaner).
/// </summary>
public class EnInclusionTests
{
    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    private static CandidateHeading Vacuum(params string[] inclusions)
    {
        var c = new CandidateHeading
        {
            Code = "8508", ChapterCode = "85", Description = "Penyedot debu", HasHeadingText = true, SourceId = "btki:8508",
            ChildSubheadings = ["8508.19.10"], ChildDescriptions = { ["8508.19.10"] = "Penyedot debu rumah tangga" },
        };
        c.EnParagraphs.AddRange(inclusions.Select(i => "[Inclusion] " + i));
        c.EnInclusions.AddRange(inclusions);
        return c;
    }

    [Fact]
    public void An_inclusion_paragraph_about_something_else_does_not_keep_the_heading()
    {
        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "Sabun mandi batang")),
            [Vacuum("This heading includes domestic vacuum cleaners and floor polishers.")]);

        var step = Assert.Single(r.ReasoningChain, s => s.Rule == "KUM_HS_1" && s.CandidateCode == "8508");
        Assert.Equal(CandidateDecision.Eliminated, step.Decision);
        // nothing matches, so this is the no-match fallback (AI or expert), not a KUM HS 1 retention
        Assert.Contains(r.ReasoningChain, s => s.Rule == RulesEngine.FallbackRule);
        Assert.DoesNotContain(r.ReasoningChain, s => s.Rule == "KUM_HS_1" && s.Decision == CandidateDecision.Retained);
    }

    [Fact]
    public void An_inclusion_paragraph_that_names_the_goods_keeps_the_heading()
    {
        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "Floor polishers")),
            [Vacuum("This heading includes domestic vacuum cleaners and floor polishers.")]);

        Assert.Contains(r.ReasoningChain, s => s.Rule == "KUM_HS_1" && s.CandidateCode == "8508" && s.Decision == CandidateDecision.Retained);
    }

    [Fact]
    public void A_name_that_is_only_part_of_another_word_does_not_count()
    {
        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "Soap")),
            [Vacuum("This heading includes soaps substitutes and saponification residues.")]);

        Assert.Contains(r.ReasoningChain, s => s.Rule == "KUM_HS_1" && s.CandidateCode == "8508" && s.Decision == CandidateDecision.Eliminated);
    }
}


