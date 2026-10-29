# Design Document — HS Classification V2

## Overview

### System Transformation

HS Classification V2 represents a fundamental architectural transformation from a simple LLM-based classifier to a legal-grade, database-centric classification system compliant with Indonesian BTKI 2022 and WCO Harmonized System GRI 1-6.

**V1 System (Current):**
- Architecture: Frontend-only React + Vite + Groq API
- Data: 105 hardcoded BTKI entries in JSON, no EN, no legal notes
- Classification: Direct LLM prediction from form input → HS code
- Risk: Hallucination, no audit trail, no source validation

**V2 System (Target):**
- Architecture: C# Backend + MySQL Database + React Frontend
- Data: 14,273 BTKI entries, 65,833 EN paragraphs, comprehensive legal notes
- Classification: Fact extraction → Database retrieval → Rules engine → Validation
- Guarantee: No LLM-generated codes/descriptions, complete audit trail, source ID traceability

### Core Principles

1. **Database as Single Source of Truth**: All HS codes, descriptions, and legal guidance must come from database, never from LLM memory
2. **Legal Compliance**: Sequential application of KUM HS 1-6 following WCO interpretation methodology
3. **Auditability**: Every classification decision traceable to source document location (file, page, paragraph)
4. **Validation-First**: No classification result released without exact code validation, parent hierarchy verification, and source ID confirmation

5. **Ask, Don't Guess**: System must request clarification when discriminator facts are missing rather than defaulting to generic codes

### Key Constraints

- LLM role: Extract facts, generate questions, apply reasoning logic — NEVER generate HS codes or descriptions
- All tariff codes: Retrieved from `btki_tariff_lines` table
- All official descriptions: Retrieved from database, never paraphrased
- All EN paragraphs: Retrieved from `explanatory_notes` table, never quoted from memory
- Validation: Reject any result where code doesn't exist, parent missing, or description mismatch
- Source traceability: Every decision must reference `source_id` linking to original Word document location

---

## Architecture

### System Context Diagram

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         HS Classification V2 System                       │
│                                                                            │
│  ┌─────────────────┐          ┌──────────────────────────────────────┐   │
│  │  React Frontend │◄────────►│         C# Backend API               │   │
│  │  (Existing V1)  │  HTTPS   │                                      │   │
│  └─────────────────┘          │  ┌────────────────────────────────┐  │   │
│                                │  │  Admin_Data_Center             │  │   │
│  │  │  - DOCX Parser (OOXML)           │  │   │
│  │  │  - Structure Detector              │  │   │
│  │  │  - Validation Engine                │  │   │
│  │  │  - Version Manager                  │  │   │
│  │  └────────────────────────────────┘  │   │
│  │                                      │   │
│  │  ┌────────────────────────────────┐  │   │
│  │  │  Classification Engine          │  │   │
│  │  │  - Fact_Engine                  │  │   │
│  │  │  - Candidate_Generator          │  │   │
│  │  │  - Rules_Engine (KUM HS 1-6)    │  │   │
│  │  │  - Validator                    │  │   │
│  │  │  - Tariff_Lookup_Module         │  │   │
│  │  └────────────────────────────────┘  │   │
│  │                 │                     │   │
│  │                 ▼                     │   │
│  │  ┌────────────────────────────────┐  │   │
│  │  │       MySQL Database 8.0+       │  │   │
│  │  │                                 │  │   │
│  │  │  - btki_tariff_lines (14,273)  │  │   │
│  │  │  - explanatory_notes (65,833)  │  │   │
│  │  │  - section_notes                │  │   │
│  │  │  - chapter_notes                │  │   │
│  │  │  - subheading_notes             │  │   │
│  │  │  - nomenclature_versions        │  │   │
│  │  │  - classification_cases         │  │   │
│  │  │  - reasoning_events (audit)     │  │   │
│  │  └────────────────────────────────┘  │   │
│  └──────────────────────────────────────┘   │
└──────────────────────────────────────────────┘
```

### Component Interaction Flow

```
User Input (Frontend)
        │
        ▼
[1. Fact_Engine]
   - Extract objective facts
   - Identify missing discriminators
   - Generate targeted questions
        │
        ▼
[2. Candidate_Generator]
   - Lexical search BTKI database
   - Semantic similarity (embeddings)
   - Function/material-based search
   - Retrieve: code + description + notes + EN
        │
        ▼
[3. Rules_Engine]
   - Apply KUM HS 1 (heading + notes + EN)
   - Apply KUM HS 2(a) incomplete goods
   - Apply KUM HS 2(b) material mixtures
   - Apply KUM HS 3(a) specific vs general
   - Apply KUM HS 3(b) essential character
   - Apply KUM HS 3(c) last numerical
   - Apply KUM HS 4 most akin (rare)
   - Apply KUM HS 5 containers/packing
   - Apply KUM HS 6 subheading descent
        │
        ▼
[4. Validator]
   - Verify code exists in database
   - Verify parent hierarchy complete
   - Verify description exact match
   - Verify source_id references valid
   - Reject if any validation fails
        │
        ▼
[5. Tariff_Lookup_Module]
   - Retrieve MFN duty rate
   - Retrieve FTA rates (reference only)
   - Retrieve PPN and PPh 22 rates
        │
        ▼
Classification Result + Audit Trail
```

---

## Components and Interfaces

### 1. Admin Data Center

**Responsibility**: Import, parse, validate, and activate BTKI/EN/Notes datasets from Microsoft Word DOCX files.

#### 1.1 DOCX Parser Module

```csharp
public interface IDocxParser
{
    Task<ParseResult> ParseBTKI(Stream docxStream, string fileName);
    Task<ParseResult> ParseEN(Stream docxStream, string fileName, string edition);
    Task<ParseResult> ParseLegalNotes(Stream docxStream, string fileName);
}

public class ParseResult
{
    public string SourceFileName { get; set; }
    public string SourceChecksum { get; set; }
    public List<TariffNode> TariffNodes { get; set; }
    public List<ENParagraph> ENParagraphs { get; set; }
    public List<LegalNote> LegalNotes { get; set; }
    public List<ValidationIssue> Issues { get; set; }
    public ParseStatistics Statistics { get; set; }
}

public class TariffNode
{
    public string Code { get; set; }  // "8471.30.20"
    public string DescriptionID { get; set; }
    public string DescriptionEN { get; set; }
    public int IndentLevel { get; set; }
    public string ParentCode { get; set; }  // derived from structure
    public SourceLocation SourceLocation { get; set; }
    public TariffRates Rates { get; set; }
}

public class SourceLocation
{
    public string FileChecksum { get; set; }
    public string DocumentPart { get; set; }  // "body", "table_5"
    public int PageNumber { get; set; }
    public int ParagraphIndex { get; set; }
    public int? TableIndex { get; set; }
    public int? RowIndex { get; set; }
    public int? CellIndex { get; set; }
    public string OOXMLElementId { get; set; }
}
```

**Parsing Strategy:**

1. **OOXML Structure Analysis**: Parse `document.xml`, detect paragraph styles (`w:pStyle`), outline levels (`w:outlineLvl`), numbering (`w:numPr`), indentation (`w:ind`), tables (`w:tbl`)
2. **Hierarchy Detection**: Build parent-child relationships from indentation levels and numbering sequences
3. **Structure Signals**: 
   - Indentation increase → child node
   - Bold + larger font → heading
   - Table structure → tariff lines with rates
   - Footnote references → capture for reconciliation
4. **Ambiguity Flagging**: When parent cannot be determined → flag `STRUCTURE_AMBIGUITY`, place in review queue
5. **Source Mapping**: Store exact location (page, paragraph index, table cell) for every extracted node

#### 1.2 Structure Validator

```csharp
public interface IStructureValidator
{
    ValidationReport ValidateHierarchy(List<TariffNode> nodes);
    ValidationReport ValidateCodes(List<TariffNode> nodes);
    ValidationReport ValidateCompleteness(List<TariffNode> nodes, ExpectedCounts expected);
}

public class ValidationIssue
{
    public IssueSeverity Severity { get; set; }  // ERROR, WARNING, INFO
    public IssueType Type { get; set; }
    public string Code { get; set; }
    public string Message { get; set; }
    public SourceLocation Location { get; set; }
}

public enum IssueType
{
    DUPLICATE_CODE,
    INVALID_CODE_FORMAT,
    MISSING_PARENT,
    ORPHAN_NODE,
    INCONSISTENT_INDENTATION,
    OVERLAPPING_DATES,
    EMPTY_DESCRIPTION,
    STRUCTURE_AMBIGUITY
}
```

**Validation Rules:**

- Code format: Must match `^\d{4}\.\d{2}\.\d{2}$` for 8-digit BTKI
- Parent existence: For code `8471.30.20`, parents `8471.30`, `8471`, `84` must exist
- No duplicates: Each code appears exactly once per version
- No orphans: Every non-section node must have valid parent
- Date consistency: No overlapping effective date ranges for same code
- Description non-empty: Indonesian description required, English optional

#### 1.3 Template Ingestion Module (CSV/Excel Alternative Path)

**Responsibility**: Accept BTKI data via standardized CSV/Excel template as an alternative to DOCX parsing, supporting manual corrections and partial updates.

**Template Columns (CSV & Excel)**:

| Column | Type | Required | Description |
|--------|------|----------|-------------|
| `code` | string | ✓ | 8-digit HS code format XXXX.XX.XX |
| `description_id` | string | ✓ | Official description in Indonesian |
| `description_en` | string | - | Official description in English |
| `parent_code` | string | ✓ | Parent HS code (e.g. 8471.30 for 8471.30.20) |
| `indent_level` | int | ✓ | Hierarchy depth (0=section, 1=chapter, 2=heading, 3=subheading, 4=tariff line) |
| `mfn_rate` | decimal | - | MFN duty rate value |
| `mfn_rate_unit` | string | - | PERCENT / SPECIFIC / COMPOUND |
| `ppn_rate` | decimal | - | PPN import rate |
| `pph22_rate` | decimal | - | PPh 22 import rate |
| `effective_from` | date | ✓ | Format: YYYY-MM-DD |
| `effective_to` | date | - | Format: YYYY-MM-DD, null = no expiry |
| `status` | char | ✓ | A = active, I = inactive |

**Interface**:

```csharp
public interface ITemplateIngestion
{
    byte[] GenerateCSVTemplate();
    byte[] GenerateExcelTemplate();
    Task<TemplateValidationResult> ValidateTemplate(Stream fileStream, string fileName);
    Task<TemplatePreview> PreviewImport(TemplateValidationResult validated, Guid versionId);
    Task<ImportResult> CommitImport(TemplatePreview preview, Guid versionId);
}

