using HsClassifier.Application.Classification;

namespace HsClassifier.Tests;

/// <summary>Weighted confidence + caps + risk flags exactly as the V2 product logic document specifies.</summary>
public class ConfidenceTests
{
    private static ConfidenceInput Full() => new(
        DecisiveFactsComplete: true, HeadingMatch: 1.0, LegalNotesTested: true, EnReviewed: true,
        SubheadingCertainty: 1.0, EliminationStrength: 1.0, CandidateAmbiguityUnresolved: false,
        Gri4Used: false, Gri3cUsed: false, AiTermSuggestion: false);

    [Fact]
    public void All_components_full_scores_100_without_flags()
    {
        var r = ConfidenceCalculator.Compute(Full());
        Assert.Equal(100, r.Score);
        Assert.Empty(r.RiskFlags);
    }

    [Fact]
    public void Weights_follow_document_25_20_15_15_15_10()
    {
        var r = ConfidenceCalculator.Compute(Full() with { HeadingMatch = 0.5, SubheadingCertainty = 0.5 });
        Assert.Equal(100 - 10 - 7.5, r.Components.Sum(c => c.Points), precision: 3);
    }

    [Fact]
    public void Legal_notes_not_tested_caps_at_60()
    {
        var r = ConfidenceCalculator.Compute(Full() with { LegalNotesTested = false });
        Assert.Equal(60, r.Score);
        Assert.Contains(ConfidenceCalculator.LegalNotesNotReviewed, r.RiskFlags);
    }

    [Fact]
    public void En_not_reviewed_caps_at_75()
    {
        var r = ConfidenceCalculator.Compute(Full() with { EnReviewed = false });
        Assert.Equal(75, r.Score);
        Assert.Contains(ConfidenceCalculator.EnNotReviewed, r.RiskFlags);
    }

    [Fact]
    public void Missing_decisive_fact_caps_at_69_and_lowest_cap_wins()
    {
        var r = ConfidenceCalculator.Compute(Full() with { DecisiveFactsComplete = false });
        Assert.True(r.Score <= 69);
        Assert.Contains(ConfidenceCalculator.MissingDecisiveFact, r.RiskFlags);

        var both = ConfidenceCalculator.Compute(Full() with { DecisiveFactsComplete = false, CandidateAmbiguityUnresolved = true });
        Assert.True(both.Score <= 59);
        Assert.Contains(ConfidenceCalculator.MultipleEqualCandidates, both.RiskFlags);
    }

    [Fact]
    public void Gri4_and_3c_and_ai_are_flagged()
    {
        var r = ConfidenceCalculator.Compute(Full() with { Gri4Used = true, Gri3cUsed = true, AiTermSuggestion = true });
        Assert.Contains(ConfidenceCalculator.Gri4Flag, r.RiskFlags);
        Assert.Contains(ConfidenceCalculator.MultipleEqualCandidates, r.RiskFlags);
        Assert.Contains(ConfidenceCalculator.AiTermFlag, r.RiskFlags);
    }

    [Theory]
    [InlineData(85, "High")]
    [InlineData(80, "High")]
    [InlineData(60, "Medium")]
    [InlineData(59, "Low")]
    public void Level_bands(int score, string level) => Assert.Equal(level, ConfidenceCalculator.Level(score).ToString());
}
