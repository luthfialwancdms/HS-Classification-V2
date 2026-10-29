namespace HsClassifier.Domain.Classification;

public enum FactStatus { Unknown, NotApplicable, Zero, Conflicting, Verified }
public enum FactSource { User, Document, Expert, System }
public enum ResultStatus { Determined, NeedClarification, NeedExpertReview }
public enum ConfidenceLevel { High, Medium, Low }
public enum CandidateDecision { Retained, Eliminated, Unresolved }

public class Fact
{
    public string Type { get; set; } = string.Empty;
    public string? RawValue { get; set; }
    public string? NormalizedValue { get; set; }
    public string? Unit { get; set; }
    public FactSource Source { get; set; } = FactSource.User;
    public string? EvidenceReference { get; set; }
    public double ConfidenceScore { get; set; } = 1.0;
    public FactStatus Status { get; set; } = FactStatus.Unknown;
}

public class FactSet
{
    public Dictionary<string, Fact> Facts { get; set; } = new();

    public Fact? Get(string type) => Facts.TryGetValue(type, out var f) ? f : null;
    public string? GetValue(string type) => Get(type)?.NormalizedValue;
    public bool IsKnown(string type) => Get(type)?.Status == FactStatus.Verified;
    public bool IsUnknown(string type) => Get(type) is null || Get(type)!.Status == FactStatus.Unknown;

    /// <summary>
    /// Single source of truth for "does this discriminator still need asking?" — shared by
    /// the question generator and KUM HS 6 so they can never disagree.
    /// Aliases are the same fact captured under another field, not inferred values.
    /// </summary>
    public bool IsAnswered(string type)
    {
        if (IsKnown(type) || Get(type)?.Status == FactStatus.NotApplicable) return true;
        var spec = GetValue("supporting_documents") ?? "";
        bool Has(params string[] units) => units.Any(u => System.Text.RegularExpressions.Regex.IsMatch(
            spec, $@"\d\s*{u}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        return type switch
        {
            "ckd_skd_status" => IsKnown("assembled_status"),
            "weight"         => Has("kg", "ton", "t"),
            "power_rating"   => Has("kW", "HP", "kVA", "W"),
            "voltage"        => Has("V", "kV"),
            _ => false
        };
    }

    public void Set(string type, string? value, FactSource source = FactSource.User,
        FactStatus status = FactStatus.Verified)
    {
        Facts[type] = new Fact
        {
            Type = type, RawValue = value, NormalizedValue = value,
            Source = source, Status = status
        };
    }
}

public class CandidateImpact
{
    public int CandidatesAffected { get; set; }
    public List<string> EliminatedCodes { get; set; } = new();
    public List<string> RetainedCodes { get; set; } = new();
}

/// <summary>A selectable answer; Value is what gets sent back as the fact value.</summary>
public record QuestionChoice(string Value, string Label);

public class ClassificationQuestion
{
    public string FactType { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public List<string>? Options { get; set; }
    /// <summary>Value/label options (e.g. official BTKI subheading texts → code prefix).</summary>
    public List<QuestionChoice>? Choices { get; set; }
    public double InformationGain { get; set; }
    public CandidateImpact Impact { get; set; } = new();
}

public class CandidateHeading
{
    public string Code { get; set; } = string.Empty;       // "8471"
    public string Description { get; set; } = string.Empty;
    public string? ChapterCode { get; set; }
    public string? SectionCode { get; set; }
    public string GenerationSource { get; set; } = "LEXICAL";
    public double RelevanceScore { get; set; } = 1.0;
    public string? SourceId { get; set; }
    // Enrichment (loaded by CandidateGenerator)
    public List<string> SectionNotesText { get; set; } = new();
    public List<string> ChapterNotesText { get; set; } = new();
    public List<string> EnParagraphs { get; set; } = new();
    public List<string> RequiredDiscriminators { get; set; } = new();
    public List<string> ChildSubheadings { get; set; } = new();
    /// <summary>Official text of intermediate levels, keyed by code prefix: "8429.5" (one-dash group), "8471.30" (subheading).</summary>
    public Dictionary<string, string> ParentDescriptions { get; set; } = new();
    /// <summary>True when Description is the official 4-digit heading text (not a child line).</summary>
    public bool HasHeadingText { get; set; }
    /// <summary>Official BTKI description per child code (from DB) — used by KUM HS 6.</summary>
    public Dictionary<string, string> ChildDescriptions { get; set; } = new();
}

public class ReasoningStep
{
    public string Rule { get; set; } = string.Empty;
    public string CandidateCode { get; set; } = string.Empty;
    public CandidateDecision Decision { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> SourceIds { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ClassificationResult
{
    public string? FinalCode { get; set; }
    public string? FinalDescription { get; set; }
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Low;
    public ResultStatus Status { get; set; } = ResultStatus.NeedClarification;
    public List<ReasoningStep> ReasoningChain { get; set; } = new();
    public List<CandidateHeading> EliminatedCandidates { get; set; } = new();
    public List<ClassificationQuestion> PendingQuestions { get; set; } = new();
    public List<string> MissingFacts { get; set; } = new();
    /// <summary>Tariff lines still in contention when KUM HS 6 could not decide (for expert review).</summary>
    public List<string> UnresolvedCodes { get; set; } = new();
    /// <summary>Heading locked for KUM HS 6 and how well its own text covered the facts (0..1).</summary>
    public string? LockedHeading { get; set; }
    public double LockedHeadingScore { get; set; }
}