public class TemplateValidationResult
{
    public bool IsValid { get; set; }
    public List<TemplateRowError> Errors { get; set; }    // ERROR = reject all
    public List<TemplateRowError> Warnings { get; set; }  // WARNING = require ack
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
}

public class TemplateRowError
{
    public int RowNumber { get; set; }
    public string ColumnName { get; set; }
    public string ErrorType { get; set; }  // INVALID_FORMAT, MISSING_REQUIRED, INVALID_PARENT, INVALID_DATE
    public string ActualValue { get; set; }
    public string SuggestedFix { get; set; }
}

public class TemplatePreview
{
    public List<TariffNode> ToInsert { get; set; }    // new codes
    public List<TariffNodeDiff> ToUpdate { get; set; }  // existing codes with changes
    public List<TariffNode> Unchanged { get; set; }     // no change, will be skipped
    public int InsertCount { get; set; }
    public int UpdateCount { get; set; }
    public int SkipCount { get; set; }
}
```

**Source ID Format for Template Uploads**:
```
template:upload:{filename_checksum}:{row_number}
```
Example: `template:upload:a3f8c2d1:row_847`

**Ingestion Method Tracking**:
- `btki_tariff_lines.ingestion_method` column: `DOCX_PARSER` | `CSV_TEMPLATE` | `XLSX_TEMPLATE`
- Allows audit of which entries came from which source
- Template entries can be overridden by subsequent DOCX parse (DOCX takes precedence)

**Admin API Endpoints**:
```
GET  /api/admin/template/download/csv    → download blank CSV template
GET  /api/admin/template/download/xlsx   → download blank Excel template
POST /api/admin/template/validate        → validate uploaded file, return errors
POST /api/admin/template/preview         → return INSERT/UPDATE/SKIP preview
POST /api/admin/template/commit          → commit after admin confirms preview
```

---

#### 1.4 Version Manager

```csharp
public interface IVersionManager
{
    Task<Guid> CreateVersion(NomenclatureVersionMetadata metadata);
    Task<ImportState> GetImportState(Guid versionId);
    Task<bool> TransitionState(Guid versionId, ImportState newState);
    Task<bool> ActivateVersion(Guid versionId);
    Task<bool> DeactivateVersion(Guid versionId);
    Task<NomenclatureVersion> GetActiveVersion(DateTime effectiveDate);
    Task<VersionComparison> CompareVersions(Guid versionIdA, Guid versionIdB);
}

public enum ImportState
{
    UPLOADED,
    PARSING,
    MAPPED,
    VALIDATING,
    DRY_RUN_PASSED,
    DRY_RUN_FAILED,
    READY_TO_COMMIT,
    COMMITTED,
    INDEXED,
    ACTIVE
}
```

**State Machine:**

```
UPLOADED → PARSING → MAPPED → VALIDATING → {DRY_RUN_PASSED, DRY_RUN_FAILED}
                                               │
                                               ▼
                                         READY_TO_COMMIT
                                               │
                                               ▼
                                          COMMITTED
                                               │
                                               ▼
                                           INDEXED
                                               │
                                               ▼
                                           ACTIVE
```

**Dry Run Validation**: Before allowing COMMIT, system executes comprehensive checks:
- All parent codes exist
- No duplicate codes
- No circular references
- Date ranges don't overlap with existing active versions
- Total counts match expected (14,273 tariff lines for BTKI 2022)

**Activation Rules**:
- Only one version can be ACTIVE per effective date range
- Activating new version automatically deactivates conflicting versions
- Historical classification records preserve reference to version_id used

---

### 2. Fact Extraction Engine

**Responsibility**: Extract objective, verifiable facts from user input and documents; generate targeted questions for missing discriminator facts.

#### 2.1 Fact Extractor

```csharp
public interface IFactExtractor
{
    Task<FactSet> ExtractFacts(ClassificationInput input);
}

public class Fact
{
    public string Type { get; set; }  // "material", "primary_function", "weight", etc.
    public string RawValue { get; set; }
    public string NormalizedValue { get; set; }
    public string Unit { get; set; }
    public FactSource Source { get; set; }
    public string EvidenceReference { get; set; }
    public double ConfidenceScore { get; set; }
    public FactStatus Status { get; set; }
}

public enum FactStatus
{
    UNKNOWN,          // Not provided
    NOT_APPLICABLE,   // User confirmed not relevant
    ZERO,             // Value is actually zero
    CONFLICTING,      // Multiple different values detected
    VERIFIED          // Confirmed by user/document
}
```

**35 Core Fact Types**:

| Category | Fact Types |
|----------|------------|
| Identity | commercial_name, generic_name, brand, model |
| Material | material, composition, percentage, concentration |
| Physical | form, dimensions, weight, capacity, power_rating, voltage |
| Process | manufacturing_process, finishing_process |
| Function | primary_function, secondary_function, operating_principle, input_process_output |
| Power | power_source, fuel_type, energy_rating |
| Use | primary_use, operating_area, mobility, mounting |
| Condition | complete_status, finished_status, assembled_status, CKD_SKD_status |
| Presentation | bulk_unit, mixture_composite, set_kit, part_accessory |
| State | used_condition, waste_scrap, packaging_container |
| Evidence | supporting_documents |

**LLM Role in Fact Extraction**:
- Use LLM to identify fact mentions in free-text descriptions
- Normalize units (e.g., "1 ton" → 1000 kg)
- Detect conflicting statements
- **LLM must NOT infer missing facts** — if weight not mentioned, status = UNKNOWN

#### 2.2 Dynamic Question Generator

```csharp
public interface IQuestionGenerator
{
    Task<QuestionSet> GenerateQuestions(FactSet facts, List<CandidateHeading> candidates);
}

public class Question
{
    public string FactType { get; set; }
    public string QuestionText { get; set; }
    public List<string> Options { get; set; }  // null for free text
    public double InformationGain { get; set; }
    public CandidateImpact Impact { get; set; }
}

public class CandidateImpact
{
    public int CandidatesAffected { get; set; }
    public List<string> EliminatedCodes { get; set; }  // if answer is X
    public List<string> RetainedCodes { get; set; }
}
```

**Question Prioritization Algorithm**:

1. Calculate information gain for each missing fact:
   ```
   InformationGain = (CandidatesEliminated / TotalCandidates) × FactReliability
   ```

2. Sort questions by InformationGain descending

3. Ask highest-gain question first

4. After answer, recalculate gains for remaining questions (candidates may have changed)

**Example**:
- Candidates: 8471.30 (portable ADP), 8471.50 (processing unit), 8471.60 (input unit)
- Missing facts: weight, has_integrated_keyboard
- Information gain:
  - `has_integrated_keyboard`: eliminates 2/3 candidates → gain = 0.67
  - `weight`: eliminates 1/3 candidates (if >10kg) → gain = 0.33
- Ask first: "Does the unit have an integrated keyboard?"

---

### 3. Candidate Generation Engine

**Responsibility**: Retrieve candidate HS codes from database using multiple search strategies; enrich with notes, EN, and discriminator requirements.

#### 3.1 Search Strategies

```csharp
public interface ICandidateGenerator
{
    Task<List<CandidateHeading>> GenerateCandidates(FactSet facts, Guid versionId);
}

public class CandidateHeading
{
    public string Code { get; set; }  // "8471" (4-digit heading)
    public string Description { get; set; }
    public string ParentChapter { get; set; }
    public string ParentSection { get; set; }
    public GenerationSource Source { get; set; }
    public double RelevanceScore { get; set; }
    
    // Legal context
    public List<SectionNote> ApplicableSectionNotes { get; set; }
    public List<ChapterNote> ApplicableChapterNotes { get; set; }
    public List<ENParagraph> ENParagraphs { get; set; }
    public List<string> RequiredDiscriminators { get; set; }
    
    // Source traceability
    public string SourceId { get; set; }
}
```

**Multi-Strategy Search**:

1. **Exact Lexical Match**: 
   ```sql
   SELECT * FROM btki_tariff_lines 
   WHERE description_id LIKE '%{genericName}%' OR description_en LIKE '%{genericName}%'
   ```

2. **Synonym/Terminology Mapping**:
   - Map user term → standard HS term (e.g., "laptop" → "automatic data processing machine portable")
   - Use pre-built terminology dictionary

3. **Multilingual Search**:
   - Search both Indonesian (`uraian_id`) and English (`uraian_en`) descriptions

4. **Semantic Similarity** (Phase 2):
   - Embed heading descriptions using Sentence Transformers
   - Store embeddings in `btki_embeddings` table
   - Cosine similarity search for semantic matches

5. **Function-Based Search**:
   - Extract `primary_function` fact → search headings by function keywords
   - Example: `primary_function = "lifting"` → candidates include 84.26 (cranes), 84.27 (forklift), 89.05 (floating cranes)

6. **Material-Based Search**:
   - Extract `material` facts → search headings by material
   - Example: `material = "steel"` → candidates include Chapter 72 (iron/steel), 73 (articles of iron/steel)

7. **Legal Note Referral**:
   - Check if Section/Chapter Notes refer goods to specific headings
   - Example: Chapter 84 Note 5 defines "automatic data processing machines" → refers to 84.71

8. **EN Cross-Reference**:
   - Parse EN "see also" and "excluded from this heading" statements
   - Retrieve referred headings as candidates

**Candidate Enrichment**: For each candidate, retrieve:
- Section Notes applicable to parent section
- Chapter Notes applicable to parent chapter
- Notes explicitly targeting that heading (Chapter Note "for purposes of heading 84.71...")
- All EN paragraphs for that heading
- Child subheadings (for later KUM HS 6 application)

---

### 4. Rules Engine (KUM HS 1-6)

**Responsibility**: Apply General Rules for Interpretation sequentially; eliminate candidates that don't meet legal criteria; lock heading and descend to subheading.

#### 4.1 KUM HS Application Interface

```csharp
public interface IRulesEngine
{
    Task<RulesResult> ApplyKUMHS(FactSet facts, List<CandidateHeading> candidates);
}

