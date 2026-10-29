namespace HsClassifier.Domain.Enums;

public enum ImportState
{
    Uploaded, Parsing, Mapped, Validating,
    DryRunPassed, DryRunFailed, ReadyToCommit,
    Committed, Indexed, Active
}

public enum IngestionMethod { DocxParser, CsvTemplate, XlsxTemplate, PdfParser }
public enum RateUnit { Percent, Specific, Compound }
public enum TaxType { Ppn, Pph22 }
public enum EffectType { Definition, Inclusion, Exclusion, Condition, Referral, Priority }
public enum ParagraphType { Scope, Definition, Inclusion, Exclusion, Technical, Example, Borderline, CrossReference }
public enum ConfidenceLevel { High, Medium, Low }
public enum CaseStatus { Determined, NeedClarification, NeedExpertReview, Rejected }
public enum UserRole { Admin, Classifier, Viewer, ExpertReviewer }
public enum AmbiguityDecision { Pending, Approved, Rejected }
