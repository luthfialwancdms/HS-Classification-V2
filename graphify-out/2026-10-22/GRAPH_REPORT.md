# Graph Report - Project_AI  (2026-10-22)

## Corpus Check
- 107 files · ~89,345 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 12 file(s) not represented in the graph (top: .log 4, (none) 2, .css 2)

## Summary
- 1430 nodes · 2951 edges · 83 communities (71 shown, 12 thin omitted)
- Extraction: 91% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 250 edges (avg confidence: 0.82)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- API Controllers and Auth
- Project Namespace Structure
- React App Shell and Views
- Design Doc Concepts
- BTKI Data Seeding
- Data Assets and MVP Risks
- Frontend Toolchain
- Auth and DI Wiring
- Solution and NuGet References
- API Launch Config
- KUM HS Rules Engine
- Classification Result DTOs
- Question Generation
- KUM HS6 Determination
- BTKI Tariff Line Model
- Fact Extraction and Candidates
- EF Core Migrations
- Classification Domain Models
- EF DbContext and DbSets
- BTKI Parsing Model
- Community 20
- Community 21
- Community 22
- Community 23
- Community 24
- Community 25
- Community 26
- Community 27
- Community 28
- Community 29
- Community 30
- Community 31
- Community 32
- Community 33
- Community 34
- Community 35
- Community 36
- Community 37
- Community 38
- Community 39
- Community 40
- Community 41
- Community 42
- Community 43
- Community 44
- Community 45
- Community 46
- Community 47
- Community 48
- Community 49
- Community 50
- Community 51
- Community 52
- Community 53
- Community 54
- Community 55
- Community 56
- Community 57
- Community 58
- Community 59
- Community 60
- Community 61
- Community 62
- Community 63
- Community 64
- Community 65
- Community 66
- Community 67
- Community 68
- Community 69
- Community 70
- Community 71
- Community 72
- Community 73
- Community 75
- Community 76
- Community 77
- Community 78
- Community 79
- Community 80
- Community 81
- Community 82

## God Nodes (most connected - your core abstractions)
1. `HsClassifierDbContext` - 52 edges
2. `CandidateHeading` - 48 edges
3. `NomenclatureVersion` - 39 edges
4. `BtkiTariffLine` - 37 edges
5. `RulesEngine` - 32 edges
6. `ClassificationCase` - 31 edges
7. `FactSet` - 30 edges
8. `Fact` - 28 edges
9. `ParsedTariffNode` - 26 edges
10. `HsClassifier.Domain.Enums` - 24 edges

## Surprising Connections (you probably didn't know these)
- `btki_reference Table` --semantically_similar_to--> `BTKI 2022 Tariff Nomenclature`  [INFERRED] [semantically similar]
  docs/DATABASE_SCHEMA.md → .kiro/specs/hs-classification-v2/requirements.md
- `users Table` --semantically_similar_to--> `JWT Auth with Admin/Classifier/Viewer Roles`  [INFERRED] [semantically similar]
  docs/DATABASE_SCHEMA.md → .kiro/specs/hs-classification-v2/tasks.md
- `src/data/kumhs_rules.json` --conceptually_related_to--> `KUM HS (GRI 1-6 General Rules for Interpretation)`  [INFERRED]
  docs/TECHNICAL.md → .kiro/specs/hs-classification-v2/requirements.md
- `audit_logs Table` --semantically_similar_to--> `Immutable Audit Trail`  [INFERRED] [semantically similar]
  docs/DATABASE_SCHEMA.md → .kiro/specs/hs-classification-v2/requirements.md
- `classification_cases Table` --semantically_similar_to--> `Reasoning Event Record`  [INFERRED] [semantically similar]
  docs/DATABASE_SCHEMA.md → .kiro/specs/hs-classification-v2/requirements.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Runtime Classification Pipeline** — kiro_specs_hs_classification_v2_design_fact_extraction_engine, kiro_specs_hs_classification_v2_design_candidate_generation_engine, kiro_specs_hs_classification_v2_design_rules_engine, kiro_specs_hs_classification_v2_design_validation_system, kiro_specs_hs_classification_v2_design_tariff_lookup_module [EXTRACTED 1.00]