public class RulesResult
{
    public string FinalCode { get; set; }  // null if cannot determine
    public string FinalDescription { get; set; }
    public ConfidenceLevel Confidence { get; set; }
    public List<ReasoningEvent> ReasoningChain { get; set; }
    public List<EliminatedCandidate> Eliminated { get; set; }
    public ResultStatus Status { get; set; }  // DETERMINED, NEED_CLARIFICATION, NEED_EXPERT_REVIEW
    public List<string> MissingFacts { get; set; }
}

public class ReasoningEvent
{
    public string Rule { get; set; }  // "KUM_HS_1", "KUM_HS_3A", etc.
    public DateTime Timestamp { get; set; }
    public FactSet FactsUsed { get; set; }
    public List<string> CandidatesBefore { get; set; }
    public List<string> CandidatesAfter { get; set; }
    public string Decision { get; set; }  // RETAINED, ELIMINATED, UNRESOLVED
    public string Reasoning { get; set; }
    public List<string> SourceIds { get; set; }  // References to notes, EN paragraphs
}
```

#### 4.2 KUM HS 1: Heading + Notes + EN

**Algorithm**:

```csharp
public async Task<KumHs1Result> ApplyKumHs1(FactSet facts, CandidateHeading candidate)
{
    var result = new KumHs1Result { Candidate = candidate.Code };
    
    // Step 1: Match heading description terms
    var headingMatch = await MatchHeadingTerms(facts, candidate.Description);
    result.HeadingMatchScore = headingMatch.Score;
    result.MatchedFacts = headingMatch.MatchedFacts;
    result.MissingFacts = headingMatch.MissingFacts;
    
    // Step 2: Apply Section Notes
    foreach (var note in candidate.ApplicableSectionNotes)
    {
        var noteResult = await ApplyNote(facts, note);
        result.NoteResults.Add(noteResult);
        
        if (noteResult.Effect == NoteEffect.EXCLUDE)
        {
            result.Decision = CandidateDecision.ELIMINATED;
            result.Reason = $"Excluded by Section {note.SectionCode} Note {note.NoteNumber}";
            result.SourceIds.Add(note.SourceId);
            return result;
        }
    }
    
    // Step 3: Apply Chapter Notes
    foreach (var note in candidate.ApplicableChapterNotes)
    {
        var noteResult = await ApplyNote(facts, note);
        result.NoteResults.Add(noteResult);
        
        if (noteResult.Effect == NoteEffect.EXCLUDE)
        {
            result.Decision = CandidateDecision.ELIMINATED;
            result.Reason = $"Excluded by Chapter {note.ChapterCode} Note {note.NoteNumber}";
            result.SourceIds.Add(note.SourceId);
            return result;
        }
        else if (noteResult.Effect == NoteEffect.REFER)
        {
            result.ReferredToHeading = noteResult.ReferredCode;
            result.SourceIds.Add(note.SourceId);
        }
    }
    
    // Step 4: Use EN for interpretation
    foreach (var en in candidate.ENParagraphs)
    {
        if (en.Type == ENType.EXCLUSION)
        {
            var exclusionMatch = await MatchExclusion(facts, en.Text);
            if (exclusionMatch.IsMatch)
            {
                result.Decision = CandidateDecision.ELIMINATED;
                result.Reason = $"Excluded by EN: {en.Text.Substring(0, 100)}...";
                result.SourceIds.Add(en.SourceId);
                return result;
            }
        }
        else if (en.Type == ENType.INCLUSION)
        {
            var inclusionMatch = await MatchInclusion(facts, en.Text);
            if (inclusionMatch.IsMatch)
            {
                result.InclusionEvidence.Add($"EN confirms: {en.Text.Substring(0, 100)}...");
                result.SourceIds.Add(en.SourceId);
            }
        }
    }
    
    // Step 5: Determine if candidate retained
    if (result.HeadingMatchScore > 0.7 && result.Decision != CandidateDecision.ELIMINATED)
    {
        result.Decision = CandidateDecision.RETAINED;
        result.Reason = "Facts match heading description and no exclusions apply";
    }
    else if (result.MissingFacts.Count > 0)
    {
        result.Decision = CandidateDecision.UNRESOLVED;
        result.Reason = $"Missing discriminator facts: {string.Join(", ", result.MissingFacts)}";
    }
    else
    {
        result.Decision = CandidateDecision.ELIMINATED;
        result.Reason = "Heading description does not match facts";
    }
    
    return result;
}
```

**Key Logic**:
- If exactly 1 heading retained after KUM HS 1 → proceed to KUM HS 6 (subheading)
- If 0 headings retained → status = NEED_EXPERT_REVIEW
- If 2+ headings retained → proceed to KUM HS 2

#### 4.3 KUM HS 2(a): Incomplete/Unfinished Goods

**Trigger**: `assembled_status = "CKD"` OR `assembled_status = "SKD"` OR `complete_status = "incomplete"` OR `finished_status = "unfinished"`

**Algorithm**:

```csharp
public async Task<KumHs2aResult> ApplyKumHs2a(FactSet facts, List<CandidateHeading> candidates)
{
    var result = new KumHs2aResult();
    
    // Check if trigger applies
    if (!IsIncompleteOrUnfinished(facts))
    {
        result.Applied = false;
        result.Reason = "KUM HS 2(a) not triggered - goods are complete/finished";
        return result;
    }
    
    result.Applied = true;
    
    // Determine if goods have essential character of complete article
    var essentialCharacter = await EvaluateEssentialCharacter(facts);
    
    if (essentialCharacter.HasEssentialCharacter)
    {
        result.Decision = "TREAT_AS_COMPLETE";
        result.Reason = $"Goods have essential character of {essentialCharacter.CompleteArticleName}";
        result.CompleteArticleCode = essentialCharacter.CompleteArticleCode;
        result.Evidence = essentialCharacter.Evidence;
    }
    else if (essentialCharacter.CannotDetermine)
    {
        result.Decision = "NEED_CLARIFICATION";
        result.MissingFacts = essentialCharacter.MissingFacts;
        result.Questions = GenerateEssentialCharacterQuestions(facts, candidates);
    }
    else
    {
        result.Decision = "CLASSIFY_AS_PRESENTED";
        result.Reason = "Goods lack essential character - classify as parts/components";
    }
    
    return result;
}
```

**Essential Character Evaluation Factors**:
- Missing components: how many, which critical functions affected
- Manufacturing processes remaining: assembly only vs substantial manufacturing
- Function availability: can primary function already operate
- Identity recognition: can complete article be recognized from presented components
- All components together: are all parts presented in same shipment

**Example Questions Generated**:
- "Which components are missing from the complete machine?"
- "Is the primary function already operational with the components presented?"
- "What further work is required after import?"
- "Are all components/parts presented together?"

#### 4.4 KUM HS 3(a): Specific vs General

**Trigger**: 2+ headings retained after KUM HS 1 and 2

**Algorithm**:

```csharp
public async Task<KumHs3aResult> ApplyKumHs3a(List<CandidateHeading> candidates, FactSet facts)
{
    var result = new KumHs3aResult();
    
    if (candidates.Count < 2)
    {
        result.Applied = false;
        return result;
    }
    
    result.Applied = true;
    
    // Compare specificity
    var specificityScores = new Dictionary<string, double>();
    
    foreach (var candidate in candidates)
    {
        var score = await CalculateSpecificity(candidate, facts);
        specificityScores[candidate.Code] = score;
    }
    
    var maxScore = specificityScores.Values.Max();
    var mostSpecific = specificityScores.Where(kvp => kvp.Value == maxScore).ToList();
    
    if (mostSpecific.Count == 1)
    {
        result.Decision = "HEADING_SELECTED";
        result.SelectedHeading = mostSpecific[0].Key;
        result.Reason = $"Heading {result.SelectedHeading} is most specific";
        result.SpecificityComparison = BuildComparisonEvidence(specificityScores, candidates);
        return result;
    }
    else
    {
        result.Decision = "PROCEED_TO_3B";
        result.Reason = "Multiple headings equally specific - essential character required";
        result.EquallySpecificHeadings = mostSpecific.Select(kvp => kvp.Key).ToList();
        return result;
    }
}
```

**Specificity Scoring**:
```csharp
private async Task<double> CalculateSpecificity(CandidateHeading candidate, FactSet facts)
{
    double score = 0.0;
    
    // Specific mention vs general category
    if (IsSpecificArticle(candidate.Description))
        score += 1.0;
    else if (IsResidualHeading(candidate.Description)) // "other", "not elsewhere specified"
        score -= 1.0;
    
    // Complete article vs parts
    if (facts.Get("part_accessory")?.NormalizedValue == "complete_article")
        score += 0.5;
    else if (candidate.Description.Contains("parts"))
        score -= 0.5;
    
    // Material/component coverage
    var factMaterialCount = facts.GetAll("material").Count;
    var descriptionMaterialCount = CountMaterialMentions(candidate.Description);
    
    if (descriptionMaterialCount == factMaterialCount)
        score += 0.5;  // covers all materials
    else if (descriptionMaterialCount < factMaterialCount)
        score -= 0.3;  // partial coverage
    
    return score;
}
```

#### 4.5 KUM HS 3(b): Essential Character

**Trigger**: KUM HS 3(a) resulted in equally specific headings OR goods are mixture/composite/set

**Algorithm**:

```csharp
public async Task<KumHs3bResult> ApplyKumHs3b(List<CandidateHeading> candidates, FactSet facts)
{
    var result = new KumHs3bResult { Applied = true };
    
    // Verify retail set criteria if applicable
    if (facts.Get("set_kit")?.NormalizedValue == "retail_set")
    {
        var retailSetValid = await ValidateRetailSet(facts);
        if (!retailSetValid.IsValid)
        {
            result.Decision = "NOT_A_SET";
            result.Reason = retailSetValid.Reason;
            return result;
        }
    }
    
    // Evaluate essential character factors
    var ecAnalysis = await EvaluateEssentialCharacter(facts, candidates);
    
    if (ecAnalysis.DominantComponent != null)
    {
        result.Decision = "HEADING_SELECTED";
        result.SelectedHeading = ecAnalysis.DominantComponent.Heading;
        result.Reason = $"Component '{ecAnalysis.DominantComponent.Name}' confers essential character";
        result.FactorsConsidered = ecAnalysis.Factors;
        result.Evidence = ecAnalysis.Evidence;
    }
    else if (ecAnalysis.CannotDetermine)
    {
        result.Decision = "PROCEED_TO_3C";
        result.Reason = "Essential character cannot be determined - apply last in numerical order";
    }
    else
    {
        result.Decision = "NEED_CLARIFICATION";
        result.MissingFacts = ecAnalysis.MissingFacts;
    }
    
    return result;
}
```

**Essential Character Factors**:

| Factor | Weight | Evidence Source |
|--------|--------|-----------------|
| Function/Role | 0.3 | `primary_function` fact + component functional analysis |
| Material | 0.15 | `material` facts + composition percentage |
| Bulk/Quantity | 0.15 | `weight`, `dimensions` facts |
| Value | 0.15 | `component_value` facts (if available) |
| Necessity | 0.15 | Whether goods can function without component |
| Consumer Perception | 0.1 | LLM analysis of what consumer considers main item |

**No single factor automatically determinative** — system must weigh multiple factors with evidence from facts.

#### 4.6 KUM HS 6: Subheading Descent

**Trigger**: 4-digit heading locked after KUM HS 1-5

**Algorithm**:

```csharp
public async Task<KumHs6Result> ApplyKumHs6(string lockedHeading, FactSet facts, Guid versionId)
{
    var result = new KumHs6Result { LockedHeading = lockedHeading };
    
    // Retrieve child subheadings from database
    var subheadings = await _btki.GetChildSubheadings(lockedHeading, versionId);
    
    // Group by dash level (1-dash, 2-dash, 3-dash for 8-digit)
    var level1 = subheadings.Where(s => s.DashLevel == 1).ToList();  // 6-digit
    
    // Apply KUM HS 1-5 at subheading level
    var level1Retained = new List<string>();
    foreach (var sub in level1)
    {
        var subResult = await ApplyKumHsAtSubheadingLevel(sub, facts);
        if (subResult.Decision == CandidateDecision.RETAINED)
            level1Retained.Add(sub.Code);
    }
    
    if (level1Retained.Count == 0)
    {
        result.Status = "NO_SUBHEADING_MATCH";
        result.MissingDiscriminators = await IdentifyMissingDiscriminators(level1, facts);
        return result;
    }
    else if (level1Retained.Count == 1)
    {
        // Descend to level 2 (8-digit)
        var level2 = await _btki.GetChildSubheadings(level1Retained[0], versionId);
        var finalCode = await SelectLevel2(level2, facts);
        
        if (finalCode != null)
        {
            result.FinalCode = finalCode;
            result.Status = "DETERMINED";
        }
        else
        {
            result.Status = "NEED_CLARIFICATION";
            result.MissingDiscriminators = await IdentifyMissingDiscriminators(level2, facts);
        }
    }
    else
    {
        // Multiple 6-digit retained - apply discrimination
        var discriminated = await DiscriminateSubheadings(level1Retained, facts);
        result.FinalCode = discriminated.SelectedCode;
        result.Status = discriminated.Status;
    }
    
    return result;
}
```

**Subheading Discriminators**:

Common discriminator types in BTKI subheadings:
- Weight threshold: "not exceeding 5 tonnes", "exceeding 10 kg"
- Capacity: "of a capacity not exceeding 1000cc"
- Power: "of an output not exceeding 100 kW"
- Size: "with screen size exceeding 15 inches"
- Composition: "containing by weight 80% or more of copper"
- Concentration: "containing by weight more than 0.5% of carbon"
- Retail vs bulk: "put up for retail sale" vs "in bulk"
- CKD vs non-CKD: "other than CKD" vs "CKD"
- Use: "for agricultural use" vs "for industrial use"

**When discriminator fact missing**:
- Status = NEED_CLARIFICATION
- Generate question: "What is the [weight/capacity/power/etc] of the unit?"
- Show candidate impact: "If ≤5 tonnes → 8704.10.31; if >5 tonnes → 8704.10.39"
- **DO NOT default to .00 generic code**

---

### 5. Validation System

**Responsibility**: Verify final HS code against database; reject hallucinated or invalid results.

#### 5.1 Validator Interface

```csharp
public interface IValidator
{
    Task<ValidationResult> ValidateClassification(RulesResult rulesResult, Guid versionId);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationFailure> Failures { get; set; }
    public TariffLine ValidatedRecord { get; set; }  // from database
}

