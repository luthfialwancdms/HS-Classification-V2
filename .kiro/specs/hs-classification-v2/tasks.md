# Implementation Plan: HS Classification V2

## Overview

This implementation plan transforms the existing frontend-only HS classification system (React + Vite + Groq API) into a legal-grade, database-centric classification system with a C# ASP.NET Core backend, MySQL 8.0+ database, and comprehensive BTKI 2022 / Explanatory Notes / Legal Notes data.

The plan is organized by component in build order: database foundation and version management → data ingestion (DOCX BTKI, EN, Legal Notes, and CSV/Excel template) → fact extraction → candidate generation → KUM HS 1-6 rules engine → validation → tariff lookup → classification orchestration and API → frontend integration → deployment preparation.

Convert the feature design into a series of prompts for a code-generation LLM that will implement each step with incremental progress. Each task builds on previous tasks and ends with wiring things together, so there is no orphaned code. Tasks focus only on writing, modifying, or testing code.

Implementation language: **C#** (backend) and **JavaScript/React** (frontend), as specified in the design document. The database is MySQL 8.0+. All HS codes, descriptions, EN paragraphs, and legal notes MUST come from the database, never from LLM memory.

## Tasks

- [x] 1. Set up project structure and database foundation
  - [x] 1.1 Create C# ASP.NET Core Web API project structure
    - Initialize ASP.NET Core 8.0 Web API project with Clean Architecture layers (API, Application, Domain, Infrastructure)
    - Add NuGet packages: Entity Framework Core, Pomelo MySQL connector, DocumentFormat.OpenXml, Serilog, AutoMapper
    - Configure dependency injection container and middleware pipeline
    - Set up environment configuration for development/staging/production
    - _Requirements: 1.1, 2.1, 3.1, 4.1_

  - [x] 1.2 Create MySQL database schema with versioning and audit tables
    - Implement EF Core DbContext and migrations for: nomenclature_versions, btki_tariff_lines, explanatory_notes, section_notes, chapter_notes, subheading_notes, mfn_rates, fta_rates, tax_rates, classification_cases, reasoning_events
    - Add indexes for code lookups, parent relationships, chapter grouping, and date range queries
    - Implement source_id as composite string ({document}:{checksum}:{part}:{location})
    - _Requirements: 4.1, 4.2_

  - [x] 1.3 Implement Version Manager with state machine
    - Implement IVersionManager with CreateVersion, GetImportState, TransitionState, ActivateVersion, DeactivateVersion, GetActiveVersion, CompareVersions
    - Enforce state sequence: UPLOADED→PARSING→MAPPED→VALIDATING→DRY_RUN_PASSED/FAILED→READY_TO_COMMIT→COMMITTED→INDEXED→ACTIVE (no skipping, no invalid backward transitions except explicit rollback)
    - Enforce only one active version per effective date range; activating a new version deactivates conflicting versions
    - Rebuild search index after activation; preserve historical classification records on deactivate/rollback
    - Create version management API endpoints
    - _Requirements: 4.1, 4.2, 4.4, 4.5, 4.6, 4.7, 4.8_

  - [ ]* 1.4 Write property test for Version Manager state machine
    - **Property 11: Version State Machine Integrity**
    - **Validates: Requirements 4.2**

  - [ ]* 1.5 Write property test for single active version constraint
    - **Property 12: Single Active Version Per Date**
    - **Validates: Requirements 4.4**