- **KUM HS General Rules for Interpretation 1-6** — kiro_specs_hs_classification_v2_design_rules_engine, kiro_specs_hs_classification_v2_design_kum_hs_1, kiro_specs_hs_classification_v2_design_kum_hs_2a, kiro_specs_hs_classification_v2_design_kum_hs_3a, kiro_specs_hs_classification_v2_design_kum_hs_3b, kiro_specs_hs_classification_v2_design_kum_hs_3c, kiro_specs_hs_classification_v2_design_kum_hs_6 [EXTRACTED 1.00]
- **Nomenclature Dataset Ingestion and Activation** — kiro_specs_hs_classification_v2_design_docx_parser, kiro_specs_hs_classification_v2_design_structure_validator, kiro_specs_hs_classification_v2_design_version_manager, kiro_specs_hs_classification_v2_design_import_state_machine, kiro_specs_hs_classification_v2_design_nomenclature_versions [EXTRACTED 1.00]
- **GIR 1-6 sequential conditional application flow** — docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_1, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_2a, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_3a, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_3b, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_4, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_5a, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_6 [EXTRACTED 1.00]
- **BTKI/EN master data chain from DOCX to validated final code** — docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_docx_ingestion_pipeline, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_tariff_node, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_legal_note, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_explanatory_notes_paragraph, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_validator_component, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_output_json_schema [INFERRED 0.85]
- **Evidence, reasoning events, confidence and audit trail stack** — docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_fact_observation, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_candidate_evaluation, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_reasoning_event, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_confidence_model, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_audit_trail, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_expert_correction [INFERRED 0.85]
- **Classification Pipeline: Fact -> Candidate -> Rules -> Validate -> Tariff** — _kiro_specs_hs_classification_v2_requirements_fact_engine, _kiro_specs_hs_classification_v2_requirements_candidate_generator, _kiro_specs_hs_classification_v2_requirements_rules_engine, _kiro_specs_hs_classification_v2_requirements_validator, _kiro_specs_hs_classification_v2_requirements_tariff_lookup_module [EXTRACTED 1.00]
- **KUM HS Rules Applied in Mandatory Sequence GRI 1-6** — _kiro_specs_hs_classification_v2_requirements_gri_1, _kiro_specs_hs_classification_v2_requirements_gri_2a, _kiro_specs_hs_classification_v2_requirements_gri_2b, _kiro_specs_hs_classification_v2_requirements_gri_3a, _kiro_specs_hs_classification_v2_requirements_gri_3b, _kiro_specs_hs_classification_v2_requirements_gri_3c, _kiro_specs_hs_classification_v2_requirements_gri_4, _kiro_specs_hs_classification_v2_requirements_gri_5, _kiro_specs_hs_classification_v2_requirements_gri_6 [EXTRACTED 1.00]
- **V1 MVP Limitations That Motivate the V2 Redesign** — docs_technical_stateless_frontend_only, docs_technical_known_limitations, _kiro_specs_hs_classification_v2_requirements_no_llm_hs_generation, _kiro_specs_hs_classification_v2_requirements_audit_trail [INFERRED 0.75]
- **HS Classification Execution Pipeline** — docs_workflow_end_to_end_fact_extraction_engine, docs_workflow_end_to_end_candidate_generation, docs_workflow_end_to_end_kum_hs_rules_engine, docs_workflow_end_to_end_validator, docs_workflow_end_to_end_tariff_lookup, docs_task_list_classification_orchestrator [EXTRACTED 1.00]
- **BTKI Reference Data Ingestion Path** — docs_workflow_end_to_end_docx_parser, docs_workflow_end_to_end_version_state_machine, docs_workflow_end_to_end_structure_ambiguity, docs_task_list_fase_1_database, docs_task_list_fase_1b_template_ingestion [INFERRED 0.85]
- **Disclaimer & Audit Governance Layer** — docs_workflow_end_to_end_fta_disclaimer, docs_project_summary_djbc_disclaimer, docs_workflow_end_to_end_reasoning_events_audit_trail [INFERRED 0.75]
- **KUM HS Reasoning Pipeline (rules -> reasoning block -> eliminations)** — docs_user_guide_kum_hs, docs_user_guide_reasoning_kum_block, docs_user_guide_eliminated_candidates, docs_ui_wireframe_reasoning_panel, docs_ui_wireframe_eliminated_list, data_readme_kumhs_rules_json [EXTRACTED 0.85]
- **BTKI Data Version Lifecycle (upload -> parse -> validate -> ambiguity -> commit)** — data_btki_parse_report_v2, data_btki_parse_report_code_counts, docs_ui_wireframe_version_state_machine, docs_ui_wireframe_validation_report, docs_ui_wireframe_ambiguity_queue, docs_ui_wireframe_upload_document_types [INFERRED 0.85]
- **Recommendation-Not-Determination Safeguards** — docs_user_guide_recommendation_only, docs_user_guide_confidence_level, docs_user_guide_expert_review_flag, docs_ui_wireframe_fta_disclaimer, docs_ui_wireframe_tariff_tax_panel [INFERRED 0.75]