public class ValidationFailure
{
    public ValidationFailureType Type { get; set; }
    public string Message { get; set; }
    public string FailedValue { get; set; }
    public string ExpectedValue { get; set; }
}

public enum ValidationFailureType
{
    CODE_NOT_FOUND,
    INVALID_FORMAT,
    PARENT_MISSING,
    DESCRIPTION_MISMATCH,
    INACTIVE_CODE,
    SOURCE_ID_INVALID
}
```

#### 5.2 Validation Checks

```csharp
public async Task<ValidationResult> ValidateClassification(RulesResult rulesResult, Guid versionId)
{
    var result = new ValidationResult { IsValid = true, Failures = new List<ValidationFailure>() };
    
    // Check 1: Code exists in database
    var record = await _btki.GetTariffLine(rulesResult.FinalCode, versionId);
    if (record == null)
    {
        result.IsValid = false;
        result.Failures.Add(new ValidationFailure
        {
            Type = ValidationFailureType.CODE_NOT_FOUND,
            Message = $"Code {rulesResult.FinalCode} does not exist in active nomenclature version",
            FailedValue = rulesResult.FinalCode
        });
        return result;
    }
    
    // Check 2: Format validation
    if (!Regex.IsMatch(rulesResult.FinalCode, @"^\d{4}\.\d{2}\.\d{2}$"))
    {
        result.IsValid = false;
        result.Failures.Add(new ValidationFailure
        {
            Type = ValidationFailureType.INVALID_FORMAT,
            Message = "Code must be 8-digit BTKI format (XXXX.XX.XX)",
            FailedValue = rulesResult.FinalCode,
            ExpectedValue = "XXXX.XX.XX"
        });
    }
    
    // Check 3: Parent hierarchy exists
    var parents = GetParentCodes(rulesResult.FinalCode);
    foreach (var parentCode in parents)
    {
        var parentExists = await _btki.CodeExists(parentCode, versionId);
        if (!parentExists)
        {
            result.IsValid = false;
            result.Failures.Add(new ValidationFailure
            {
                Type = ValidationFailureType.PARENT_MISSING,
                Message = $"Parent code {parentCode} does not exist",
                FailedValue = parentCode
            });
        }
    }
    
    // Check 4: Description exact match
    if (rulesResult.FinalDescription != record.DescriptionID)
    {
        result.IsValid = false;
        result.Failures.Add(new ValidationFailure
        {
            Type = ValidationFailureType.DESCRIPTION_MISMATCH,
            Message = "Description does not match database record",
            FailedValue = rulesResult.FinalDescription,
            ExpectedValue = record.DescriptionID
        });
    }
    
    // Check 5: Code active on classification date
    if (!record.IsActive || record.EffectiveFrom > DateTime.Now || record.EffectiveTo < DateTime.Now)
    {
        result.IsValid = false;
        result.Failures.Add(new ValidationFailure
        {
            Type = ValidationFailureType.INACTIVE_CODE,
            Message = $"Code is not active on classification date",
            FailedValue = rulesResult.FinalCode
        });
    }
    
    // Check 6: Source IDs valid
    foreach (var sourceId in rulesResult.ReasoningChain.SelectMany(r => r.SourceIds))
    {
        var sourceValid = await _sources.ValidateSourceId(sourceId, versionId);
        if (!sourceValid)
        {
            result.IsValid = false;
            result.Failures.Add(new ValidationFailure
            {
                Type = ValidationFailureType.SOURCE_ID_INVALID,
                Message = $"Source ID {sourceId} does not reference valid database record",
                FailedValue = sourceId
            });
        }
    }
    
    result.ValidatedRecord = record;
    return result;
}
```

**Rejection Policy**: If `ValidationResult.IsValid == false`, system MUST NOT present result to user. Return error to Rules Engine for correction.

---

### 6. Tariff Lookup Module

**Responsibility**: Retrieve duty and tax rates from database after validation passes.

#### 6.1 Tariff Lookup Interface

```csharp
public interface ITariffLookupModule
{
    Task<TariffRates> GetRates(string code, Guid versionId);
}

public class TariffRates
{
    public string Code { get; set; }
    public MFNRate MFN { get; set; }
    public List<FTARate> FTASchemes { get; set; }
    public TaxRate PPN { get; set; }
    public TaxRate PPh22 { get; set; }
    public string SourceId { get; set; }
}

public class MFNRate
{
    public decimal Value { get; set; }
    public RateUnit Unit { get; set; }  // PERCENT, SPECIFIC, COMPOUND
    public string Currency { get; set; }  // for SPECIFIC
    public string QuantityUnit { get; set; }  // for SPECIFIC
    public string Formula { get; set; }  // for COMPOUND
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    public string SourceId { get; set; }
}