- [x] 2. Implement BTKI DOCX parser and ingestion
  - [x] 2.1 Create DOCX parser using OpenXML SDK
    - Implement IDocxParser.ParseBTKI extracting OOXML structure (paragraph styles, outline levels, numbering, indentation, tables, merged cells, footnotes)
    - Extract tariff nodes: code, indent_level, description_id, description_en, unit, MFN/FTA/PPN/PPh22 rates, footnotes, effective dates
    - Calculate and store SourceLocation: SHA256 file checksum, document part, page, paragraph index, table/row/cell indexes, OOXML element id
    - _Requirements: 1.1, 1.2, 1.3, 1.6_

  - [x] 2.2 Implement hierarchy detection and parent relationship builder
    - Build parent-child relationships from indentation levels and numbering sequences
    - Derive parent_code from hierarchy (e.g., 8471.30.20 → 8471.30)
    - Flag STRUCTURE_AMBIGUITY when parent cannot be determined and route to review queue
    - _Requirements: 1.3, 1.4_

  - [x] 2.3 Create Structure Validator with comprehensive checks
    - Implement IStructureValidator (ValidateHierarchy, ValidateCodes, ValidateCompleteness)
    - Detect duplicate codes, invalid code format (^\d{4}\.\d{2}\.\d{2}$), missing parents, orphan nodes, inconsistent indentation, overlapping effective dates, empty descriptions
    - Generate validation report with issue severity (ERROR, WARNING, INFO) and source location
    - _Requirements: 1.8_

  - [x] 2.4 Implement dry run validation and commit workflow
    - Dry run checks: all parents exist, no duplicates, no circular references, date ranges no overlap, total counts match expected (14,273 lines)
    - Build side-by-side comparison view between source Word and parsed records
    - Detect tracked changes / unresolved revisions and require admin confirmation
    - Require admin approval for all STRUCTURE_AMBIGUITY entries before activation
    - _Requirements: 1.4, 1.7, 1.9, 1.10, 1.11_

  - [x] 2.5 Create BTKI import API endpoints and read-only source storage
    - POST /api/admin/btki/upload, GET /api/admin/btki/versions, GET /api/admin/btki/validation-report/:versionId, POST /api/admin/btki/approve-ambiguities, POST /api/admin/btki/commit/:versionId
    - Generate mapping report with source paragraph/table location per node
    - Store original Word file in read-only storage with checksum for audit
    - _Requirements: 1.5, 1.12_

  - [ ]* 2.6 Write property test for source traceability
    - **Property 1: Source Traceability Completeness**
    - **Validates: Requirements 1.5, 1.6, 2.6, 3.4, 7.5**

  - [ ]* 2.7 Write property test for file checksum integrity
    - **Property 17: File Checksum Integrity**
    - **Validates: Requirement 1.12**

- [x] 3. Implement Explanatory Notes parser and ingestion
  - [x] 3.1 Create EN DOCX parser with semantic segmentation
    - Extend IDocxParser with ParseEN (edition, section, chapter, heading_code, subheading_code, heading_title)
    - Parse paragraphs: paragraph_number, scope, definition, inclusion, exclusion, technical characteristics, examples, borderline guidance, cross-references
    - Classify paragraph_type: SCOPE, DEFINITION, INCLUSION, EXCLUSION, TECHNICAL, EXAMPLE, BORDERLINE, CROSS_REFERENCE
    - Segment by semantic unit (not fixed character count); extract referred_codes; store SourceLocation
    - _Requirements: 2.1, 2.2, 2.3, 2.6_

  - [x] 3.2 Implement EN paragraph linking and context preservation
    - Set previous_paragraph_id, next_paragraph_id, parent_section_id, referred_codes
    - Preserve complete multi-paragraph exclusions/cross-references without mid-statement truncation
    - Build EN heading coverage report
    - _Requirements: 2.4, 2.5, 2.7_

  - [x] 3.3 Create EN validation and admin import workflow
    - Detect EN paragraphs without valid heading reference; reconcile paragraph count (65,833 expected)
    - Require admin approval before activation
    - API: POST /api/admin/en/upload, GET /api/admin/en/coverage-report/:versionId, POST /api/admin/en/commit/:versionId
    - _Requirements: 2.8, 2.9, 2.10_

  - [ ]* 3.4 Write property test for EN semantic segmentation
    - **Property 16: EN Semantic Segmentation Preservation**
    - **Validates: Requirements 2.3, 2.4, 2.5**

- [x] 4. Implement Legal Notes parser and ingestion
  - [x] 4.1 Create Legal Notes parser with effect classification
    - Extend IDocxParser with ParseLegalNotes for Section/Chapter/Subheading notes
    - Classify source_type (SECTION_NOTE, CHAPTER_NOTE, SUBHEADING_NOTE); populate target_heading when a Chapter Note targets specific headings
    - Extract effect_type (DEFINITION, INCLUSION, EXCLUSION, CONDITION, REFERRAL, PRIORITY), target_codes, referred_codes
    - Store SourceLocation with checksum, page, paragraph, OOXML reference
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 4.2 Create Legal Notes validation and import workflow
    - Reconcile note count per section/chapter; detect notes without valid scope reference
    - Require admin approval before activation
    - API: POST /api/admin/notes/upload, GET /api/admin/notes/validation-report/:versionId, POST /api/admin/notes/commit/:versionId
    - _Requirements: 3.5, 3.6, 3.7_