## Communities (83 total, 12 thin omitted)

### Community 0 - "API Controllers and Auth"
Cohesion: 0.06
Nodes (27): AdminImportController, Actor, AdminTemplateController, AuthController, BtkiController, ClassificationController, SeesAll, UserId (+19 more)

### Community 1 - "Project Namespace Structure"
Cohesion: 0.06
Nodes (28): HsClassifier.Domain.Enums, HsClassifier.Infrastructure.Persistence, HsClassifier.Infrastructure.Parsing, HsClassifier.Infrastructure.Template, HsClassifier.Infrastructure.Seeding, HsClassifier.Application.Classification.Rules, HsClassifier.Tests, HsClassifier.Infrastructure.Services (+20 more)

### Community 2 - "React App Shell and Views"
Cohesion: 0.07
Nodes (67): App(), ClarificationBanner(), LoginScreen(), viewFromPath(), VIEWS, AdminDashboard(), BatchDetail(), BatchesPanel() (+59 more)

### Community 3 - "Design Doc Concepts"
Cohesion: 0.06
Nodes (38): HS Classification V2 Design Document, Admin Data Center, BTKI 2022 / PMK 26-2022 Dataset, btki_tariff_lines Table, Candidate Generation Engine (ICandidateGenerator), CandidateHeading (Enriched Candidate), classification_cases Table, DOCX Parser Module (IDocxParser) (+30 more)

### Community 4 - "BTKI Data Seeding"
Cohesion: 0.06
Nodes (36): Parsed Code Counts (14273 unique, 438 tables), Missing Chapter 77, Key Code Verification (OK/MISSING), BTKI 2022 Parse Report v2, catatan_bab_seed.json, dropdown_master.json, hs_headings_seed.csv, kumhs_rules.json (+28 more)

### Community 5 - "Data Assets and MVP Risks"
Cohesion: 0.07
Nodes (33): API Key Exposed in Browser (MVP risk), btki_national.json (105 national subpositions), catatan_bab_seed.json (Section/Chapter Notes), Gold Dataset (150 validated cases), openai/gpt-oss-120b via Groq, hs_headings_seed.csv (heading-level seed), kumhs_rules.json (KUM HS 1-6 rules), Prompt Builder (classifier.js buildPrompt()) (+25 more)

### Community 6 - "Frontend Toolchain"
Cohesion: 0.06
Nodes (36): dependencies, react, react-dom, tailwindcss, @tailwindcss/vite, devDependencies, eslint, @eslint/js (+28 more)

### Community 7 - "Auth and DI Wiring"
Cohesion: 0.09
Nodes (18): IJwtService, DependencyInjection, GeminiTermSuggester, Model, GroqTermSuggester, BaseUrl, Endpoint, Model (+10 more)

### Community 8 - "Solution and NuGet References"
Cohesion: 0.07
Nodes (33): HsClassifier.API, net8.0, Microsoft.EntityFrameworkCore.Design (9.0.19), Microsoft.EntityFrameworkCore.SqlServer (9.0.19), HsClassifier.Application, net8.0, Microsoft.NET.Sdk, HsClassifier.Domain (+25 more)

### Community 9 - "API Launch Config"
Cohesion: 0.07
Nodes (28): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, launchUrl, applicationUrl (+20 more)

### Community 10 - "KUM HS Rules Engine"
Cohesion: 0.21
Nodes (11): RulesEngine, Fact, ConfidenceScore, EvidenceReference, NormalizedValue, RawValue, Source, Status (+3 more)

### Community 11 - "Classification Result DTOs"
Cohesion: 0.08
Nodes (27): ClassificationSessionResult, CandidateCodes, CaseId, Confidence, EliminatedCount, FinalCode, FinalDescription, Message (+19 more)