public class FTARate
{
    public string SchemeCode { get; set; }  // "ACFTA", "AKFTA", "IJEPA", etc.
    public string PartnerCountry { get; set; }
    public decimal Value { get; set; }
    public RateUnit Unit { get; set; }
    public string Conditions { get; set; }  // Summary only
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    public string SourceId { get; set; }
}
```

#### 6.2 Retrieval Logic

```csharp
public async Task<TariffRates> GetRates(string code, Guid versionId)
{
    var rates = new TariffRates { Code = code };
    
    // Retrieve MFN rate
    rates.MFN = await _db.MFNRates
        .Where(r => r.Code == code && r.VersionId == versionId && r.IsActive)
        .Select(r => new MFNRate
        {
            Value = r.Value,
            Unit = r.Unit,
            Currency = r.Currency,
            QuantityUnit = r.QuantityUnit,
            Formula = r.Formula,
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo,
            SourceId = r.SourceId
        })
        .FirstOrDefaultAsync();
    
    // Retrieve FTA rates
    rates.FTASchemes = await _db.FTARates
        .Where(r => r.Code == code && r.VersionId == versionId && r.IsActive)
        .Select(r => new FTARate
        {
            SchemeCode = r.SchemeCode,
            PartnerCountry = r.PartnerCountry,
            Value = r.Value,
            Unit = r.Unit,
            Conditions = r.ConditionsSummary,
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo,
            SourceId = r.SourceId
        })
        .ToListAsync();
    
    // Retrieve PPN
    rates.PPN = await _db.TaxRates
        .Where(r => r.Code == code && r.TaxType == TaxType.PPN && r.VersionId == versionId && r.IsActive)
        .Select(r => new TaxRate
        {
            Value = r.Value,
            Unit = r.Unit,
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo,
            SourceId = r.SourceId
        })
        .FirstOrDefaultAsync();
    
    // Retrieve PPh 22
    rates.PPh22 = await _db.TaxRates
        .Where(r => r.Code == code && r.TaxType == TaxType.PPH22_IMPORT && r.VersionId == versionId && r.IsActive)
        .Select(r => new TaxRate
        {
            Value = r.Value,
            Unit = r.Unit,
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo,
            SourceId = r.SourceId
        })
        .FirstOrDefaultAsync();
    
    return rates;
}
```

**Important Disclaimers** (must be displayed to user):

1. **FTA Rates**: "FTA rates shown are available rates for this HS code. Actual entitlement depends on: Rules of Origin compliance, valid Certificate of Origin, direct consignment, and procedural requirements. Consult customs broker for eligibility."

2. **Standard Rates**: "PPN and PPh 22 rates shown are standard rates from BTKI. Special exemptions, facilities, or reductions under specific PMK regulations are not included. Consult with tax advisor for applicable rates."

---

## Data Models

### Database Schema Overview

```sql
-- Nomenclature versioning
nomenclature_versions (
  id UUID PRIMARY KEY,
  name VARCHAR(100),
  hs_edition VARCHAR(10),  -- "2022"
  btki_edition VARCHAR(20),  -- "PMK 26/2022"
  effective_from DATE,
  effective_to DATE,
  state ENUM(...),
  is_active BOOLEAN
)

-- BTKI tariff lines (14,273 entries for BTKI 2022)
btki_tariff_lines (
  id BIGINT PRIMARY KEY,
  version_id UUID REFERENCES nomenclature_versions(id),
  code VARCHAR(15) UNIQUE,  -- "8471.30.20"
  parent_code VARCHAR(15),  -- "8471.30"
  section_code VARCHAR(5),
  chapter_code VARCHAR(5),
  heading_code VARCHAR(10),
  subheading_code VARCHAR(12),
  indent_level INT,
  description_id TEXT NOT NULL,
  description_en TEXT,
  source_id VARCHAR(255),  -- "PMK26-2022:checksum:body:p150:table_5:row_42:cell_1"
  is_active BOOLEAN,
  INDEX idx_code (code),
  INDEX idx_parent (parent_code),
  INDEX idx_chapter (chapter_code)
)

-- Explanatory Notes (65,833 paragraphs)
explanatory_notes (
  id BIGINT PRIMARY KEY,
  version_id UUID,
  heading_code VARCHAR(10),  -- "8471"
  subheading_code VARCHAR(12),  -- NULL if heading-level EN
  paragraph_number VARCHAR(20),
  paragraph_type ENUM('SCOPE', 'DEFINITION', 'INCLUSION', 'EXCLUSION', 'TECHNICAL', 'EXAMPLE', 'BORDERLINE', 'CROSS_REFERENCE'),
  text_id TEXT,
  text_en TEXT,
  previous_para_id BIGINT,
  next_para_id BIGINT,
  parent_section_id BIGINT,
  referred_codes JSON,  -- array of HS codes referenced
  source_id VARCHAR(255),
  INDEX idx_heading (heading_code),
  INDEX idx_type (paragraph_type)
)

-- Legal notes
section_notes (
  id INT PRIMARY KEY,
  version_id UUID,
  section_code VARCHAR(5),
  note_number VARCHAR(10),
  paragraph_number VARCHAR(10),
  text_id TEXT,
  text_en TEXT,
  effect_type ENUM('DEFINITION', 'INCLUSION', 'EXCLUSION', 'CONDITION', 'REFERRAL', 'PRIORITY'),
  target_codes JSON,  -- NULL if applies to whole section
  referred_codes JSON,
  source_id VARCHAR(255),
  INDEX idx_section (section_code)
)

chapter_notes (
  id INT PRIMARY KEY,
  version_id UUID,
  chapter_code VARCHAR(5),
  target_heading VARCHAR(10),  -- NULL if applies to whole chapter
  note_number VARCHAR(10),
  paragraph_number VARCHAR(10),
  text_id TEXT,
  text_en TEXT,
  effect_type ENUM(...),
  referred_codes JSON,
  source_id VARCHAR(255),
  INDEX idx_chapter (chapter_code),
  INDEX idx_target (target_heading)
)

subheading_notes (
  id INT PRIMARY KEY,
  version_id UUID,
  heading_code VARCHAR(10),
  note_number VARCHAR(10),
  text_id TEXT,
  text_en TEXT,
  effect_type ENUM(...),
  target_codes JSON,
  referred_codes JSON,
  source_id VARCHAR(255),
  INDEX idx_heading (heading_code)
)

-- Duty and tax rates
mfn_rates (
  id BIGINT PRIMARY KEY,
  version_id UUID,
  code VARCHAR(15),
  value DECIMAL(10,4),
  unit ENUM('PERCENT', 'SPECIFIC', 'COMPOUND'),
  currency VARCHAR(3),  -- for SPECIFIC
  quantity_unit VARCHAR(20),  -- for SPECIFIC
  formula TEXT,  -- for COMPOUND
  effective_from DATE,
  effective_to DATE,
  source_id VARCHAR(255),
  is_active BOOLEAN,
  INDEX idx_code (code)
)

fta_rates (
  id BIGINT PRIMARY KEY,
  version_id UUID,
  code VARCHAR(15),
  scheme_code VARCHAR(20),  -- "ACFTA", "AKFTA", "IJEPA", "AANZFTA", etc.
  partner_country VARCHAR(3),
  value DECIMAL(10,4),
  unit ENUM('PERCENT', 'SPECIFIC', 'COMPOUND'),
  conditions_summary TEXT,
  effective_from DATE,
  effective_to DATE,
  source_id VARCHAR(255),
  is_active BOOLEAN,
  INDEX idx_code (code),
  INDEX idx_scheme (scheme_code)
)

tax_rates (
  id BIGINT PRIMARY KEY,
  version_id UUID,
  code VARCHAR(15),
  tax_type ENUM('PPN', 'PPH22_IMPORT', 'PPH22_EXPORT'),
  value DECIMAL(10,4),
  unit ENUM('PERCENT', 'SPECIFIC'),
  effective_from DATE,
  effective_to DATE,
  source_id VARCHAR(255),
  is_active BOOLEAN,
  INDEX idx_code_type (code, tax_type)
)

-- Classification audit trail
classification_cases (
  id BIGINT PRIMARY KEY,
  user_id INT,
  version_id UUID REFERENCES nomenclature_versions(id),
  -- input facts (JSON)
  facts JSON,
  -- final result
  final_code VARCHAR(15),
  final_description TEXT,
  confidence ENUM('HIGH', 'MEDIUM', 'LOW'),
  status ENUM('DETERMINED', 'NEED_CLARIFICATION', 'NEED_EXPERT_REVIEW'),
  -- metadata
  created_at TIMESTAMP,
  classification_date DATE,
  INDEX idx_user (user_id),
  INDEX idx_code (final_code),
  INDEX idx_date (classification_date)
)