- [x] 5. Checkpoint - Database and DOCX ingestion complete
  - Ensure all tests pass, database schema is stable, and BTKI/EN/Legal Notes import workflows are functional.
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement CSV/Excel Template Ingestion (alternative admin path)
  - [x] 6.1 Add ingestion_method column to schema
    - Add ingestion_method ENUM('DOCX_PARSER','CSV_TEMPLATE','XLSX_TEMPLATE') to btki_tariff_lines; migrate existing rows to DOCX_PARSER
    - Update DOCX parser to set ingestion_method = DOCX_PARSER
    - _Requirements: 21.12_

  - [x] 6.2 Generate downloadable CSV and Excel templates
    - CSV template with columns: code, description_id, description_en, parent_code, indent_level, mfn_rate, mfn_rate_unit, ppn_rate, pph22_rate, effective_from, effective_to, status
    - Excel (.xlsx) template with same columns plus header tooltips explaining each field
    - API: GET /api/admin/template/download/csv, GET /api/admin/template/download/xlsx
    - _Requirements: 21.1, 21.2_

  - [x] 6.3 Implement template file validator
    - Validate each row: code format XXXX.XX.XX, non-empty description_id, valid parent_code, date format YYYY-MM-DD, valid status (A/I), valid rate_unit (PERCENT/SPECIFIC/COMPOUND)
    - Generate row-level report (row number, column, error type, suggested fix); reject on ERROR, require ack on WARNING
    - Parse both CSV (UTF-8) and Excel (.xlsx)
    - API: POST /api/admin/template/validate
    - _Requirements: 21.3, 21.4, 21.5_

  - [x] 6.4 Implement preview diff and commit workflow
    - Categorize rows as INSERT / UPDATE (changed fields highlighted) / UNCHANGED (skip); no silent skips
    - Require admin confirmation of preview before commit
    - On commit assign source_id template:upload:{filename_checksum}:{row_number}, set ingestion_method; apply Version Manager state machine; never bypass dry run
    - API: POST /api/admin/template/preview, POST /api/admin/template/commit
    - _Requirements: 21.6, 21.7, 21.8, 21.9, 21.10, 21.11_

- [x] 7. Implement Fact Extraction Engine
  - [x] 7.1 Create fact extractor with 35 fact types
    - Implement IFactExtractor.ExtractFacts covering all 35 fact types across Identity, Material, Physical, Process, Function, Power, Use, Condition, Presentation, State, Evidence
    - Store per fact: raw_value, normalized_value, unit, source (USER/DOCUMENT/EXPERT/SYSTEM), evidence_reference, confidence_score, status (UNKNOWN/NOT_APPLICABLE/ZERO/CONFLICTING/VERIFIED)
    - Use LLM only to identify mentions, normalize units, and detect conflicts; never infer missing facts; never assign defaults
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 7.2 Implement dynamic question generator with information gain
    - Implement IQuestionGenerator.GenerateQuestions
    - Compute information gain = (CandidatesEliminated / TotalCandidates) × FactReliability; sort descending; recalc after each answer
    - Include CandidateImpact (candidates_affected, eliminated_codes, retained_codes); return NEED_CLARIFICATION with missing_facts + questions + candidate_impact
    - _Requirements: 5.5, 6.1, 6.2, 6.3_

  - [x] 7.3 Add conflict detection and session memory
    - Flag CONFLICTING when contradictory values from different sources and request user confirmation
    - Do not re-ask a fact marked NOT_APPLICABLE within the same session
    - _Requirements: 6.4, 6.5_

  - [ ]* 7.4 Write property test for fact extraction never defaults
    - **Property 3: Fact Extraction Never Defaults**
    - **Validates: Requirement 5.4**

  - [ ]* 7.5 Write property test for conflicting facts detection
    - **Property 4: Conflicting Facts Flagged**
    - **Validates: Requirements 5.3, 6.5**

  - [ ]* 7.6 Write property test for question prioritization
    - **Property 5: Questions Prioritized by Information Gain**
    - **Validates: Requirement 6.2**