### Community 12 - "Question Generation"
Cohesion: 0.12
Nodes (17): QuestionGeneratorService, CandidateHeading, ChapterCode, ChapterNotesText, ChildDescriptions, ChildSubheadings, Code, Description (+9 more)

### Community 13 - "KUM HS6 Determination"
Cohesion: 0.11
Nodes (21): ClassificationResult, Confidence, EliminatedCandidates, FinalCode, FinalDescription, MissingFacts, PendingQuestions, ReasoningChain (+13 more)

### Community 14 - "BTKI Tariff Line Model"
Cohesion: 0.09
Nodes (22): BtkiTariffLine, ChapterCode, Code, DescriptionEn, DescriptionId, EffectiveFrom, EffectiveTo, FtaRates (+14 more)

### Community 15 - "Fact Extraction and Candidates"
Cohesion: 0.26
Nodes (4): FactExtractionService, CandidateGeneratorService, ClassificationOrchestrator, ClassificationValidator

### Community 16 - "EF Core Migrations"
Cohesion: 0.19
Nodes (3): HsClassifier.Infrastructure.Migrations, InitialCreate, ExpertReviewAndImportBatches

### Community 17 - "Classification Domain Models"
Cohesion: 0.13
Nodes (16): CandidateDecision, Eliminated, Retained, Unresolved, CandidateImpact, CandidatesAffected, EliminatedCodes, RetainedCodes (+8 more)

### Community 18 - "EF DbContext and DbSets"
Cohesion: 0.11
Nodes (16): HsClassifierDbContext, AmbiguityItems, AppUsers, BtkiTariffLines, ChapterNotes, ClassificationCases, ExpertCorrections, ExplanatoryNotes (+8 more)

### Community 19 - "BTKI Parsing Model"
Cohesion: 0.11
Nodes (18): ParsedTariffNode, AmbiguityReason, ChapterCode, Code, DescriptionEn, DescriptionId, HeadingCode, IndentLevel (+10 more)

### Community 20 - "Community 20"
Cohesion: 0.11
Nodes (18): NomenclatureVersion, BtkiEdition, ChapterNotes, CreatedAt, CreatedBy, Description, EffectiveFrom, EffectiveTo (+10 more)

### Community 21 - "Community 21"
Cohesion: 0.11
Nodes (18): SubheadingNote, EffectType, HeadingCode, Id, NoteNumber, ReferredCodesJson, SourceId, TargetCodesJson (+10 more)

### Community 22 - "Community 22"
Cohesion: 0.16
Nodes (8): clean(), get_bab(), get_seksi(), is_tariff_table(), main(), node_type(), normalize_hs(), parse_table()

### Community 24 - "Community 24"
Cohesion: 0.17
Nodes (10): AmbiguityGate, AmbiguityResolution, BtkiStructureService, DryRunResult, AmbiguousNodes, CanCommit, ErrorCount, Issues (+2 more)

### Community 25 - "Community 25"
Cohesion: 0.24
Nodes (14): Admin Data Center, BTKI 2022 Tariff Nomenclature, Candidate Generation Engine, CSV/Excel Template Ingestion Path, Explanatory Notes (EN) Ingestion, Section, Chapter and Subheading Notes, Nomenclature Version and Activation, STRUCTURE_AMBIGUITY Review Queue (+6 more)

### Community 27 - "Community 27"
Cohesion: 0.23
Nodes (9): clean(), extract_bm_from_cell(), extract_codes_from_cell(), extract_texts_from_cell(), get_seksi(), is_tariff_table(), main(), node_type() (+1 more)

### Community 28 - "Community 28"
Cohesion: 0.14
Nodes (14): FtaRate, ConditionsSummary, EffectiveFrom, EffectiveTo, Id, IsActive, PartnerCountryCode, RateUnit (+6 more)

### Community 29 - "Community 29"
Cohesion: 0.14
Nodes (14): MfnRate, Currency, EffectiveFrom, EffectiveTo, Formula, Id, IsActive, QuantityUnit (+6 more)

### Community 30 - "Community 30"
Cohesion: 0.22
Nodes (10): Immutable Audit Trail, KADL - Klasifikasi Atas Dasar Legalitas, Reasoning Event Record, JWT Auth with Admin/Classifier/Viewer Roles, audit_logs Table, classification_cases Table, daily_usage Table, expert_reviews Table (+2 more)