reasoning_events (
  id BIGINT PRIMARY KEY,
  case_id BIGINT REFERENCES classification_cases(id),
  event_order INT,
  rule_applied VARCHAR(50),  -- "KUM_HS_1", "KUM_HS_3A", etc.
  timestamp TIMESTAMP,
  facts_used JSON,
  candidates_before JSON,  -- array of codes
  candidates_after JSON,
  decision VARCHAR(50),  -- "RETAINED", "ELIMINATED", "UNRESOLVED"
  reasoning TEXT,
  source_ids JSON,  -- array of source_id references
  INDEX idx_case (case_id),
  INDEX idx_rule (rule_applied)
)
```

### Source ID Format

**Structure**: `{document}:{checksum}:{part}:{location}`

**Examples**:

```
PMK26-2022:abc123def456:body:p150:table_5:row_42:cell_1
EN-2022:789xyz456abc:body:p2301:para_15
SectionNote-XVI:def789ghi012:body:p5:para_2
ChapterNote-84:ghi345jkl678:body:p12:para_5:sub_a
```

**Components**:
- `document`: Source document identifier (PMK26-2022, EN-2022, etc.)
- `checksum`: SHA256 hash of source file (first 12 chars)
- `part`: Document part (body, header, table_N)
- `location`: Hierarchical path to exact location (page:paragraph:table:row:cell OR page:paragraph:subparagraph)

**Traceability Guarantee**: Every source_id must be:
1. Stored when dataset imported
2. Verified during validation
3. Included in reasoning_events for audit trail
4. Resolvable to original Word document location

---

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

**Property Reflection**

After analyzing all acceptance criteria, several patterns emerge that allow consolidation:

**Consolidation Opportunities:**

1. **Source Traceability** (Requirements 1.5, 1.6, 2.6, 3.4, 7.5): All extracted/retrieved entities must have source_id → consolidate into one comprehensive source traceability property

2. **Database-Only Retrieval** (Requirements 7.2, 17.1): Candidates and final codes must exist in database → consolidate into one "no hallucination" property

3. **Required Fields Population** (Requirements 5.2, 7.3, 8.6): Multiple properties about "all required fields present" → consolidate into schema completeness properties per entity type

4. **Validation Chain** (Requirements 17.1-17.7): Seven separate validation checks → group into one comprehensive validation property

5. **KUM HS Universal Application** (Requirements 8.1, 8.7): Every classification must apply KUM HS rules → consolidate into one rules engine completeness property

**Unique Properties Retained:**

- Fact extraction never defaults missing facts (5.4) - critical constraint
- Question prioritization by information gain (6.2) - algorithmic correctness
- State machine transitions follow sequence (4.2) - workflow correctness
- Only one active version per date range (4.4) - data integrity
- Subheading comparison at same level only (16.7) - legal compliance

### Property 1: Source Traceability Completeness

*For any* extracted entity (tariff node, EN paragraph, legal note) or retrieved candidate heading, that entity SHALL have a populated source_id field containing file checksum, document part, and hierarchical location path.

**Validates: Requirements 1.5, 1.6, 2.6, 3.4, 7.5**

### Property 2: No Hallucinated Codes or Descriptions

*For any* candidate heading returned by Candidate_Generator or final HS code determined by Rules_Engine, that code and its official description SHALL exist as an exact match in the btki_tariff_lines table for the active nomenclature version.

**Validates: Requirements 7.2, 17.1, 17.3**

### Property 3: Fact Extraction Never Defaults

*For any* fact type that is not explicitly provided in user input or extracted from documents, the Fact_Engine SHALL set that fact's status to UNKNOWN and value to null, never assigning a default value.

**Validates: Requirement 5.4**

### Property 4: Conflicting Facts Flagged

*For any* fact F where value V1 is extracted from source S1 and value V2≠V1 is extracted from source S2, the fact status SHALL be CONFLICTING and the system SHALL request user confirmation before proceeding.

**Validates: Requirements 5.3, 6.5**

### Property 5: Questions Prioritized by Information Gain

*For any* set of generated questions for missing discriminator facts, the questions array SHALL be ordered by information gain descending, where information gain is calculated as (CandidatesEliminated / TotalCandidates).

**Validates: Requirement 6.2**

### Property 6: Candidate Enrichment Completeness

*For any* candidate heading retrieved from database, that candidate SHALL include all required enrichment fields: official description, parent hierarchy (section/chapter), applicable Section Notes, applicable Chapter Notes, applicable Subheading Notes, EN paragraphs for that heading, child subheadings, and source_id.

**Validates: Requirement 7.3**

### Property 7: KUM HS 1 Universal Application

*For all* candidate headings in a classification session, the Rules_Engine SHALL apply KUM HS 1 evaluation to each candidate, creating a reasoning_event with rule="KUM_HS_1", candidate code, matched facts, missing facts, note results, EN results, decision (RETAINED/ELIMINATED/UNRESOLVED), reason, and source_ids.

**Validates: Requirements 8.1, 8.6**

### Property 8: KUM HS Early Stopping

*For any* classification where KUM HS 1 results in exactly one heading retained, the Rules_Engine SHALL proceed directly to KUM HS 6 (subheading descent) without applying KUM HS 2-4, unless new facts are provided that change the candidate set.

**Validates: Requirement 8.7**

### Property 9: KUM HS 3(a) Specificity Selection

*For any* classification with 2+ retained headings after KUM HS 1 and 2, when one heading provides more specific description of the goods than others, the Rules_Engine SHALL select that heading and NOT proceed to KUM HS 3(b), recording the specificity comparison evidence in the reasoning chain.

**Validates: Requirements 11.1, 11.3**

### Property 10: KUM HS 6 Same-Level Comparison

*For any* subheading selection at a given dash level (1-dash, 2-dash, 3-dash), the Rules_Engine SHALL compare only subheadings at that same dash level, never comparing subheadings from different parent headings or different dash levels simultaneously.

**Validates: Requirements 16.3, 16.4**

### Property 11: Version State Machine Integrity

*For any* dataset import, state transitions SHALL follow the defined sequence: UPLOADED → PARSING → MAPPED → VALIDATING → {DRY_RUN_PASSED | DRY_RUN_FAILED} → READY_TO_COMMIT → COMMITTED → INDEXED → ACTIVE, with no state skipping and no invalid backward transitions except explicit rollback.

**Validates: Requirement 4.2**

### Property 12: Single Active Version Per Date

*For any* classification date T, there SHALL exist at most one nomenclature version with state=ACTIVE where effective_from ≤ T and effective_to ≥ T.

**Validates: Requirement 4.4**

### Property 13: Comprehensive Validation

*For any* final HS code C with description D classified on date T, the Validator SHALL verify: (1) code exists in btki_tariff_lines for active version, (2) code format matches XXXX.XX.XX, (3) all parent codes exist, (4) description exactly matches database record, (5) code is active on date T, and (6) all source_ids in reasoning chain reference valid records. If any check fails, validation SHALL return IsValid=false with specific failure details.

**Validates: Requirements 17.1, 17.2, 17.3, 17.4, 17.5, 17.6, 17.7**

### Property 14: Validation Rejection Prevents User Presentation

*For any* classification result where validation returns IsValid=false, the system SHALL NOT present that result to the end user and SHALL instead return error status to the Rules_Engine for correction.

**Validates: Requirements 17.7, 17.8**

### Property 15: Tariff Rate Source Traceability

*For any* retrieved tariff rate (MFN, FTA, PPN, PPh 22), that rate SHALL include a source_id field referencing the database record from which it was retrieved, and that source_id SHALL be traceable to the original BTKI document location.

**Validates: Requirements 18.4, 18.5, 19.6, 20.5, 20.6**

### Property 16: EN Semantic Segmentation Preservation

*For any* multi-paragraph exclusion statement or cross-reference in EN import, all component paragraphs SHALL reference the same parent_section_id and form a linked sequence via previous_paragraph_id/next_paragraph_id, with no truncation mid-statement.

**Validates: Requirements 2.3, 2.4, 2.5**

### Property 17: File Checksum Integrity

*For any* uploaded DOCX file stored for audit purposes, the stored file's calculated SHA256 checksum SHALL exactly match the checksum calculated at upload time and stored in the source_id field.

**Validates: Requirement 1.12**

### Property 18: Essential Character Required for KUM HS 2(a) and 3(b)

*For any* goods classified under KUM HS 2(a) (incomplete/unfinished) or KUM HS 3(b) (composite goods/sets), when the system determines "TREAT_AS_COMPLETE" or selects a heading based on essential character, the reasoning_event SHALL include: factors considered (function, material, bulk, value, necessity), evidence for each factor, and explanation of why the selected component/article confers essential character.

**Validates: Requirements 9.2, 9.3, 12.2, 12.4, 12.5**

### Property 19: Discriminator Fact Clarification

*For any* subheading selection where discriminator fact F (weight, capacity, power, composition, use, etc.) is required to choose between subheadings but F has status UNKNOWN, the system SHALL return status NEED_CLARIFICATION with a question for F showing candidate impact (which subheading applies for each answer), and SHALL NOT default to generic .00 code.

**Validates: Requirements 16.6, 16.7**

### Property 20: Audit Trail Immutability

*For any* classification case, all reasoning_events recorded SHALL remain immutable, including after version rollback, expert review, or correction. Historical reasoning chains SHALL be preserved for audit purposes even when classification is modified.

**Validates: Requirements 4.5, 4.6**

---

## Error Handling

### Error Categories and Recovery Strategies

#### 1. Data Import Errors

**OOXML Parsing Failures:**
- **Cause**: Corrupted DOCX, unexpected structure, unsupported Word features
- **Detection**: Exception during DocumentFormat.OpenXml parsing
- **Response**: Reject upload, log detailed error, request valid DOCX from administrator
- **User Message**: "Unable to parse document. Ensure file is valid DOCX format (not DOC or DOCM) and contains no macros or protected sections."

**Structure Ambiguity:**
- **Cause**: Cannot determine parent hierarchy from indentation/numbering
- **Detection**: Parent resolution algorithm returns multiple candidates or none
- **Response**: Flag entry with STRUCTURE_AMBIGUITY, place in review queue, block activation
- **User Message**: "Dataset contains {N} entries with ambiguous structure. Review and manually assign parents before activation."
- **Recovery**: Administrator manually reviews flagged entries, assigns correct parent, re-runs validation

**Validation Failures:**
- **Cause**: Duplicate codes, missing parents, invalid hierarchy, overlapping dates
- **Detection**: Dry run validation before COMMIT state
- **Response**: Transition to DRY_RUN_FAILED state, present detailed validation report
- **User Message**: "Validation failed: {specific issues}. Fix source document and re-upload."
- **Recovery**: Administrator corrects source DOCX, re-uploads, re-runs validation

#### 2. Classification Execution Errors

**Database Unavailability:**
- **Cause**: MySQL connection lost, query timeout, deadlock
- **Detection**: DbException during query execution
- **Response**: Return HTTP 503 Service Unavailable, log error with stack trace
- **User Message**: "Classification service temporarily unavailable. Please try again in a moment."
- **Recovery**: Automatic retry with exponential backoff (3 retries max), circuit breaker if repeated failures

**No Active Version for Date:**
- **Cause**: Classification date falls outside all version effective_from/effective_to ranges
- **Detection**: Query returns null when fetching active version for date
- **Response**: Return HTTP 400 Bad Request
- **User Message**: "No active tariff nomenclature for classification date {date}. Contact administrator."
- **Recovery**: Administrator activates appropriate version or adjusts effective date ranges

**No Candidates Generated:**
- **Cause**: User input too vague, no matching headings in database
- **Detection**: Candidate_Generator returns empty list
- **Response**: Return status NEED_CLARIFICATION with enhanced questions
- **User Message**: "Unable to identify candidate HS codes. Please provide more specific product description, function, and material information."
- **Recovery**: User provides additional facts, classification restarts

**All Candidates Eliminated:**
- **Cause**: All candidates eliminated by KUM HS 1 (notes/EN exclusions)
- **Detection**: Rules_Engine KUM HS 1 phase leaves zero retained candidates
- **Response**: Flag for expert review with elimination reasons
- **User Message**: "All candidate headings excluded by legal notes or EN. Expert review required. Elimination reasons: {list}"
- **Recovery**: Expert reviewer analyzes elimination logic, either confirms no valid heading exists or identifies missing candidate that should have been retrieved

**Cannot Determine Essential Character:**
- **Cause**: KUM HS 3(b) triggered but essential character factors inconclusive
- **Detection**: EssentialCharacterEvaluator returns CannotDetermine=true
- **Response**: Proceed to KUM HS 3(c) if possible, otherwise flag for expert review
- **User Message**: "Essential character cannot be determined from provided facts. Expert review required."
- **Recovery**: Expert reviews goods, applies human judgment on essential character

**Validation Failure:**
- **Cause**: Final code doesn't exist, parent missing, description mismatch, inactive code, or invalid source_id
- **Detection**: Validator returns IsValid=false with specific failures
- **Response**: Block result presentation, log validation failure with full context
- **User Message**: "Internal classification error: validation failed. Case logged for investigation."
- **Recovery**: Engineering investigates, fixes Rules_Engine or database issue, re-runs classification

#### 3. LLM Integration Errors

**LLM API Unavailability:**
- **Cause**: API rate limit, timeout, service outage
- **Detection**: HTTP error from LLM provider (429, 503, 504)
- **Response**: Retry with exponential backoff, fallback to rule-based extraction if LLM repeatedly fails
- **User Message**: "Fact extraction taking longer than expected. Please wait..."
- **Recovery**: Automatic retry up to 3 attempts, then graceful degradation to keyword-based fact extraction

**LLM Hallucination in Fact Extraction:**
- **Cause**: LLM invents facts not present in input
- **Detection**: Source reference validation — LLM must cite input location for each fact
- **Response**: Reject facts without valid source reference
- **User Message**: None (internal validation, invisible to user)
- **Recovery**: Only accept facts with evidence_reference pointing to input text location

**LLM Generates HS Code (Constraint Violation):**
- **Cause**: Prompt engineering failure, LLM ignores constraints
- **Detection**: Validation finds code not in database
- **Response**: Reject entire classification result, alert engineering team
- **User Message**: "Classification failed validation. Case logged for review."
- **Recovery**: Engineering reviews prompt, strengthens constraints, re-tests LLM behavior

### Error Logging and Monitoring

**Structured Logging Format:**
```json
{
  "timestamp": "2024-01-15T10:30:45Z",
  "level": "ERROR",
  "component": "RulesEngine.KumHs3b",
  "case_id": 12345,
  "user_id": 789,
  "error_code": "ESSENTIAL_CHARACTER_UNDETERMINED",
  "message": "Cannot determine essential character from available facts",
  "context": {
    "candidates": ["8471", "8525"],
    "facts_available": ["commercial_name", "generic_name", "material"],
    "facts_missing": ["weight", "bulk", "component_values"]
  },
  "stack_trace": "..."
}
```

**Critical Alerts** (Trigger PagerDuty/Slack notification):
- Validation failure rate > 1% (indicates systemic issue)
- Database connection failures
- LLM repeatedly generates invalid codes (constraint violation)
- Active version query returns null for current date

**Metrics to Monitor:**
- Classification success rate (DETERMINED / total)
- Average reasoning chain length (number of KUM HS rules applied)
- Clarification request rate (NEED_CLARIFICATION / total)
- Expert review rate (NEED_EXPERT_REVIEW / total)
- Validation failure rate (IsValid=false / total)
- Fact extraction coverage (average facts extracted per case)
- Candidate generation hit rate (candidates found / attempts)

---

## Testing Strategy

### Testing Approach Overview

The HS Classification V2 system requires a **dual testing strategy**:

1. **Unit Tests**: Verify specific behaviors, edge cases, and error conditions
2. **Property-Based Tests**: Verify universal properties across all inputs (correctness guarantees)
3. **Integration Tests**: Verify component interactions and database operations
4. **End-to-End Tests**: Verify complete classification workflows with known gold standard cases

### Unit Testing Strategy

**Target**: Individual components and functions

**Framework**: xUnit for C#, Jest for TypeScript/React frontend

**Coverage Goals**: 80% line coverage minimum, 90% for Rules Engine and Validator

**Key Unit Test Suites**:

1. **DOCX Parser Tests**:
   - Parse valid BTKI DOCX → verify node extraction
   - Parse DOCX with tables → verify rate extraction
   - Parse DOCX with merged cells → verify cell content mapping
   - Parse DOCX with tracked changes → verify detection and rejection
   - Parse corrupted DOCX → verify error handling

2. **Structure Validator Tests**:
   - Dataset with duplicate code → verify detection
   - Dataset with missing parent → verify detection
   - Dataset with orphan nodes → verify detection
   - Dataset with overlapping dates → verify detection
   - Valid dataset → verify dry run passes

3. **Fact Extractor Tests**:
   - Input with weight mention → extract weight fact with unit normalized
   - Input with conflicting weights from different sources → flag CONFLICTING
   - Input without weight mention → status UNKNOWN, value null (NO DEFAULT)
   - Input with "N/A" for fact → user confirmed NOT_APPLICABLE

4. **Question Generator Tests**:
   - Candidates requiring weight discriminator, weight UNKNOWN → generate weight question with candidate impact
   - Questions for multiple missing facts → verify ordered by information gain descending
   - Fact marked NOT_APPLICABLE → verify not asked again in same session

5. **Candidate Generator Tests**:
   - Input "laptop" → retrieve 8471 (ADP machines)
   - Input "crane barge" → retrieve 8905 (floating cranes) not 8426 (land cranes)
   - Input with no matching keywords → return empty list
   - Every returned candidate has source_id → verify
   - Returned code not in database → SHOULD NEVER HAPPEN (test with mock to verify safeguard)

6. **Rules Engine Tests**:
   - KUM HS 1 with one candidate retained → skip to KUM HS 6
   - KUM HS 1 with 2+ candidates → proceed to KUM HS 2 or 3
   - KUM HS 3(a) with one specific heading → select it
   - KUM HS 3(a) with equally specific → proceed to KUM HS 3(b)
   - KUM HS 6 comparing different-level subheadings → reject (must compare same level only)

7. **Validator Tests**:
   - Code exists, format valid, parents exist, description matches, active, source_ids valid → IsValid=true
   - Code doesn't exist → IsValid=false, failure type CODE_NOT_FOUND
   - Code format "847130" (missing dots) → IsValid=false, INVALID_FORMAT
   - Parent "8471.30" doesn't exist for "8471.30.20" → IsValid=false, PARENT_MISSING
   - Description mismatch → IsValid=false, DESCRIPTION_MISMATCH
   - Code inactive on classification date → IsValid=false, INACTIVE_CODE
   - Source_id references non-existent record → IsValid=false, SOURCE_ID_INVALID

### Property-Based Testing Strategy

**Target**: Universal properties that must hold for ALL valid inputs

**Framework**: FsCheck for C# (integrated with xUnit)

**Minimum Iterations**: 100 per property test

**Property Test Tagging**: Each property test must include comment referencing design property:
```csharp
// Feature: hs-classification-v2, Property 2: No Hallucinated Codes or Descriptions
[Property(MaxTest = 100)]
public Property CandidateCodesExistInDatabase() { ... }
```

**Key Property Tests**:

**Property 2: No Hallucinated Codes**
```csharp
// Feature: hs-classification-v2, Property 2: No Hallucinated Codes or Descriptions
[Property(MaxTest = 100)]
public Property CandidateCodesExistInDatabase()
{
    return Prop.ForAll(
        GenerateFactSet(),
        facts => {
            var candidates = _candidateGenerator.GenerateCandidates(facts, activeVersionId).Result;
            return candidates.All(c => 
                _db.BtkiTariffLines.Any(t => 
                    t.Code == c.Code && 
                    t.DescriptionID == c.Description &&
                    t.VersionId == activeVersionId
                )
            );
        }
    );
}
```

**Property 3: Fact Extraction Never Defaults**
```csharp
// Feature: hs-classification-v2, Property 3: Fact Extraction Never Defaults
[Property(MaxTest = 100)]
public Property MissingFactsNeverDefaulted()
{
    return Prop.ForAll(
        GeneratePartialInput(),  // Input deliberately missing random facts
        input => {
            var facts = _factEngine.ExtractFacts(input).Result;
            var allFactTypes = GetAll35FactTypes();
            var providedFactTypes = GetFactTypesInInput(input);
            var missingFactTypes = allFactTypes.Except(providedFactTypes);
            
            return missingFactTypes.All(ft => {
                var fact = facts.GetFact(ft);
                return fact.Status == FactStatus.UNKNOWN && fact.NormalizedValue == null;
            });
        }
    );
}
```

**Property 5: Questions Prioritized by Information Gain**
```csharp
// Feature: hs-classification-v2, Property 5: Questions Prioritized by Information Gain
[Property(MaxTest = 100)]
public Property QuestionsSortedByInformationGain()
{
    return Prop.ForAll(
        GenerateCandidatesWithMissingDiscriminators(),
        (facts, candidates) => {
            var questions = _questionGenerator.GenerateQuestions(facts, candidates).Result;
            
            for (int i = 0; i < questions.Count - 1; i++)
            {
                if (questions[i].InformationGain < questions[i+1].InformationGain)
                    return false;  // Not properly sorted
            }
            return true;
        }
    );
}
```

**Property 7: KUM HS 1 Universal Application**
```csharp
// Feature: hs-classification-v2, Property 7: KUM HS 1 Universal Application
[Property(MaxTest = 100)]
public Property KumHs1AppliedToAllCandidates()
{
    return Prop.ForAll(
        GenerateClassificationInput(),
        input => {
            var result = _rulesEngine.ApplyKUMHS(input.Facts, input.Candidates).Result;
            var kumHs1Events = result.ReasoningChain.Where(e => e.Rule == "KUM_HS_1").ToList();
            
            // Must have KUM HS 1 event for each candidate
            return kumHs1Events.Count == input.Candidates.Count &&
                   kumHs1Events.All(e => 
                       e.Decision != null && 
                       e.Reason != null && 
                       e.SourceIds.Any()
                   );
        }
    );
}
```

**Property 13: Comprehensive Validation**
```csharp
// Feature: hs-classification-v2, Property 13: Comprehensive Validation
[Property(MaxTest = 100)]
public Property ValidationPerformsAllChecks()
{
    return Prop.ForAll(
        GenerateRulesResult(),
        rulesResult => {
            var validation = _validator.ValidateClassification(rulesResult, activeVersionId).Result;
            
            if (validation.IsValid)
            {
                // If valid, all checks must have passed
                var codeExists = _db.BtkiTariffLines.Any(t => t.Code == rulesResult.FinalCode);
                var formatValid = Regex.IsMatch(rulesResult.FinalCode, @"^\d{4}\.\d{2}\.\d{2}$");
                var parentsExist = GetParentCodes(rulesResult.FinalCode).All(p => 
                    _db.BtkiTariffLines.Any(t => t.Code == p)
                );
                var descriptionMatches = _db.BtkiTariffLines.First(t => t.Code == rulesResult.FinalCode)
                    .DescriptionID == rulesResult.FinalDescription;
                var codeActive = _db.BtkiTariffLines.First(t => t.Code == rulesResult.FinalCode)
                    .IsActive && /* date range check */;
                
                return codeExists && formatValid && parentsExist && descriptionMatches && codeActive;
            }
            else
            {
                // If invalid, must have specific failure
                return validation.Failures.Any();
            }
        }
    );
}
```

**Property 14: Validation Rejection Prevents User Presentation**
```csharp
// Feature: hs-classification-v2, Property 14: Validation Rejection Prevents User Presentation
[Property(MaxTest = 100)]
public Property InvalidResultsNeverReachUser()
{
    return Prop.ForAll(
        GenerateInvalidRulesResult(),  // Generator creates results that will fail validation
        invalidResult => {
            var classificationResult = _classificationService.Classify(invalidResult.Input).Result;
            
            // Result to user should either be success (after fixing) or error, never the invalid result
            return classificationResult.Status == "ERROR" || 
                   (classificationResult.Status == "DETERMINED" && 
                    _validator.ValidateClassification(classificationResult, activeVersionId).Result.IsValid);
        }
    );
}
```

### Integration Testing Strategy

**Target**: Component interactions, database operations, external service calls

**Framework**: xUnit with Docker test containers for MySQL

**Test Environment**: Isolated test database with seed data

**Key Integration Tests**:

1. **End-to-End BTKI Import**:
   - Upload real BTKI DOCX → parse → validate → commit → activate → verify queryable
   - Test with BTKI 2022 (14,273 lines) — duration ~5 minutes

2. **Classification Pipeline**:
   - User input → Fact_Engine → Candidate_Generator → Rules_Engine → Validator → Tariff_Lookup
   - Verify reasoning_events recorded at each step
   - Verify audit trail complete and source_ids valid

3. **Version Activation and Rollback**:
   - Activate version V2 → classify goods → deactivate V2, activate V1 → verify classifications use V1
   - Rollback V2 activation → verify reasoning_events preserved

4. **Database Constraint Enforcement**:
   - Attempt to insert duplicate code → verify rejected by unique constraint
   - Attempt to insert tariff line without parent → verify rejected by foreign key
   - Attempt to activate two versions with overlapping dates → verify application logic prevents

### Gold Standard Test Cases

**Purpose**: Verify system produces correct classifications for well-known test cases with official rulings

**Source**: Real-world classification examples from:
- WEA08 Crane Barge case (89.05 vs 89.01)
- WEA35 Dumper Tractor case (87.04.10 vs 87.04.21 vs 87.01)
- Laptop classification (84.71.30 vs 85.25 vs 85.28)

**Test Format**:
```csharp
[Fact]
public async Task GoldStandard_CraneBarge_89_05()
{
    var input = new ClassificationInput
    {
        CommercialName = "Floating Crane",
        GenericName = "Crane Barge",
        PrimaryFunction = "Mengangkat/Lifting (crane, hoist, forklift)",
        OperatingArea = "Laut / Lepas pantai",
        Mobility = "Tidak bergerak sendiri (towed, pushed)",
        Specifications = "Kapasitas angkat 500 ton, tidak memiliki propulsi sendiri"
    };
    
    var result = await _classificationService.Classify(input);
    
    Assert.Equal("8905.90.90", result.FinalCode);
    Assert.Equal("DETERMINED", result.Status);
    Assert.Contains("EN 89.05 explicitly mentions floating cranes", result.ReasoningChain.SelectMany(r => r.Reasoning));
    Assert.DoesNotContain("8901", result.EliminatedCandidates.Select(c => c.Code.Substring(0, 4)));
}
```

**Gold Standard Test Suite**: Minimum 20 cases covering:
- All 6 KUM HS rules
- Incomplete goods (CKD/SKD)
- Composite goods
- Retail sets
- Material-based headings
- Function-based headings

### Test Data Management

**Seed Data**: 
- Mini BTKI subset (500 representative tariff lines across all sections)
- Mini EN subset (1000 paragraphs for key headings)
- Sample Section/Chapter/Subheading Notes

**Data Refresh**: Test database rebuilt before each integration test run

**Anonymization**: No real user data in test environment; all test cases use synthetic or public domain examples

---

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-4)

**Database Setup:**
- MySQL 8.0+ schema creation
- Tables: nomenclature_versions, btki_tariff_lines, section_notes, chapter_notes, subheading_notes
- Indexes for performance
- Seed data: 500-line mini BTKI for testing

**Admin Data Center Core:**
- DOCX parser using DocumentFormat.OpenXml
- OOXML structure detector (paragraph styles, tables, indentation)
- Basic tariff node extraction
- Source location tracking (file checksum, paragraph index)
- Validation engine (duplicate detection, hierarchy checking)

**Deliverable**: Successfully import mini BTKI DOCX, validate, and query via SQL

### Phase 2: Classification Engine Core (Weeks 5-8)

**Fact Extraction Engine:**
- LLM integration for fact extraction (with source citation requirement)
- 35 fact type extractors
- Fact status management (UNKNOWN, NOT_APPLICABLE, CONFLICTING)
- Never-default constraint enforcement

**Candidate Generation:**
- Database search strategies (lexical, function-based, material-based)
- Candidate enrichment (notes, EN retrieval)
- Source ID population

**Rules Engine - KUM HS 1:**
- Heading term matching
- Section/Chapter Note application
- EN paragraph usage
- Reasoning event recording

**Deliverable**: Given user input, extract facts, generate candidates, apply KUM HS 1, record reasoning chain

### Phase 3: Complete Rules Engine (Weeks 9-12)

**KUM HS 2-6 Implementation:**
- KUM HS 2(a): Incomplete goods essential character evaluation
- KUM HS 2(b): Material extension
- KUM HS 3(a): Specific vs general specificity scoring
- KUM HS 3(b): Essential character for composites/sets
- KUM HS 3(c): Last in numerical order
- KUM HS 4: Most akin (with expert review flag)
- KUM HS 5: Containers and packing
- KUM HS 6: Subheading descent with same-level comparison

**Validator:**
- All 7 validation checks
- Rejection on any failure
- Detailed failure reporting

**Deliverable**: Complete classification pipeline from input to validated 8-digit code

### Phase 4: Full BTKI Import and EN (Weeks 13-16)

**Admin Data Center Full Features:**
- EN paragraph import and segmentation
- Legal notes import and classification
- Version management state machine
- Dry run validation
- Activation/deactivation workflows
- Version comparison diff

**Full Dataset:**
- Import complete BTKI 2022 (14,273 tariff lines)
- Import EN 2022 (65,833 paragraphs)
- Import all Section/Chapter/Subheading Notes

**Deliverable**: Complete, validated, activated BTKI 2022 + EN database

### Phase 5: Tariff Lookup and Frontend Integration (Weeks 17-20)

**Tariff Lookup Module:**
- MFN rate retrieval
- FTA rate retrieval with disclaimer
- PPN and PPh 22 rate retrieval with disclaimer
- Source ID traceability for all rates

**Frontend Integration:**
- Adapt existing React V1 frontend
- New classification result display (reasoning chain, eliminated candidates, clarification requests)
- Admin UI for dataset import and version management
- Audit trail viewer

**Deliverable**: Complete V2 system with UI

### Phase 6: Testing and Validation (Weeks 21-24)

**Test Suite Development:**
- Unit tests (80%+ coverage)
- Property-based tests (20 properties × 100 iterations each)
- Integration tests (database operations, pipeline)
- Gold standard tests (20 known cases)

**Performance Testing:**
- Classification latency target: <5 seconds for typical case
- Database query optimization
- Candidate retrieval caching

**User Acceptance Testing:**
- Internal testing with customs experts
- 50 real-world classification cases
- Feedback incorporation

**Deliverable**: Production-ready V2 system with comprehensive test coverage

---

## Success Criteria

The HS Classification V2 system is considered successful when:

1. **No Hallucination**: 100% of final codes returned exist in database (zero hallucinated codes in 1000 test cases)
2. **Source Traceability**: 100% of reasoning events have valid source_ids traceable to original documents
3. **Validation Enforcement**: 100% of validation failures prevent result presentation to user
4. **Fact Extraction Correctness**: 0% of missing facts assigned default values (all UNKNOWN when not provided)
5. **Gold Standard Accuracy**: ≥95% agreement with 20 known gold standard classifications
6. **Performance**: 90th percentile classification latency ≤5 seconds
7. **Expert Review Rate**: ≤15% of classifications flagged for expert review (indicates system handles majority autonomously)
8. **User Satisfaction**: ≥4.0/5.0 rating from customs expert users after UAT

---

## Conclusion

The HS Classification V2 design transforms a hallucination-prone LLM classifier into a systematic, legal-grade, database-centric classification system. By enforcing strict constraints (no LLM-generated codes/descriptions, comprehensive validation, source traceability, no defaulted facts), the system provides auditability and compliance required for customs classification.

The architecture separates concerns clearly: Admin Data Center handles data import and version management; Fact Engine extracts objective evidence; Candidate Generator retrieves from database; Rules Engine applies legal logic sequentially; Validator enforces correctness; Tariff Lookup provides rates. Each component has well-defined interfaces and testable contracts expressed as correctness properties.

Property-based testing ensures universal guarantees hold across all inputs, while integration and gold standard tests verify real-world accuracy. The comprehensive error handling strategy addresses data import issues, classification failures, and constraint violations with appropriate recovery paths.

The implementation roadmap phases the work over 24 weeks, allowing incremental delivery and validation at each milestone. Success criteria provide quantitative measures of system correctness and performance.

This design provides the foundation for a production-grade HS classification system that customs professionals can trust for legal compliance and audit purposes.