- [x] 8. Implement Candidate Generation Engine
  - [x] 8.1 Create multi-strategy database search
    - Implement ICandidateGenerator.GenerateCandidates with strategies: exact lexical, synonym/terminology mapping, multilingual, semantic similarity, function-based, material-based, composition-based, process, form/presentation, use/principal-use, parent-machine, legal-note referral, EN cross-reference
    - Assign generation_source per candidate; never generate codes/descriptions not in the active version database
    - _Requirements: 7.1, 7.2, 7.4_

  - [x] 8.2 Implement candidate enrichment with legal context
    - For each candidate retrieve: official description, parent hierarchy (section→chapter→heading), applicable Section Notes, Chapter Notes, notes targeting the heading, Subheading Notes, EN paragraphs, child subheadings, required discriminator facts, cross-referenced headings
    - Populate source_id for traceability
    - _Requirements: 7.3, 7.5_

  - [ ]* 8.3 Write property test for no hallucinated codes
    - **Property 2: No Hallucinated Codes or Descriptions**
    - **Validates: Requirements 7.2, 17.1, 17.3**

  - [ ]* 8.4 Write property test for candidate enrichment completeness
    - **Property 6: Candidate Enrichment Completeness**
    - **Validates: Requirement 7.3**

- [x] 9. Implement KUM HS 1 (GRI 1) Rules Engine
  - [x] 9.1 Create KUM HS 1 evaluation logic
    - Implement IRulesEngine.ApplyKUMHS; for each candidate compare facts to heading terms
    - Apply Section Notes, Chapter Notes, heading-targeted notes, and EN (inclusions/exclusions/borderline) in operational sequence
    - Record reasoning_event per candidate: matched_facts, missing_facts, conflicting_facts, note_results, EN_results, decision (RETAINED/ELIMINATED/UNRESOLVED), reason, source_ids
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.8_

  - [x] 9.2 Implement early stopping when one heading retained
    - When exactly one heading retained after KUM HS 1, proceed directly to KUM HS 6; do not apply KUM HS 2-4 unless new facts change candidates
    - _Requirements: 8.7_

  - [ ]* 9.3 Write property test for KUM HS 1 universal application
    - **Property 7: KUM HS 1 Universal Application**
    - **Validates: Requirements 8.1, 8.6**

  - [ ]* 9.4 Write property test for early stopping
    - **Property 8: KUM HS Early Stopping**
    - **Validates: Requirement 8.7**

- [x] 10. Implement KUM HS 2 (GRI 2a, 2b) Rules Engine
  - [x] 10.1 Create KUM HS 2(a) incomplete/unfinished goods logic
    - Trigger on incomplete/unfinished/unassembled/disassembled/CKD/SKD/blank
    - Evaluate essential character (missing components, remaining processes, function availability, identity recognition, components-together); generate clarification questions
    - Output TREAT_AS_COMPLETE / CLASSIFY_AS_PRESENTED / NEED_CLARIFICATION with essential-character evidence
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6_

  - [x] 10.2 Create KUM HS 2(b) material extension logic
    - Trigger on mixtures/combinations/material extension; proceed to KUM HS 3 when 2+ headings result; record retained material-based headings
    - _Requirements: 10.1, 10.2, 10.3_

- [x] 11. Implement KUM HS 3 (GRI 3a, 3b, 3c) Rules Engine
  - [x] 11.1 Create KUM HS 3(a) specific vs general logic
    - Trigger with 2+ headings retained; score specificity (+1 specific, -1 residual, complete-vs-parts, material coverage)
    - Select most specific and stop (no 3(b)); when equally specific proceed to 3(b); record comparison evidence and source_ids
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_

  - [x] 11.2 Create KUM HS 3(b) essential character logic
    - Trigger for mixtures/composites/sets; verify retail-set criteria
    - Weigh factors (function 0.3, material 0.15, bulk 0.15, value 0.15, necessity 0.15, consumer perception 0.1); no single factor auto-determinative
    - Output HEADING_SELECTED / PROCEED_TO_3C / NEED_CLARIFICATION; record factors, weighting, evidence, reasoning
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6_

  - [x] 11.3 Create KUM HS 3(c) last in numerical order logic
    - Trigger when 3(a) and 3(b) fail; select numerically-last among equally applicable candidates that passed KUM HS 1; do not pad with weak candidates; record equivalents and source_ids
    - _Requirements: 13.1, 13.2, 13.3, 13.4_

  - [ ]* 11.4 Write property test for KUM HS 3(a) specificity selection
    - **Property 9: KUM HS 3(a) Specificity Selection**
    - **Validates: Requirements 11.1, 11.3**

  - [ ]* 11.5 Write property test for essential character evidence
    - **Property 18: Essential Character Required for KUM HS 2(a) and 3(b)**
    - **Validates: Requirements 9.2, 9.3, 12.2, 12.4, 12.5**