### Community 31 - "Community 31"
Cohesion: 0.19
Nodes (4): clean(), is_header_row(), parse_bm(), parse_hs()

### Community 32 - "Community 32"
Cohesion: 0.17
Nodes (12): EnParseResult, Issues, Paragraphs, SourceChecksum, SourceFileName, TotalParagraphs, ParseIssue, Code (+4 more)

### Community 33 - "Community 33"
Cohesion: 0.15
Nodes (13): AmbiguityItem, Batch, BatchId, Code, CorrectedParent, DecidedAt, DecidedBy, Decision (+5 more)

### Community 34 - "Community 34"
Cohesion: 0.15
Nodes (13): ClassificationCase, CandidateCodesJson, ClassificationDate, Confidence, CreatedAt, FactsJson, FinalCode, FinalDescription (+5 more)

### Community 35 - "Community 35"
Cohesion: 0.15
Nodes (12): ImportBatch, Checksum, CreatedAt, CreatedBy, ErrorCount, FileName, Id, Items (+4 more)

### Community 36 - "Community 36"
Cohesion: 0.15
Nodes (13): ReasoningEvent, CandidatesAfterJson, CandidatesBeforeJson, Case, CaseId, Decision, EventOrder, FactsUsedJson (+5 more)

### Community 37 - "Community 37"
Cohesion: 0.15
Nodes (13): ChapterNote, ChapterNumber, EffectType, Id, NoteNumber, ParagraphNumber, ReferredCodesJson, SourceId (+5 more)

### Community 38 - "Community 38"
Cohesion: 0.15
Nodes (13): ExplanatoryNote, HeadingCode, Id, NextParagraphId, ParagraphNumber, ParagraphType, ParentSectionId, PreviousParagraphId (+5 more)

### Community 39 - "Community 39"
Cohesion: 0.17
Nodes (12): ImportState, Active, Committed, DryRunFailed, DryRunPassed, Indexed, Mapped, Parsing (+4 more)

### Community 40 - "Community 40"
Cohesion: 0.35
Nodes (12): Essential Character Determination, GRI 1 - Heading Terms, Notes and EN, GRI 2(a) - Incomplete and Unfinished Goods, GRI 2(b) - Material Extension to Mixtures, GRI 3(a) - Most Specific Heading, GRI 3(b) - Essential Character, GRI 3(c) - Last in Numerical Order, GRI 4 - Most Akin Goods (+4 more)

### Community 41 - "Community 41"
Cohesion: 0.18
Nodes (10): KUM HS 1 (heading terms, Section Notes, Chapter Notes, EN assistance), KUM HS 2(b) (mixtures and combinations of materials), KUM HS 3(a) (most specific heading), KUM HS 3(c) (last in numerical order), KUM HS 5(a) (special cases and containers), KUM HS 5(b) (packing materials and packing containers), KUM HS 6 (same-level subheading comparison after heading lock), kum_hs_rule Table (GRI_1..GRI_6 trigger/evaluation schema) (+2 more)

### Community 42 - "Community 42"
Cohesion: 0.26
Nodes (7): src/data/dropdown_master.json, ClassificationForm(), INITIAL_STATE, SECTIONS, MultiSelectField(), SelectField(), TextField()

### Community 43 - "Community 43"
Cohesion: 0.17
Nodes (12): ParsedNote, EffectType, NoteNumber, NoteType, ParagraphNumber, ReferredCodesJson, ScopeCode, SourceId (+4 more)

### Community 44 - "Community 44"
Cohesion: 0.17
Nodes (11): FactSource, Document, Expert, System, User, FactStatus, Conflicting, NotApplicable (+3 more)

### Community 45 - "Community 45"
Cohesion: 0.20
Nodes (8): AppUser, CreatedAt, Email, Id, IsActive, PasswordHash, Role, BtkiCsvSeeder

### Community 46 - "Community 46"
Cohesion: 0.17
Nodes (12): SectionNote, EffectType, Id, NoteNumber, ParagraphNumber, ReferredCodesJson, SectionNumber, SourceId (+4 more)

### Community 47 - "Community 47"
Cohesion: 0.17
Nodes (12): TaxRate, EffectiveFrom, EffectiveTo, Id, IsActive, RateUnit, RateValue, SourceId (+4 more)

