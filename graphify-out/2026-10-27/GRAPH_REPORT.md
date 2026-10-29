# Graph Report - Project_AI  (2026-10-27)

## Corpus Check
- 126 files · ~112,676 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 14 file(s) not represented in the graph (top: .log 6, (none) 2, .css 2)

## Summary
- 1759 nodes · 4026 edges · 102 communities (81 shown, 21 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 324 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Task
- HsClassifier.Domain.Enums
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
- IActionResult
- EF DbContext and DbSets
- AI HS Classification Intelligence V2 — Flow Bisnis & Flow Sistem
- NomenclatureVersion
- SubheadingNote
- data/parse_btki.py
- FactSet
- Dimension
- BTKI 2022 Tariff Nomenclature
- .ParseBTKIAsync
- parse_btki_v2.py
- FtaRate
- MfnRate
- classification_cases Table
- parse_btki.py
- Fact
- AmbiguityItem
- ClassificationCase
- ImportBatch
- ReasoningEvent
- ChapterNote
- ExplanatoryNote
- ImportState
- KUM HS Rules Engine
- Deterministic KUM HS Rules Engine
- InlineData
- ParsedNote
- FactStatus
- AppUser
- SectionNote
- TaxRate
- AGENTS.md
- ClassificationService Orchestrator
- json
- DOCX Ingestion Pipeline (OOXML-first, deterministic parser)
- ExpertCorrection
- csv
- re
- candidate_evaluation Table (RETAINED / ELIMINATED / UNRESOLVED)
- ExpertReviewService
- .Compute
- Classification Validator
- enrich_btki.py
- EnParseResult
- .Evaluate
- TemplateDiffRow
- .Narrow
- VersionDiffResult
- .Assemble
- buildPrompt
- tariff_rate Table (MFN_BM, FTA_BM, PPN_IMPORT, PPH22_IMPORT)
- Validator (exact code, description, active-date, hierarchy, indent, source ID)
- BtkiParseResult
- LegalNotesParseResult
- classification_case Table
- Fact Gate (UNKNOWN / NOT_APPLICABLE / ZERO / CONFLICTING / VERIFIED)
- TemplateIngestionService
- IActionResult
- DateTime
- ParsedTariffNode
- vercel.json
- Non-Negotiable Principles (LLM cannot invent HS codes, descriptions, notes or EN)
- Fact
- ReasoningStep
- Free vs Premium Feature Gating
- .ExtractFromInput
- AmbiguityQueueTests
- DryRunResult
- ClassificationQuestion
- EffectType
- Enums.cs
- List
- Stream
- ParagraphType
- .Setup
- .Setup
- .Parse
- Fact Extraction Engine
- CaseStatus
- UserRole
- .Filter
- .DecideNoCandidate
- .Seed

## God Nodes (most connected - your core abstractions)
1. `Fact` - 75 edges
2. `CandidateHeading` - 61 edges
3. `HsClassifierDbContext` - 54 edges
4. `FactSet` - 47 edges
5. `NomenclatureVersion` - 41 edges
6. `BtkiTariffLine` - 38 edges
7. `ReasoningStep` - 36 edges
8. `RulesEngine` - 34 edges
9. `ClassificationCase` - 33 edges
10. `ParsedTariffNode` - 30 edges

## Surprising Connections (you probably didn't know these)
- `btki_reference Table` --semantically_similar_to--> `BTKI 2022 Tariff Nomenclature`  [INFERRED] [semantically similar]
  docs/DATABASE_SCHEMA.md → .kiro/specs/hs-classification-v2/requirements.md
- `users Table` --semantically_similar_to--> `JWT Auth with Admin/Classifier/Viewer Roles`  [INFERRED] [semantically similar]
  docs/DATABASE_SCHEMA.md → .kiro/specs/hs-classification-v2/tasks.md
- `audit_logs Table` --semantically_similar_to--> `Immutable Audit Trail`  [INFERRED] [semantically similar]
  docs/DATABASE_SCHEMA.md → .kiro/specs/hs-classification-v2/requirements.md
- `classification_cases Table` --semantically_similar_to--> `Reasoning Event Record`  [INFERRED] [semantically similar]
  docs/DATABASE_SCHEMA.md → .kiro/specs/hs-classification-v2/requirements.md
- `Key Code Verification (OK/MISSING)` --semantically_similar_to--> `Ambiguity Queue (parser parent uncertainty)`  [INFERRED] [semantically similar]
  data/btki_parse_report.md → docs/ui_wireframe.html

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **KUM HS Reasoning Pipeline (rules -> reasoning block -> eliminations)** — docs_user_guide_kum_hs, docs_user_guide_reasoning_kum_block, docs_user_guide_eliminated_candidates, docs_ui_wireframe_reasoning_panel, docs_ui_wireframe_eliminated_list, data_readme_kumhs_rules_json [EXTRACTED 0.85]
- **Classification Pipeline: Fact -> Candidate -> Rules -> Validate -> Tariff** — _kiro_specs_hs_classification_v2_requirements_fact_engine, _kiro_specs_hs_classification_v2_requirements_candidate_generator, _kiro_specs_hs_classification_v2_requirements_rules_engine, _kiro_specs_hs_classification_v2_requirements_validator, _kiro_specs_hs_classification_v2_requirements_tariff_lookup_module [EXTRACTED 1.00]
- **GIR 1-6 sequential conditional application flow** — docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_1, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_2a, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_3a, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_3b, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_4, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_5a, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_kum_hs_6 [EXTRACTED 1.00]
- **HS Classification Execution Pipeline** — docs_workflow_end_to_end_fact_extraction_engine, docs_workflow_end_to_end_candidate_generation, docs_workflow_end_to_end_kum_hs_rules_engine, docs_workflow_end_to_end_validator, docs_workflow_end_to_end_tariff_lookup, docs_task_list_classification_orchestrator [EXTRACTED 1.00]
- **Runtime Classification Pipeline** — kiro_specs_hs_classification_v2_design_fact_extraction_engine, kiro_specs_hs_classification_v2_design_candidate_generation_engine, kiro_specs_hs_classification_v2_design_rules_engine, kiro_specs_hs_classification_v2_design_validation_system, kiro_specs_hs_classification_v2_design_tariff_lookup_module [EXTRACTED 1.00]
- **Nomenclature Dataset Ingestion and Activation** — kiro_specs_hs_classification_v2_design_docx_parser, kiro_specs_hs_classification_v2_design_structure_validator, kiro_specs_hs_classification_v2_design_version_manager, kiro_specs_hs_classification_v2_design_import_state_machine, kiro_specs_hs_classification_v2_design_nomenclature_versions [EXTRACTED 1.00]
- **KUM HS General Rules for Interpretation 1-6** — kiro_specs_hs_classification_v2_design_rules_engine, kiro_specs_hs_classification_v2_design_kum_hs_1, kiro_specs_hs_classification_v2_design_kum_hs_2a, kiro_specs_hs_classification_v2_design_kum_hs_3a, kiro_specs_hs_classification_v2_design_kum_hs_3b, kiro_specs_hs_classification_v2_design_kum_hs_3c, kiro_specs_hs_classification_v2_design_kum_hs_6 [EXTRACTED 1.00]
- **KUM HS Rules Applied in Mandatory Sequence GRI 1-6** — _kiro_specs_hs_classification_v2_requirements_gri_1, _kiro_specs_hs_classification_v2_requirements_gri_2a, _kiro_specs_hs_classification_v2_requirements_gri_2b, _kiro_specs_hs_classification_v2_requirements_gri_3a, _kiro_specs_hs_classification_v2_requirements_gri_3b, _kiro_specs_hs_classification_v2_requirements_gri_3c, _kiro_specs_hs_classification_v2_requirements_gri_4, _kiro_specs_hs_classification_v2_requirements_gri_5, _kiro_specs_hs_classification_v2_requirements_gri_6 [EXTRACTED 1.00]
- **Disclaimer & Audit Governance Layer** — docs_workflow_end_to_end_fta_disclaimer, docs_project_summary_djbc_disclaimer, docs_workflow_end_to_end_reasoning_events_audit_trail [INFERRED 0.75]
- **Recommendation-Not-Determination Safeguards** — docs_user_guide_recommendation_only, docs_user_guide_confidence_level, docs_user_guide_expert_review_flag, docs_ui_wireframe_fta_disclaimer, docs_ui_wireframe_tariff_tax_panel [INFERRED 0.75]
- **V1 MVP Limitations That Motivate the V2 Redesign** — docs_technical_stateless_frontend_only, docs_technical_known_limitations, _kiro_specs_hs_classification_v2_requirements_no_llm_hs_generation, _kiro_specs_hs_classification_v2_requirements_audit_trail [INFERRED 0.75]
- **BTKI Data Version Lifecycle (upload -> parse -> validate -> ambiguity -> commit)** — data_btki_parse_report_v2, data_btki_parse_report_code_counts, docs_ui_wireframe_version_state_machine, docs_ui_wireframe_validation_report, docs_ui_wireframe_ambiguity_queue, docs_ui_wireframe_upload_document_types [INFERRED 0.85]
- **BTKI Reference Data Ingestion Path** — docs_workflow_end_to_end_docx_parser, docs_workflow_end_to_end_version_state_machine, docs_workflow_end_to_end_structure_ambiguity, docs_task_list_fase_1_database, docs_task_list_fase_1b_template_ingestion [INFERRED 0.85]
- **Evidence, reasoning events, confidence and audit trail stack** — docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_fact_observation, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_candidate_evaluation, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_reasoning_event, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_confidence_model, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_audit_trail, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_expert_correction [INFERRED 0.85]
- **BTKI/EN master data chain from DOCX to validated final code** — docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_docx_ingestion_pipeline, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_tariff_node, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_legal_note, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_explanatory_notes_paragraph, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_validator_component, docs_ai_hs_classification_intelligence_v2_product_logic_ui_and_coding_prompt_output_json_schema [INFERRED 0.85]

## Communities (102 total, 21 thin omitted)

### Community 0 - "Task"
Cohesion: 0.17
Nodes (5): IVersionManager, BtkiImportService, CarryReport, VersionManager, VersionLifecycleTests

### Community 1 - "HsClassifier.Domain.Enums"
Cohesion: 0.06
Nodes (22): HsClassifier.Domain.Enums, HsClassifier.Infrastructure.Persistence, HsClassifier.Infrastructure.Parsing, HsClassifier.Infrastructure.Template, HsClassifier.Infrastructure.Seeding, HsClassifier.Application.Classification.Rules, HsClassifier.Tests, HsClassifier.Infrastructure.Services (+14 more)

### Community 2 - "React App Shell and Views"
Cohesion: 0.05
Nodes (83): src/services/apiClient.js Backend Client, src/components/App.jsx, src/services/classifier.js, mockResponse() Fallback, App(), ClarificationBanner(), LoginScreen(), viewFromPath() (+75 more)

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
Nodes (35): dependencies, react, react-dom, tailwindcss, @tailwindcss/vite, devDependencies, eslint, @eslint/js (+27 more)

### Community 7 - "Auth and DI Wiring"
Cohesion: 0.07
Nodes (22): AuthController, LoginRequest, IJwtService, DependencyInjection, GeminiTermSuggester, Model, GroqTermSuggester, BaseUrl (+14 more)

### Community 8 - "Solution and NuGet References"
Cohesion: 0.07
Nodes (34): HsClassifier.API, net8.0, Microsoft.EntityFrameworkCore.Design (9.0.19), Microsoft.EntityFrameworkCore.SqlServer (9.0.19), HsClassifier.Application, net8.0, Microsoft.NET.Sdk, HsClassifier.Domain (+26 more)

### Community 9 - "API Launch Config"
Cohesion: 0.07
Nodes (28): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, launchUrl, applicationUrl (+20 more)

### Community 11 - "Classification Result DTOs"
Cohesion: 0.07
Nodes (29): ClassificationSessionResult, CandidateCodes, CaseId, Confidence, ConfidenceDetail, ConfidenceScore, EliminatedCount, FinalCode (+21 more)

### Community 12 - "Question Generation"
Cohesion: 0.10
Nodes (17): CandidateHeading, ChapterCode, ChapterNotesText, ChildDescriptions, ChildSubheadings, Code, Description, EnParagraphs (+9 more)

### Community 13 - "KUM HS6 Determination"
Cohesion: 0.10
Nodes (20): ClassificationResult, Confidence, EliminatedCandidates, FinalCode, FinalDescription, LockedHeading, LockedHeadingScore, MissingFacts (+12 more)

### Community 14 - "BTKI Tariff Line Model"
Cohesion: 0.08
Nodes (23): BtkiTariffLine, ChapterCode, Code, DescriptionEn, DescriptionId, EffectiveFrom, EffectiveTo, FtaRates (+15 more)

### Community 15 - "Fact Extraction and Candidates"
Cohesion: 0.24
Nodes (4): CandidateGeneratorService, ClassificationOrchestrator, ClassificationValidator, TariffLookupService

### Community 17 - "IActionResult"
Cohesion: 0.13
Nodes (9): AdminImportController, Actor, AmbiguityDecisionRequest, AdminTemplateController, RegisterRequest, ClassificationStartRequest, Facts, TransitionRequest (+1 more)

### Community 18 - "EF DbContext and DbSets"
Cohesion: 0.11
Nodes (16): HsClassifierDbContext, AmbiguityItems, AppUsers, BtkiTariffLines, ChapterNotes, ClassificationCases, ExpertCorrections, ExplanatoryNotes (+8 more)

### Community 19 - "AI HS Classification Intelligence V2 — Flow Bisnis & Flow Sistem"
Cohesion: 0.08
Nodes (23): 10. Batasan Saat Ini, 1. Ringkasan Satu Paragraf, 2. Prinsip yang Dipegang Sistem, 3. Peran Pengguna, 4. Gambaran Besar Alur End-to-End, 5.1 Langkah demi langkah, 5.2 Pertanyaan subpos (KUM HS 6), 5.3 Barang dengan nama pasar / merek (bantuan AI) (+15 more)

### Community 20 - "NomenclatureVersion"
Cohesion: 0.11
Nodes (18): NomenclatureVersion, BtkiEdition, ChapterNotes, CreatedAt, CreatedBy, Description, EffectiveFrom, EffectiveTo (+10 more)

### Community 21 - "SubheadingNote"
Cohesion: 0.18
Nodes (11): SubheadingNote, EffectType, HeadingCode, Id, NoteNumber, ReferredCodesJson, SourceId, TargetCodesJson (+3 more)

### Community 22 - "data/parse_btki.py"
Cohesion: 0.16
Nodes (8): clean(), get_bab(), get_seksi(), is_tariff_table(), main(), node_type(), normalize_hs(), parse_table()

### Community 23 - "FactSet"
Cohesion: 0.38
Nodes (3): ConditionGate, FactSet, Facts

### Community 24 - "Dimension"
Cohesion: 0.15
Nodes (10): Constraint, Dimension, ApparentPower, Length, Mass, Power, Voltage, Volume (+2 more)

### Community 25 - "BTKI 2022 Tariff Nomenclature"
Cohesion: 0.26
Nodes (13): Admin Data Center, BTKI 2022 Tariff Nomenclature, Candidate Generation Engine, CSV/Excel Template Ingestion Path, Explanatory Notes (EN) Ingestion, Section, Chapter and Subheading Notes, Nomenclature Version and Activation, STRUCTURE_AMBIGUITY Review Queue (+5 more)

### Community 27 - "parse_btki_v2.py"
Cohesion: 0.23
Nodes (9): clean(), extract_bm_from_cell(), extract_codes_from_cell(), extract_texts_from_cell(), get_seksi(), is_tariff_table(), main(), node_type() (+1 more)

### Community 28 - "FtaRate"
Cohesion: 0.14
Nodes (14): FtaRate, ConditionsSummary, EffectiveFrom, EffectiveTo, Id, IsActive, PartnerCountryCode, RateUnit (+6 more)

### Community 29 - "MfnRate"
Cohesion: 0.14
Nodes (14): MfnRate, Currency, EffectiveFrom, EffectiveTo, Formula, Id, IsActive, QuantityUnit (+6 more)

### Community 30 - "classification_cases Table"
Cohesion: 0.20
Nodes (11): Immutable Audit Trail, KADL - Klasifikasi Atas Dasar Legalitas, Reasoning Event Record, JWT Auth with Admin/Classifier/Viewer Roles, audit_logs Table, classification_cases Table, daily_usage Table, expert_reviews Table (+3 more)

### Community 31 - "parse_btki.py"
Cohesion: 0.19
Nodes (4): clean(), is_header_row(), parse_bm(), parse_hs()

### Community 32 - "Fact"
Cohesion: 0.18
Nodes (10): Fact, ConfidenceScore, EvidenceReference, NormalizedValue, RawValue, Source, Status, Type (+2 more)

### Community 33 - "AmbiguityItem"
Cohesion: 0.15
Nodes (13): AmbiguityItem, Batch, BatchId, Code, CorrectedParent, DecidedAt, DecidedBy, Decision (+5 more)

### Community 34 - "ClassificationCase"
Cohesion: 0.13
Nodes (15): ClassificationCase, CandidateCodesJson, ClassificationDate, Confidence, ConfidenceJson, ConfidenceScore, CreatedAt, FactsJson (+7 more)

### Community 35 - "ImportBatch"
Cohesion: 0.15
Nodes (12): ImportBatch, Checksum, CreatedAt, CreatedBy, ErrorCount, FileName, Id, Items (+4 more)

### Community 36 - "ReasoningEvent"
Cohesion: 0.15
Nodes (13): ReasoningEvent, CandidatesAfterJson, CandidatesBeforeJson, Case, CaseId, Decision, EventOrder, FactsUsedJson (+5 more)

### Community 37 - "ChapterNote"
Cohesion: 0.15
Nodes (13): ChapterNote, ChapterNumber, EffectType, Id, NoteNumber, ParagraphNumber, ReferredCodesJson, SourceId (+5 more)

### Community 38 - "ExplanatoryNote"
Cohesion: 0.15
Nodes (13): ExplanatoryNote, HeadingCode, Id, NextParagraphId, ParagraphNumber, ParagraphType, ParentSectionId, PreviousParagraphId (+5 more)

### Community 39 - "ImportState"
Cohesion: 0.17
Nodes (12): ImportState, Active, Committed, DryRunFailed, DryRunPassed, Indexed, Mapped, Parsing (+4 more)

### Community 40 - "KUM HS Rules Engine"
Cohesion: 0.35
Nodes (12): Essential Character Determination, GRI 1 - Heading Terms, Notes and EN, GRI 2(a) - Incomplete and Unfinished Goods, GRI 2(b) - Material Extension to Mixtures, GRI 3(a) - Most Specific Heading, GRI 3(b) - Essential Character, GRI 3(c) - Last in Numerical Order, GRI 4 - Most Akin Goods (+4 more)

### Community 41 - "Deterministic KUM HS Rules Engine"
Cohesion: 0.18
Nodes (10): KUM HS 1 (heading terms, Section Notes, Chapter Notes, EN assistance), KUM HS 2(b) (mixtures and combinations of materials), KUM HS 3(a) (most specific heading), KUM HS 3(c) (last in numerical order), KUM HS 5(a) (special cases and containers), KUM HS 5(b) (packing materials and packing containers), KUM HS 6 (same-level subheading comparison after heading lock), kum_hs_rule Table (GRI_1..GRI_6 trigger/evaluation schema) (+2 more)

### Community 43 - "ParsedNote"
Cohesion: 0.17
Nodes (12): ParsedNote, EffectType, NoteNumber, NoteType, ParagraphNumber, ReferredCodesJson, ScopeCode, SourceId (+4 more)

### Community 44 - "FactStatus"
Cohesion: 0.17
Nodes (11): FactSource, Document, Expert, System, User, FactStatus, Conflicting, NotApplicable (+3 more)

### Community 45 - "AppUser"
Cohesion: 0.29
Nodes (7): AppUser, CreatedAt, Email, Id, IsActive, PasswordHash, Role

### Community 46 - "SectionNote"
Cohesion: 0.17
Nodes (12): SectionNote, EffectType, Id, NoteNumber, ParagraphNumber, ReferredCodesJson, SectionNumber, SourceId (+4 more)

### Community 47 - "TaxRate"
Cohesion: 0.12
Nodes (16): TaxRate, EffectiveFrom, EffectiveTo, Id, IsActive, RateUnit, RateValue, SourceId (+8 more)

### Community 49 - "ClassificationService Orchestrator"
Cohesion: 0.33
Nodes (6): Tariff Lookup Module, ICandidateGenerator (13 Search Strategies), ClassificationService Orchestrator, Clean Architecture Backend Layers, IRulesEngine (ApplyKUMHS), ITariffLookupModule

### Community 50 - "json"
Cohesion: 0.25
Nodes (4): fmt_code(), fmt_code4(), fmt_code6(), main()

### Community 51 - "DOCX Ingestion Pipeline (OOXML-first, deterministic parser)"
Cohesion: 0.22
Nodes (8): Admin Data Center, DOCX Visual Verification Workflow (render, sample, side-by-side, reconcile, approve), EXPLANATORY NOTES (EN)_DATA_NOT_LOADED Status, explanatorynotes(en)_document Table, explanatorynotes(en)_paragraph Table, OOXML Structure Mapping (styles, outline level, numbering, indentation, tables), Minimum Roles (USER..AUDITOR), Security Controls (untrusted documents, tenant isolation, RBAC)

### Community 52 - "ExpertCorrection"
Cohesion: 0.18
Nodes (11): ExpertCorrection, Action, AfterCode, AfterStatus, BeforeCode, BeforeStatus, CaseId, CreatedAt (+3 more)

### Community 53 - "csv"
Cohesion: 0.36
Nodes (4): get_seksi(), load_existing_seed(), load_hs6_from_csv(), main()

### Community 54 - "re"
Cohesion: 0.33
Nodes (4): is_vague(), is_vague(), is_vague(), main()

### Community 55 - "candidate_evaluation Table (RETAINED / ELIMINATED / UNRESOLVED)"
Cohesion: 0.27
Nodes (8): candidate_evaluation Table (RETAINED / ELIMINATED / UNRESOLVED), Confidence Model (weighted components), Implementation Milestones 1-6, KADL (Klasifikasi Atas Dasar Legal) Human Output, KUM HS 4 (goods most akin), Mandatory Output JSON Schema, Retrieval Engine (BTKI nodes, ancestors, notes, EN, cross-refs), V2 Classification Wizard (11 steps)

### Community 56 - "ExpertReviewService"
Cohesion: 0.26
Nodes (5): ReviewController, UserId, ReviewDecisionRequest, ExpertReviewService, ReviewOutcome

### Community 57 - ".Compute"
Cohesion: 0.22
Nodes (6): ConfidenceCalculator, ConfidenceComponent, Points, ConfidenceInput, ConfidenceReport, ConfidenceTests

### Community 58 - "Classification Validator"
Cohesion: 0.22
Nodes (8): Source ID Traceability, Classification Validator, 20 Correctness Properties (Property-Based Tests), IValidator (ValidateClassification), callGroq(), Groq Chat Completions API, Known Limitations of the V1 MVP, llama-3.3-70b-versatile Model

### Community 59 - "enrich_btki.py"
Cohesion: 0.33
Nodes (5): dash_level(), is_vague(), main(), enrich_uraian(), get_parent_uraian()

### Community 60 - "EnParseResult"
Cohesion: 0.13
Nodes (15): EnParseResult, Issues, Paragraphs, SourceChecksum, SourceFileName, TotalParagraphs, ParsedEnParagraph, HeadingCode (+7 more)

### Community 62 - "TemplateDiffRow"
Cohesion: 0.18
Nodes (9): TemplateDiffRow, Code, NewValue, OldValue, Row, TemplatePreview, ToInsert, ToUpdate (+1 more)

### Community 64 - "VersionDiffResult"
Cohesion: 0.14
Nodes (14): DiffEntry, Chapter, Level, LineText, VersionDiff, VersionDiffResult, Added, BaselineCount (+6 more)

### Community 65 - ".Assemble"
Cohesion: 0.07
Nodes (29): ParseIssue, Code, Message, RowNumber, Severity, Type, BtkiHierarchyBuilder, BtkiAssembly (+21 more)

### Community 67 - "tariff_rate Table (MFN_BM, FTA_BM, PPN_IMPORT, PPH22_IMPORT)"
Cohesion: 0.33
Nodes (4): P0 Acceptance Criteria, Golden Cases (reference cases, never hard-coded), Tariff Lookup Panel (MFN, FTA list, PPN, PPh 22, source, effective dates), Mandatory Regression and Adversarial Tests

### Community 68 - "Validator (exact code, description, active-date, hierarchy, indent, source ID)"
Cohesion: 0.29
Nodes (4): Classification Risk Flags, EN_HEADING_NOTE Treatment, legal_note Table (SECTION_NOTE, CHAPTER_NOTE, SUBHEADING_NOTE), Validator (exact code, description, active-date, hierarchy, indent, source ID)

### Community 69 - "BtkiParseResult"
Cohesion: 0.14
Nodes (14): BtkiParseResult, IngestionMethod, Issues, MissingGroupCodes, SkippedRows, SourceChecksum, SourceFileName, TariffNodes (+6 more)

### Community 70 - "LegalNotesParseResult"
Cohesion: 0.29
Nodes (7): LegalNotesParseResult, ChapterNotes, Issues, SectionNotes, SourceChecksum, SourceFileName, SubheadingNotes

### Community 71 - "classification_case Table"
Cohesion: 0.40
Nodes (5): Case Status State Machine, classification_case Table, expert_correction Table, fact_observation Table, LLM Permitted Actions (extraction, normalization, candidate ranking, KADL narrative)

### Community 72 - "Fact Gate (UNKNOWN / NOT_APPLICABLE / ZERO / CONFLICTING / VERIFIED)"
Cohesion: 0.40
Nodes (4): decision_requirement Table, fact_definition Table, KUM HS 2(a) (incomplete, unfinished, unassembled, CKD/SKD, blank), KUM HS 3(b) (essential character, retail set test)

### Community 73 - "TemplateIngestionService"
Cohesion: 0.18
Nodes (9): TemplateIngestionService, TemplateRowError, TemplateValidationResult, Errors, IsValid, ParsedRows, TotalRows, ValidRows (+1 more)

### Community 75 - "IActionResult"
Cohesion: 0.23
Nodes (4): BtkiController, ClassificationController, SeesAll, UserId

### Community 76 - "DateTime"
Cohesion: 0.15
Nodes (5): CreateVersionRequest, InitialCreate, ExpertReviewAndImportBatches, ConfidenceScore, HsClassifierDbContextModelSnapshot

### Community 77 - "ParsedTariffNode"
Cohesion: 0.11
Nodes (19): ParsedTariffNode, AmbiguityReason, ChapterCode, Code, DescriptionEn, DescriptionId, HeadingCode, IndentLevel (+11 more)

### Community 78 - "vercel.json"
Cohesion: 0.40
Nodes (4): buildCommand, framework, outputDirectory, rewrites

### Community 81 - "ReasoningStep"
Cohesion: 0.15
Nodes (13): ConditionOutcome, NarrowResult, CandidateDecision, Eliminated, Retained, Unresolved, ReasoningStep, CandidateCode (+5 more)

### Community 84 - "AmbiguityQueueTests"
Cohesion: 0.29
Nodes (3): AmbiguityGate, AmbiguityResolution, AmbiguityQueueTests

### Community 85 - "DryRunResult"
Cohesion: 0.29
Nodes (7): DryRunResult, AmbiguousNodes, CanCommit, ErrorCount, Issues, TotalNodes, WarningCount

### Community 86 - "ClassificationQuestion"
Cohesion: 0.18
Nodes (12): CandidateImpact, CandidatesAffected, EliminatedCodes, RetainedCodes, ClassificationQuestion, Choices, FactType, Impact (+4 more)

### Community 87 - "EffectType"
Cohesion: 0.29
Nodes (7): EffectType, Condition, Definition, Exclusion, Inclusion, Priority, Referral

### Community 88 - "Enums.cs"
Cohesion: 0.17
Nodes (11): AmbiguityDecision, Approved, Pending, Rejected, ConfidenceLevel, High, Low, Medium (+3 more)

### Community 90 - "Stream"
Cohesion: 0.27
Nodes (3): IBtkiPdfParser, IDocxParser, BtkiPdfParser

### Community 91 - "ParagraphType"
Cohesion: 0.22
Nodes (9): ParagraphType, Borderline, CrossReference, Definition, Example, Exclusion, Inclusion, Scope (+1 more)

### Community 95 - "Fact Extraction Engine"
Cohesion: 0.40
Nodes (5): Fact Extraction Engine, Question Prioritisation by Information Gain, NEED_CLARIFICATION Status, IFactExtractor (35 Fact Types), IQuestionGenerator

### Community 96 - "CaseStatus"
Cohesion: 0.40
Nodes (5): CaseStatus, Determined, NeedClarification, NeedExpertReview, Rejected

### Community 97 - "UserRole"
Cohesion: 0.40
Nodes (5): UserRole, Admin, Classifier, ExpertReviewer, Viewer

## Ambiguous Edges - Review These
- `Export PDF / Audit Trail Actions` → `Stateless MVP (no history, no audit trail)`  [AMBIGUOUS]
  docs/ui_wireframe.html · relation: conceptually_related_to
- `Classification History Table` → `Stateless MVP (no history, no audit trail)`  [AMBIGUOUS]
  docs/ui_wireframe.html · relation: conceptually_related_to
- `Three-Screen Shell (Klasifikasi/Riwayat/Admin)` → `#root mount + /src/main.jsx entry`  [AMBIGUOUS]
  hs-classifier/index.html · relation: conceptually_related_to

## Knowledge Gaps
- **533 isolated node(s):** `Actor`, `UserId`, `SeesAll`, `Facts`, `UserId` (+528 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 694 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **21 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `HsClassifierDbContext` connect `EF DbContext and DbSets` to `Task`, `HsClassifier.Domain.Enums`, `Auth and DI Wiring`, `BTKI Tariff Line Model`, `Fact Extraction and Candidates`, `IActionResult`, `NomenclatureVersion`, `SubheadingNote`, `FtaRate`, `MfnRate`, `AmbiguityItem`, `ClassificationCase`, `ImportBatch`, `ReasoningEvent`, `ChapterNote`, `ExplanatoryNote`, `AppUser`, `SectionNote`, `TaxRate`, `ExpertCorrection`, `ExpertReviewService`, `TemplateIngestionService`, `IActionResult`, `DateTime`, `.Setup`, `.Setup`, `.Seed`?**
  _High betweenness centrality (0.052) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `Fact` (e.g. with `.ExtractFromInput()` and `.MergeLlmFacts()`) actually correct?**
  _`Fact` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Actor`, `UserId`, `SeesAll` to the rest of the system?**
  _533 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `HsClassifier.Domain.Enums` be split into smaller, more focused modules?**
  _Cohesion score 0.058848797250859106 - nodes in this community are weakly interconnected._
- **Why does `ReasoningStep` connect `ReasoningStep` to `KUM HS Rules Engine`, `Question Generation`, `KUM HS6 Determination`, `DateTime`, `Fact Extraction and Candidates`, `ClassificationQuestion`, `FactSet`, `List`, `.Narrow`?**
  _High betweenness centrality (0.047) - this node is a cross-community bridge._

### Low-confidence Hints
_AMBIGUOUS edges — the extractor was unsure. Verify before acting on these._

- **What is the exact relationship between `Export PDF / Audit Trail Actions` and `Stateless MVP (no history, no audit trail)`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **What is the exact relationship between `Classification History Table` and `Stateless MVP (no history, no audit trail)`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._