- [x] 12. Implement KUM HS 4 and 5 (GRI 4, 5) Rules Engine
  - [x] 12.1 Create KUM HS 4 most akin logic
    - Trigger when KUM HS 1-3 fail; compare on material, form, function, operating principle, use, technical characteristics, commercial identity; display comparator goods; flag expert review on high risk; record factors and source_ids
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5_

  - [x] 12.2 Create KUM HS 5(a) and 5(b) containers and packing logic
    - 5(a): specially shaped cases — evaluate shaped/fitted, long-term use, presented with article, normally sold with article, not conferring essential character → classified with article
    - 5(b): packing material/containers — evaluate presented with goods, normally used for packing, not suitable for repetitive use → classified with goods
    - _Requirements: 15.1, 15.2, 15.3, 15.4, 15.5, 15.6_

- [x] 13. Implement KUM HS 6 (GRI 6) Subheading Descent
  - [x] 13.1 Create subheading descent logic after heading locked
    - Trigger when 4-digit heading locked; retrieve child subheadings; compare only same dash level
    - Apply KUM HS 1-5 mutatis mutandis using subheading description, Subheading Notes, still-relevant Section/Chapter Notes, discriminator thresholds (weight, capacity, power, size, composition, concentration, retail/bulk, CKD/non-CKD, use)
    - Descend 4→6→8 digit; return NEED_CLARIFICATION when discriminator facts missing (never default to .00); never compare across different 4-digit groups after lock
    - _Requirements: 16.1, 16.2, 16.3, 16.4, 16.5, 16.6, 16.7_

  - [ ]* 13.2 Write property test for same-level comparison
    - **Property 10: KUM HS 6 Same-Level Comparison**
    - **Validates: Requirements 16.3, 16.4**

  - [ ]* 13.3 Write property test for discriminator clarification
    - **Property 19: Discriminator Fact Clarification**
    - **Validates: Requirements 16.6, 16.7**

- [ ] 14. Checkpoint - Classification engine complete
  - Ensure all KUM HS 1-6 rules are implemented, the reasoning chain is captured for all decision paths, and all tests pass.
  - Ensure all tests pass, ask the user if questions arise.

- [x] 15. Implement Validation System
  - [x] 15.1 Create comprehensive validator
    - Implement IValidator.ValidateClassification performing all checks: code exists in active version, format XXXX.XX.XX, all parent codes exist, description exact match, code active on classification date, all source_ids reference valid records
    - Return IsValid=false with specific ValidationFailure per failed check (CODE_NOT_FOUND, INVALID_FORMAT, PARENT_MISSING, DESCRIPTION_MISMATCH, INACTIVE_CODE, SOURCE_ID_INVALID)
    - _Requirements: 17.1, 17.2, 17.3, 17.4, 17.5, 17.6, 17.7_

  - [x] 15.2 Implement validation rejection and error handling
    - On failure, do not present result to user; return error to Rules_Engine for correction; log failure with full context
    - _Requirements: 17.8_

  - [ ]* 15.3 Write property test for comprehensive validation
    - **Property 13: Comprehensive Validation**
    - **Validates: Requirements 17.1, 17.2, 17.3, 17.4, 17.5, 17.6, 17.7**

  - [ ]* 15.4 Write property test for validation rejection prevents presentation
    - **Property 14: Validation Rejection Prevents User Presentation**
    - **Validates: Requirements 17.7, 17.8**

- [x] 16. Implement Tariff Lookup Module
  - [x] 16.1 Create MFN duty rate retrieval
    - Implement ITariffLookupModule MFN retrieval from mfn_rates for validated code; verify effective on classification date
    - Return rate_value, rate_unit (PERCENT/SPECIFIC/COMPOUND), currency, quantity_unit, formula, source_id; return null with explanation when absent
    - _Requirements: 18.1, 18.2, 18.3, 18.4, 18.5_

  - [x] 16.2 Create FTA rate reference retrieval
    - Retrieve all FTA rates by scheme_code and partner_country; return value, unit, effective dates, conditions_summary, source_id
    - Label "Available FTA Rates" (not "Applicable"); show eligibility disclaimer; never assert applicability from partner country alone
    - _Requirements: 19.1, 19.2, 19.3, 19.4, 19.5, 19.6_

  - [x] 16.3 Create PPN and PPh 22 rate retrieval
    - Retrieve standard PPN and PPh 22 import rates from tax_rates (value, unit, effective dates, source_id); show standard-rate disclaimer; return null with explanation when absent
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6_

  - [ ]* 16.4 Write property test for tariff rate source traceability
    - **Property 15: Tariff Rate Source Traceability**
    - **Validates: Requirements 18.4, 18.5, 19.6, 20.5, 20.6**

