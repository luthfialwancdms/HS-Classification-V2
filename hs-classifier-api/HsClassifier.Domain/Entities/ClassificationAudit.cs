using HsClassifier.Domain.Enums;
namespace HsClassifier.Domain.Entities;

public class ClassificationCase
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public Guid VersionId { get; set; }
    public string FactsJson { get; set; } = "{}";
    public string? FinalCode { get; set; }
    public string? FinalDescription { get; set; }
    /// <summary>Candidate codes the engine left open — what an expert chooses from later.</summary>
    public string? CandidateCodesJson { get; set; }
    public ConfidenceLevel? Confidence { get; set; }
    /// <summary>0–100 score from the weighted confidence model (null when no final code).</summary>
    public int? ConfidenceScore { get; set; }
    /// <summary>JSON: { components[], caps[], riskFlags[] } for audit and UI.</summary>
    public string? ConfidenceJson { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.NeedClarification;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ClassificationDate { get; set; } = DateTime.UtcNow;

    public ICollection<ReasoningEvent> ReasoningEvents { get; set; } = new List<ReasoningEvent>();
}

public class ReasoningEvent
{
    public long Id { get; set; }
    public long CaseId { get; set; }
    public int EventOrder { get; set; }
    public string RuleApplied { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? FactsUsedJson { get; set; }
    public string? CandidatesBeforeJson { get; set; }
    public string? CandidatesAfterJson { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public string? SourceIdsJson { get; set; }

    public ClassificationCase? Case { get; set; }
}

public class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Classifier;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

/// <summary>Immutable record of an expert decision on a case (approve / override / reject).</summary>
public class ExpertCorrection
{
    public long Id { get; set; }
    public long CaseId { get; set; }
    public string Action { get; set; } = string.Empty;   // EXPERT_APPROVED | EXPERT_OVERRIDE | EXPERT_REJECTED | REVIEW_REQUESTED
    public string? BeforeCode { get; set; }
    public string? AfterCode { get; set; }
    public string? BeforeStatus { get; set; }
    public string? AfterStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int ReviewerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One DOCX upload against a version: the persisted validation report.</summary>
public class ImportBatch
{
    public long Id { get; set; }
    public Guid VersionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public string ReportJson { get; set; } = "{}";
    public int TotalNodes { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AmbiguityItem> Items { get; set; } = new List<AmbiguityItem>();
}

/// <summary>A STRUCTURE_AMBIGUITY node awaiting an admin decision (Req 1.4, 1.11).</summary>
public class AmbiguityItem
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string? ProposedParent { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AmbiguityDecision Decision { get; set; } = AmbiguityDecision.Pending;
    public string? CorrectedParent { get; set; }
    public string? Note { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }

    public ImportBatch? Batch { get; set; }
}