### Community 48 - "Community 48"
Cohesion: 0.17
Nodes (11): AmbiguityDecision, Approved, Pending, Rejected, ConfidenceLevel, High, Low, Medium (+3 more)

### Community 49 - "Community 49"
Cohesion: 0.20
Nodes (10): Fact Extraction Engine, Question Prioritisation by Information Gain, NEED_CLARIFICATION Status, Tariff Lookup Module, ClassificationService Orchestrator, Clean Architecture Backend Layers, IFactExtractor (35 Fact Types), IQuestionGenerator (+2 more)

### Community 50 - "Community 50"
Cohesion: 0.25
Nodes (4): fmt_code(), fmt_code4(), fmt_code6(), main()

### Community 51 - "Community 51"
Cohesion: 0.22
Nodes (8): Admin Data Center, DOCX Visual Verification Workflow (render, sample, side-by-side, reconcile, approve), EXPLANATORY NOTES (EN)_DATA_NOT_LOADED Status, explanatorynotes(en)_document Table, explanatorynotes(en)_paragraph Table, OOXML Structure Mapping (styles, outline level, numbering, indentation, tables), Minimum Roles (USER..AUDITOR), Security Controls (untrusted documents, tenant isolation, RBAC)

### Community 52 - "Community 52"
Cohesion: 0.18
Nodes (11): ExpertCorrection, Action, AfterCode, AfterStatus, BeforeCode, BeforeStatus, CaseId, CreatedAt (+3 more)

### Community 53 - "Community 53"
Cohesion: 0.36
Nodes (4): get_seksi(), load_existing_seed(), load_hs6_from_csv(), main()

### Community 54 - "Community 54"
Cohesion: 0.33
Nodes (4): is_vague(), is_vague(), is_vague(), main()

### Community 55 - "Community 55"
Cohesion: 0.27
Nodes (8): candidate_evaluation Table (RETAINED / ELIMINATED / UNRESOLVED), Confidence Model (weighted components), Implementation Milestones 1-6, KADL (Klasifikasi Atas Dasar Legal) Human Output, KUM HS 4 (goods most akin), Mandatory Output JSON Schema, Retrieval Engine (BTKI nodes, ancestors, notes, EN, cross-refs), V2 Classification Wizard (11 steps)

### Community 58 - "Community 58"
Cohesion: 0.22
Nodes (8): Source ID Traceability, Classification Validator, 20 Correctness Properties (Property-Based Tests), IValidator (ValidateClassification), callGroq(), Groq Chat Completions API, Known Limitations of the V1 MVP, llama-3.3-70b-versatile Model

### Community 59 - "Community 59"
Cohesion: 0.33
Nodes (5): dash_level(), is_vague(), main(), enrich_uraian(), get_parent_uraian()

### Community 60 - "Community 60"
Cohesion: 0.22
Nodes (9): ParsedEnParagraph, HeadingCode, ParagraphNumber, ParagraphType, ReferredCodesJson, SequenceInHeading, SourceId, SubheadingCode (+1 more)

### Community 61 - "Community 61"
Cohesion: 0.22
Nodes (9): ParagraphType, Borderline, CrossReference, Definition, Example, Exclusion, Inclusion, Scope (+1 more)

### Community 62 - "Community 62"
Cohesion: 0.22
Nodes (9): TemplateDiffRow, Code, NewValue, OldValue, Row, TemplatePreview, ToInsert, ToUpdate (+1 more)

### Community 64 - "Community 64"
Cohesion: 0.29
Nodes (5): ResultStatus, Determined, NeedClarification, NeedExpertReview, NoCandidateFallbackTests

### Community 66 - "Community 66"
Cohesion: 0.29
Nodes (6): src/services/apiClient.js Backend Client, src/components/App.jsx, buildPrompt(), src/services/classifier.js, src/data/kumhs_rules.json, mockResponse() Fallback

### Community 67 - "Community 67"
Cohesion: 0.33
Nodes (4): P0 Acceptance Criteria, Golden Cases (reference cases, never hard-coded), Tariff Lookup Panel (MFN, FTA list, PPN, PPh 22, source, effective dates), Mandatory Regression and Adversarial Tests