- [x] 17. Create classification orchestration and API endpoints
  - [x] 17.1 Implement classification orchestrator
    - Create ClassificationService orchestrating Fact_Engine → Candidate_Generator → Rules_Engine → Validator → Tariff_Lookup_Module
    - Persist classification_cases (user_id, version_id, facts, final_code, final_description, confidence, status, dates) and immutable reasoning_events (event_order, rule_applied, facts_used, candidates_before/after, decision, reasoning, source_ids)
    - Handle iterative fact collection (NEED_CLARIFICATION resume) and expert-review escalation (NEED_EXPERT_REVIEW)
    - _Requirements: 5.5, 8.1, 16.1_

  - [x] 17.2 Create classification REST API endpoints
    - POST /api/classification/start, POST /api/classification/:caseId/facts, GET /api/classification/:caseId/status, GET /api/classification/:caseId/reasoning, GET /api/classification/:caseId/tariffs, POST /api/classification/:caseId/export
    - _Requirements: 17.1, 18.1, 19.1, 20.1_

  - [x] 17.3 Add authentication and authorization middleware
    - JWT authentication; role-based authorization (Admin, Classifier, Viewer); protect admin endpoints; log all requests with user_id
    - _Requirements: 4.1, 17.8_

  - [ ]* 17.4 Write property test for audit trail immutability
    - **Property 20: Audit Trail Immutability**
    - **Validates: Requirements 4.5, 4.6**

- [x] 18. Integrate with existing React frontend
  - [x] 18.1 Update React app to connect to C# backend API
    - Replace direct Groq calls in src/services/classifier.js with backend classification API client (src/services/apiClient.js); ClassificationForm → POST /api/classification/start
    - Implement iterative fact collection (render clarification questions, submit via /facts, poll /status until DETERMINED / NEED_CLARIFICATION / NEED_EXPERT_REVIEW)
    - Update ResultPanel to show final code, official description, confidence, reasoning summary, and tariff rates (MFN, FTA, PPN, PPh 22) with disclaimers
    - Add reasoning-chain viewer with expandable steps and source_id references; preserve V1 UI/UX
    - _Requirements: 5.5, 6.1, 17.3, 18.4, 19.4, 20.4_

  - [x] 18.2 Add admin dashboard for data center operations
    - Routes /admin/versions, /admin/upload, /admin/validation; version list with import-state indicators and activate/deactivate/rollback actions
    - DOCX upload UI for BTKI/EN/Legal Notes plus CSV/Excel template upload tab (download buttons, row-level error table, INSERT/UPDATE/SKIP diff preview, admin confirmation)
    - Validation report with side-by-side source comparison; STRUCTURE_AMBIGUITY review queue (approve/reject); version activation gated on dry-run results
    - _Requirements: 1.9, 1.11, 2.10, 3.7, 4.4, 4.6, 4.7, 21.1, 21.2, 21.4, 21.8, 21.9_

  - [x] 18.3 Implement frontend error handling and user feedback
    - User-friendly API error messages; loading/progress states for long classifications; clarification questions rendered with candidate-impact context
    - Always render FTA eligibility and standard-rate (PPN/PPh 22) disclaimers; retry on transient failures
    - _Requirements: 6.3, 17.8, 19.4, 20.4_

- [ ] 19. Final integration and deployment preparation
  - [x] 19.1 Create database seeding scripts
    - Seed initial admin user and BTKI 2022 nomenclature version metadata; document first-time DOCX upload process
    - _Requirements: 4.1_

  - [x] 19.2 Configure production deployment settings
    - Production appsettings with MySQL connection string; CORS for React origin; Serilog structured logging; HTTPS enforcement; health check endpoints; docker-compose (C# API + MySQL + nginx static frontend)
    - _Requirements: 4.1, 17.8_

  - [x] 19.3 Create API documentation
    - OpenAPI/Swagger for all admin and classification endpoints; auth requirements; request/response schemas and examples; error codes
    - _Requirements: 17.8_

  - [ ]* 19.4 Write integration tests for complete classification flow
    - End-to-end: upload DOCX → validate → activate → classify → retrieve tariffs; CSV/Excel template path; error scenarios; audit trail capture; version switching
    - _Requirements: 4.8, 17.1, 18.1, 21.10_

- [x] 21. Case history, expert review, validation reports and ambiguity queue (from V2 product logic doc)
  - [x] 21.1 Schema and roles: UserRole.ExpertReviewer, CaseStatus.Rejected, CandidateCodesJson, ExpertCorrection, ImportBatch, AmbiguityItem
    - _Requirements: 1.4, 1.11, 14.4_
  - [x] 21.2 Riwayat klasifikasi: GET /api/classification/cases (filter, paging, per-role visibility) and GET /api/classification/{id}; history page
    - _Requirements: 17.3_
  - [x] 21.3 Expert review: approve (candidate or EXPERT_OVERRIDE, validator-gated), reject, request review; append-only audit; review page
    - _Requirements: 14.4, 17.1, 17.8_
  - [x] 21.4 Persisted validation report per upload; STRUCTURE_AMBIGUITY queue with per-item approve/reject; commit and activation gated on no pending items and same checksum; Tambah Versi
    - _Requirements: 1.4, 1.9, 1.11, 4.2_

- [x] 22. Gap closing from V2 product logic doc (batch 1)
  - [x] 22.1 Seed MFN rates for active version from data/btki_2022_full.csv (clean percentages only; unparseable skipped and logged)
    - _Requirements: 18.1, 18.4_
  - [x] 22.2 Weighted confidence model (25/20/15/15/15/10) with caps and risk flags; persisted per case and shown in UI
    - _Requirements: 8.8, 17.3_
  - [x] 22.3 Goods-condition gate before candidate search: KUM HS 2(a) incomplete/CKD/SKD (TREAT_AS_COMPLETE / CLASSIFY_AS_PRESENTED), retail set 3-test (KUM HS 3b), mixture essential component (2b→3b), Section XVI Note 2 parts routed to the parts heading citing the parent machine; form "Kondisi Saat Impor" mapped to these facts
    - _Requirements: 9.1–9.6, 10.1–10.3, 12.1–12.6_
  - [x] 22.5 KUM HS 6 numeric thresholds: parse bounds (berat/daya/kVA/volume/panjang/tegangan; melebihi, tidak melebihi, tetapi, kurang dari, atau lebih) from official subheading texts, compare with stated values (fact or specification text), ask for the value when a level is split by a number; OCR-misplaced group texts and disjunctive texts are never trusted
    - _Requirements: 16.5, 16.6_
  - [x] 22.4 KUM HS 5 packaging/containers, accessories, multifunction machines, bulk, used goods/scrap modules
    - [x] KUM HS 5(a) cases/containers (4 tests) and 5(b) packing (normally used / reusable) in ConditionGate; "Wadah/Kemasan" option in Kondisi Saat Impor; follow-up rounds store only new reasoning steps
    - [x] Accessories (necessity / standalone / dedicated → "Bagian dan aksesori" heading of the parent; tied parent headings are asked, not 3(c)-guessed)
    - [x] Multifunction machines (Section XVI Note 3 principal function) and functional units (Note 4); no principal function → expert (3(c))
    - [x] Bulk: physical state, net quantity (feeds numeric thresholds), industrial vs retail presentation recorded in reasoning
    - [x] Used goods (still works / repairable → classified as new) and waste/scrap (material → only "sisa dan skrap" headings naming it; contaminated → expert)
    - [ ] Legal Note exclusions for these modules (needs Catatan Bagian/Bab data); missing scrap headings in BTKI data (e.g. 74.04 tembaga, 76.02 aluminium)
    - _Requirements: 15.1–15.6_

- [ ] 23. BTKI 2022 from the official PDFs (repository.beacukai.go.id, docs/btki/)
  - [x] 23.1 BtkiTableAssembler: words + positions of the 3-column table (Pos Tarif | Uraian | Description) to rows; columns from whitespace corridors, baselines not glyph boxes, superscripts/subscripts glued to their word, hyphen-wrapped words rejoined, code printed above its text handled
  - [x] 23.2 BtkiHierarchyBuilder + BtkiPdfParser (PdfPig 0.1.16): headings, one-dash groups, subheadings, tariff lines in the shape the classifier reads; heading built from a single-line heading (204 positions had none); duplicates keep the first; whole book parses in 5 s: 12,922 nodes, 81 chapters, 0 errors
  - [x] 23.3 Admin upload accepts .docx or one/many .pdf (checksum independent of order), 64+ MB; structure check accepts hierarchy codes; Upload Dokumen tab
  - [x] 23.4 PDF commit completes the version from the active one: chapters without a text layer (1-15, 98-99), one-dash group texts the PDF omits, MFN rates by code; each copied row marked `carry:`; refuses a version that already has rows
  - [x] 23.5 Version diff (added / removed / reworded / OCR-only) with per-chapter counts; "Bandingkan" in the Versi tab
  - [x] 23.6 Activating a version returns the replaced one to Indexed so it can be re-activated (rollback)
  - [x] 23.7 "BTKI 2022 (PDF resmi)" activated (admin approved); 15-case regression old vs new, no regression. Found and fixed on the way: candidate list cap 10 -> 25 (the PDF added headings that filled it and pushed out 8501 for "motor listrik"); KUM HS 3(a) tie-break by Focus (share of the heading's own words that the goods' name covers) instead of by which child lines share a word; headings printed only as one tariff line are also created for carried chapters 1-15 ("madu alam" now 0409, was 1702.90.20). Old version "BTKI 2022" stays Indexed for rollback
  - [ ] 23.8 Chapters 1-15 are scanned images (39 pages): still the older CSV text; needs OCR or another source. Catatan Bagian/Bab are not in these PDFs
  - [ ] 23.9 Explanatory Notes from `EN HS 2022 KPU TANJUNG PRIOK v2 (AutoRecovered).docx`

- [ ] 20. Final checkpoint - System complete
  - Ensure all unit, property, and integration tests pass; all 20 correctness properties validated; all 21 requirements covered end to end.
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional property-based tests and can be skipped for a faster MVP.
- Each task references specific requirement sub-clauses for traceability; all 21 requirements are covered across tasks 1-19.
- Checkpoints ensure incremental validation and user feedback opportunities.
- Implementation uses C# (backend), MySQL 8.0+ (database), and React (frontend) per the design document.
- All HS codes, descriptions, EN paragraphs, and legal notes MUST come from the database, never from LLM memory.
- Source traceability (source_id) must be preserved throughout every decision.
- KUM HS rules MUST be applied in sequence: GRI 1 → GRI 2 → GRI 3 → GRI 4 → GRI 5 → GRI 6.
- Validation MUST reject any classification where the code doesn't exist, format is wrong, parent is missing, description mismatches, code is inactive, or a source_id is invalid.
- Completion status reflects the already-implemented backend (project setup, schema, Version Manager, DOCX/EN/legal-notes parsers, CSV/Excel template ingestion, fact engine, candidate generator, KUM HS 1-6 rules, validator, tariff lookup, orchestration, API, auth, seeding, deployment config). Remaining open items are the frontend integration (18.1-18.3), API documentation (19.3), the two end-of-phase checkpoints (5, 14, 20), and all optional property/integration tests.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "2.1", "3.1", "4.1"] },
    { "id": 2, "tasks": ["1.4", "1.5", "2.2", "3.2", "4.2"] },
    { "id": 3, "tasks": ["2.3", "3.3", "6.1"] },
    { "id": 4, "tasks": ["2.4", "3.4", "6.2"] },
    { "id": 5, "tasks": ["2.5", "6.3"] },
    { "id": 6, "tasks": ["2.6", "2.7", "6.4", "7.1"] },
    { "id": 7, "tasks": ["7.2", "7.3"] },
    { "id": 8, "tasks": ["7.4", "7.5", "7.6", "8.1"] },
    { "id": 9, "tasks": ["8.2"] },
    { "id": 10, "tasks": ["8.3", "8.4", "9.1"] },
    { "id": 11, "tasks": ["9.2", "9.3", "9.4", "10.1", "10.2"] },
    { "id": 12, "tasks": ["11.1"] },
    { "id": 13, "tasks": ["11.2", "11.3"] },
    { "id": 14, "tasks": ["11.4", "11.5", "12.1", "12.2"] },
    { "id": 15, "tasks": ["13.1"] },
    { "id": 16, "tasks": ["13.2", "13.3", "15.1"] },
    { "id": 17, "tasks": ["15.2", "15.3", "15.4", "16.1"] },
    { "id": 18, "tasks": ["16.2", "16.3"] },
    { "id": 19, "tasks": ["16.4", "17.1"] },
    { "id": 20, "tasks": ["17.2", "17.3"] },
    { "id": 21, "tasks": ["17.4", "18.1"] },
    { "id": 22, "tasks": ["18.2", "18.3"] },
    { "id": 23, "tasks": ["19.1", "19.2"] },
    { "id": 24, "tasks": ["19.3", "19.4"] }
  ]
}
```