### Community 68 - "Community 68"
Cohesion: 0.29
Nodes (4): Classification Risk Flags, EN_HEADING_NOTE Treatment, legal_note Table (SECTION_NOTE, CHAPTER_NOTE, SUBHEADING_NOTE), Validator (exact code, description, active-date, hierarchy, indent, source ID)

### Community 69 - "Community 69"
Cohesion: 0.29
Nodes (7): BtkiParseResult, Issues, SkippedRows, SourceChecksum, SourceFileName, TariffNodes, TotalRows

### Community 70 - "Community 70"
Cohesion: 0.29
Nodes (7): LegalNotesParseResult, ChapterNotes, Issues, SectionNotes, SourceChecksum, SourceFileName, SubheadingNotes

### Community 71 - "Community 71"
Cohesion: 0.40
Nodes (5): Case Status State Machine, classification_case Table, expert_correction Table, fact_observation Table, LLM Permitted Actions (extraction, normalization, candidate ranking, KADL narrative)

### Community 72 - "Community 72"
Cohesion: 0.40
Nodes (4): decision_requirement Table, fact_definition Table, KUM HS 2(a) (incomplete, unfinished, unassembled, CKD/SKD, blank), KUM HS 3(b) (essential character, retail set test)

### Community 75 - "Community 75"
Cohesion: 0.40
Nodes (5): CaseStatus, Determined, NeedClarification, NeedExpertReview, Rejected

### Community 76 - "Community 76"
Cohesion: 0.40
Nodes (5): UserRole, Admin, Classifier, ExpertReviewer, Viewer

### Community 78 - "Community 78"
Cohesion: 0.40
Nodes (4): buildCommand, framework, outputDirectory, rewrites

### Community 80 - "Community 80"
Cohesion: 0.50
Nodes (4): RateUnit, Compound, Percent, Specific

## Ambiguous Edges - Review These
- `Stateless MVP (no history, no audit trail)` → `Export PDF / Audit Trail Actions`  [AMBIGUOUS]
  docs/ui_wireframe.html · relation: conceptually_related_to
- `Stateless MVP (no history, no audit trail)` → `Classification History Table`  [AMBIGUOUS]
  docs/ui_wireframe.html · relation: conceptually_related_to
- `Three-Screen Shell (Klasifikasi/Riwayat/Admin)` → `#root mount + /src/main.jsx entry`  [AMBIGUOUS]
  hs-classifier/index.html · relation: conceptually_related_to

## Knowledge Gaps
- **492 isolated node(s):** `Actor`, `UserId`, `SeesAll`, `Facts`, `UserId` (+487 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 621 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **12 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `HsClassifierDbContext` connect `EF DbContext and DbSets` to `API Controllers and Auth`, `Project Namespace Structure`, `BTKI Tariff Line Model`, `Fact Extraction and Candidates`, `Community 20`, `Community 21`, `Community 28`, `Community 29`, `Community 33`, `Community 34`, `Community 35`, `Community 36`, `Community 37`, `Community 38`, `Community 45`, `Community 46`, `Community 47`, `Community 52`, `Community 56`, `Community 63`, `Community 65`, `Community 77`, `Community 81`?**
  _High betweenness centrality (0.082) - this node is a cross-community bridge._
- **Are the 5 inferred relationships involving `CandidateHeading` (e.g. with `.Identical_parent_texts_fall_back_to_tariff_line_labels()` and `.Kum_hs_1_matches_whole_words_only()`) actually correct?**
  _`CandidateHeading` has 5 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Actor`, `UserId`, `SeesAll` to the rest of the system?**
  _492 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `API Controllers and Auth` be split into smaller, more focused modules?**
  _Cohesion score 0.056292906178489706 - nodes in this community are weakly interconnected._
- **Why does `ClassificationCase` connect `Community 34` to `API Controllers and Auth`, `Project Namespace Structure`, `Community 35`, `Community 36`, `Community 73`, `Community 75`, `Community 77`, `Fact Extraction and Candidates`, `Community 48`, `Community 81`, `EF DbContext and DbSets`, `Community 56`, `Community 63`?**
  _High betweenness centrality (0.045) - this node is a cross-community bridge._

### Low-confidence Hints
_AMBIGUOUS edges — the extractor was unsure. Verify before acting on these._

- **What is the exact relationship between `Stateless MVP (no history, no audit trail)` and `Export PDF / Audit Trail Actions`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Stateless MVP (no history, no audit trail)` and `Classification History Table`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._