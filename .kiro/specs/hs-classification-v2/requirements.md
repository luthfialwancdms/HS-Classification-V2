# Requirements Document

## Introduction

This document specifies the requirements for upgrading AI HS Classification Intelligence from V1 to V2. The V2 system transforms a simple form-based LLM classifier into a systematic, database-centric, legal-grade classification system compliant with Indonesian BTKI 2022, KUM HS (General Rules for Interpretation) 1–6, Section Notes, Chapter Notes, Subheading Notes, and Explanatory Notes.

**Current V1 System:**
- 14-field form → LLM directly predicts HS code → Result
- Risks: hallucination, no audit trail, no source validation
- Frontend-only: React + Vite + Tailwind + Groq API

**Target V2 System:**
- Database-centric retrieval (BTKI 2022: 14,273 entries, EN: 65,833 paragraphs)
- Fact extraction engine with dynamic questioning
- Candidate generation from database (NOT LLM)
- Rules engine applying KUM HS 1–6 sequentially
- Validation system with source ID traceability
- Tariff lookup (MFN, FTA, PPN, PPh 22)
- Complete audit trail with reasoning events
- Backend architecture: C# + MySQL
- Frontend: existing React + Vite (to be integrated)

**Non-Negotiable Constraints:**
1. LLM MUST NOT generate HS codes (only from database)
2. LLM MUST NOT generate official descriptions (only from database)
3. LLM MUST NOT quote EN from memory (only from database)
4. LLM MUST NOT guess missing facts (must ask user)
5. Every decision MUST have source_id traceable to original document
6. Validation MUST reject if code not found, inactive, wrong digits, parent missing, or description mismatch


## Glossary

- **BTKI (Buku Tarif Kepabeanan Indonesia)**: Indonesian Customs Tariff Book based on HS 2022 and AHTN, containing 14,273 tariff lines organized hierarchically (Section → Chapter → Heading → Subheading → Tariff Line)
- **HS Code**: Harmonized System code, 8-digit format in BTKI (XXXX.XX.XX) where first 6 digits are international HS, digits 7-8 are national
- **KUM_HS (GRI - General Rules for Interpretation)**: Six sequential legal rules (GRI 1-6) mandated by WCO for HS classification
- **EN (Explanatory Notes)**: Official interpretative guidance for HS nomenclature, providing scope, inclusion, exclusion, and borderline guidance per heading
- **Admin_Data_Center**: System component providing administrative interface for importing, validating, and activating BTKI/EN datasets
- **Fact_Engine**: System component extracting objective facts from user input and documents
- **Candidate_Generator**: System component retrieving candidate HS codes from database using lexical, semantic, and function-based search
- **Rules_Engine**: System component applying KUM HS 1-6 sequentially with structured reasoning
- **Validator**: System component performing exact code validation, parent hierarchy validation, and source ID traceability
- **Tariff_Lookup_Module**: System component retrieving MFN, FTA, PPN, and PPh 22 rates from database
- **KADL (Klasifikasi Atas Dasar Legalitas)**: Legal-basis classification reasoning chain
- **Audit_Trail**: Immutable log of all classification decisions with timestamps, actors, and source references
- **Reasoning_Event**: Structured record of a single decision step with input facts, rule applied, candidates before/after, and source IDs
- **Source_ID**: Unique identifier linking every decision to original document location (file, page, paragraph, table, cell)
- **Nomenclature_Version**: Specific edition of BTKI with effective date range and activation status


## Requirements

### Requirement 1: Admin Data Center - BTKI Database Ingestion

**User Story:** As a Data Administrator, I want to import BTKI 2022 from Microsoft Word DOCX files, so that the system has an authoritative source of HS codes and official descriptions.

#### Acceptance Criteria

1. WHEN a DOCX file containing BTKI is uploaded, THE Admin_Data_Center SHALL parse the OOXML structure to extract tariff nodes
2. THE Admin_Data_Center SHALL extract section number, chapter number, heading code, subheading code, tariff line code, indentation level, official description (Indonesian), official description (English), unit, MFN duty rate, FTA rates per scheme, PPN rate, PPh 22 rate, footnotes, and effective dates
3. THE Admin_Data_Center SHALL detect paragraph styles, outline levels, numbering definitions, indentation values, table structures, merged cells, and footnotes
4. WHEN a code's parent hierarchy cannot be determined from structure signals, THE Admin_Data_Center SHALL flag STRUCTURE_AMBIGUITY and place the entry in review queue
5. THE Admin_Data_Center SHALL generate a mapping report showing source paragraph/table location for each extracted tariff node
6. THE Admin_Data_Center SHALL calculate and store the source file checksum, document part, page number, paragraph index, table index, row index, and cell index for each tariff node
7. THE Admin_Data_Center SHALL reconcile total row count, heading count, subheading count, and tariff line count against expected values
8. THE Admin_Data_Center SHALL detect and report duplicate codes, invalid codes, missing parents, orphan nodes, inconsistent indentation, overlapping effective dates, and empty descriptions
9. THE Admin_Data_Center SHALL render side-by-side comparison view between source Word document and parsed records
10. WHEN tracked changes or unresolved revisions are detected in DOCX, THE Admin_Data_Center SHALL report them and require administrator confirmation before proceeding
11. THE Admin_Data_Center SHALL require administrator approval for all STRUCTURE_AMBIGUITY entries before allowing dataset activation
12. THE Admin_Data_Center SHALL preserve original Word file in read-only storage with checksum for audit purposes


### Requirement 2: Admin Data Center - Explanatory Notes Ingestion

**User Story:** As a Data Administrator, I want to import Explanatory Notes from Microsoft Word DOCX files, so that the system can interpret heading scope and resolve borderline cases.

#### Acceptance Criteria

1. WHEN a DOCX file containing Explanatory Notes is uploaded, THE Admin_Data_Center SHALL parse the OOXML structure to extract EN paragraphs
2. THE Admin_Data_Center SHALL extract edition, section, chapter, heading code, subheading code if available, heading title, paragraph number, sub-title, scope statement, definition, inclusion, exclusion, technical characteristics, examples, borderline guidance, cross-references to other headings, tables, lists, and images with captions
3. THE Admin_Data_Center SHALL segment EN content by heading and paragraph semantic unit, NOT by fixed character count
4. WHEN one exclusion statement or cross-reference spans multiple paragraphs, THE Admin_Data_Center SHALL preserve the complete semantic context without truncation
5. THE Admin_Data_Center SHALL store previous_paragraph_id, next_paragraph_id, parent_section_id, and referred_codes for each EN paragraph
6. THE Admin_Data_Center SHALL calculate and store source file checksum, document part, page number, paragraph index, and OOXML element reference for each EN paragraph
7. THE Admin_Data_Center SHALL generate EN heading coverage report showing which HS headings have corresponding EN paragraphs
8. THE Admin_Data_Center SHALL detect and report EN paragraphs without valid heading reference
9. THE Admin_Data_Center SHALL reconcile total paragraph count against expected values
10. THE Admin_Data_Center SHALL require administrator approval before activating EN dataset


### Requirement 3: Admin Data Center - Legal Notes Ingestion

**User Story:** As a Data Administrator, I want to import Section Notes, Chapter Notes, and Subheading Notes, so that the system can apply legal exclusions, definitions, and conditions during classification.

#### Acceptance Criteria

1. WHEN legal notes are imported, THE Admin_Data_Center SHALL classify each note by source_type: SECTION_NOTE, CHAPTER_NOTE, or SUBHEADING_NOTE
2. THE Admin_Data_Center SHALL extract section number, chapter number, target codes array, note number, paragraph number, text (Indonesian), text (English), effect_type (DEFINITION, INCLUSION, EXCLUSION, CONDITION, REFERRAL, PRIORITY), and referred_codes array
3. WHEN a Chapter Note explicitly targets specific headings (e.g., "for purposes of heading 04.03"), THE Admin_Data_Center SHALL store the source_type as CHAPTER_NOTE with target_heading populated
4. THE Admin_Data_Center SHALL calculate and store source_id with file checksum, document part, page number, paragraph index, and OOXML reference
5. THE Admin_Data_Center SHALL reconcile note count per section and chapter against expected values
6. THE Admin_Data_Center SHALL detect and report notes without valid scope reference
7. THE Admin_Data_Center SHALL require administrator approval before activating legal notes dataset


### Requirement 4: Admin Data Center - Version Management and Activation

**User Story:** As a Data Administrator, I want to manage multiple BTKI versions with effective dates, so that the system can classify goods using the correct nomenclature for the import date.

#### Acceptance Criteria

1. THE Admin_Data_Center SHALL allow creation of nomenclature versions with name, jurisdiction, HS edition, AHTN edition, BTKI edition, effective_from date, effective_to date, regulation number, and source document references
2. WHEN a new dataset is imported, THE Admin_Data_Center SHALL assign import state: UPLOADED → PARSING → MAPPED → VALIDATING → DRY_RUN_PASSED or DRY_RUN_FAILED → READY_TO_COMMIT → COMMITTED → INDEXED → ACTIVE
3. THE Admin_Data_Center SHALL execute dry run validation before allowing commit, checking for duplicate codes, invalid hierarchy, missing parents, orphan nodes, and date overlaps
4. THE Admin_Data_Center SHALL allow administrator to activate only one nomenclature version per effective date range
5. THE Admin_Data_Center SHALL allow administrator to deactivate a version, preventing its use in new classifications while preserving historical classification records
6. THE Admin_Data_Center SHALL allow administrator to rollback an activation without deleting audit records
7. THE Admin_Data_Center SHALL provide version comparison view showing added, modified, and deleted tariff nodes between two versions
8. THE Admin_Data_Center SHALL rebuild search index after version activation


### Requirement 5: Fact Extraction Engine - Objective Fact Extraction

**User Story:** As a Classifier, I want the system to extract objective facts from my product description and documents, so that classification is based on verifiable evidence rather than subjective interpretation.

#### Acceptance Criteria

1. WHEN user input and documents are provided, THE Fact_Engine SHALL identify and extract at minimum 35 fact types: commercial name, generic name, brand, model, material, composition, percentage/concentration, form, dimensions, weight, capacity, power/voltage, manufacturing process, primary function, secondary function, operating principle, input-process-output, power source, primary use, operating area, mobility, mounting, complete/incomplete status, finished/unfinished status, assembled/unassembled status, CKD/SKD designation, bulk/unit presentation, mixture/composite designation, set/kit designation, part/accessory designation, used condition, waste/scrap status, packaging/container, and supporting documents
2. THE Fact_Engine SHALL store for each extracted fact: raw value, normalized value, unit if applicable, source type (USER, DOCUMENT, EXPERT, SYSTEM), evidence reference, confidence score, and verification status
3. THE Fact_Engine SHALL distinguish between fact statuses: UNKNOWN (not provided), NOT_APPLICABLE (not relevant for this product), ZERO (value is actually zero), CONFLICTING (multiple different values detected), and VERIFIED (confirmed)
4. THE Fact_Engine SHALL NOT assign default values to missing facts
5. WHEN facts required for discrimination between candidates are missing, THE Fact_Engine SHALL return status NEED_CLARIFICATION with missing_facts array, questions array ordered by information gain, and candidate_impact array showing which candidates each fact discriminates


### Requirement 6: Fact Extraction Engine - Dynamic Fact Discovery

**User Story:** As a Classifier, I want the system to ask me targeted questions when critical facts are missing, so that I provide only the information needed for accurate classification without filling unnecessary fields.

#### Acceptance Criteria

1. WHEN candidate HS codes require discriminator facts not yet provided, THE Fact_Engine SHALL generate targeted questions for those facts
2. THE Fact_Engine SHALL prioritize questions by information gain, asking first the fact that eliminates the most candidates
3. THE Fact_Engine SHALL provide question context showing which candidate codes will be affected by the answer
4. WHEN a fact is marked NOT_APPLICABLE by user, THE Fact_Engine SHALL NOT ask for that fact again in the same classification session
5. WHEN contradictory fact values are detected from different sources, THE Fact_Engine SHALL flag CONFLICTING status and request user confirmation of the correct value


### Requirement 7: Candidate Generation Engine - Database-Centric Retrieval

**User Story:** As a Classifier, I want candidate HS codes retrieved from the BTKI database using multiple search strategies, so that all potentially applicable headings are considered based on actual nomenclature.

#### Acceptance Criteria

1. THE Candidate_Generator SHALL retrieve candidate headings from database using at minimum these search strategies: exact lexical match, synonym/terminology mapping, multilingual search (Indonesian and English), semantic similarity search, function-based search, material-based search, composition-based search, manufacturing process search, form/presentation search, use/principal-use search, parent-machine search, legal-note referral, and EN cross-reference
2. THE Candidate_Generator SHALL NOT generate HS codes or descriptions not found in the active nomenclature version database
3. FOR ALL candidate headings retrieved, THE Candidate_Generator SHALL include: official heading description, parent hierarchy (section → chapter → heading), applicable Section Notes, applicable Chapter Notes, notes explicitly targeting that heading, applicable Subheading Notes, EN heading paragraphs, child subheadings, required discriminator facts, and cross-referenced headings
4. THE Candidate_Generator SHALL assign generation_source to each candidate indicating which search strategy retrieved it
5. FOR ALL candidates, THE Candidate_Generator SHALL include source_id traceable to database records


### Requirement 8: KUM HS Rules Engine - GRI 1 Mandatory Application

**User Story:** As a Classifier, I want the system to apply KUM HS 1 (GRI 1) to every candidate heading, so that classification follows the legal requirement to interpret heading terms, section notes, chapter notes, and explanatory notes.

#### Acceptance Criteria

1. FOR ALL candidate headings, THE Rules_Engine SHALL apply KUM HS 1 by comparing objective facts against heading description terms
2. THE Rules_Engine SHALL apply Section Notes applicable to the candidate heading's section
3. THE Rules_Engine SHALL apply Chapter Notes applicable to the candidate heading's chapter
4. THE Rules_Engine SHALL apply definition, inclusion, exclusion, condition, and referral effects from applicable notes
5. THE Rules_Engine SHALL use EN paragraphs for the candidate heading to interpret scope, technical characteristics, inclusions, exclusions, and borderline guidance
6. THE Rules_Engine SHALL record structured evaluation result for each candidate including: matched facts, missing facts, conflicting facts, legal note results, EN results, decision (RETAINED, ELIMINATED, UNRESOLVED), reason, and source IDs
7. WHEN KUM HS 1 results in exactly one heading retained, THE Rules_Engine SHALL NOT apply KUM HS 2-4 unless new facts change the analysis
8. THE Rules_Engine SHALL apply KUM HS rules in operational sequence: heading terms → Section Notes → Chapter Notes → notes targeting the heading → EN assistance → candidate result


### Requirement 9: KUM HS Rules Engine - GRI 2(a) Incomplete and Unfinished Goods

**User Story:** As a Classifier, I want the system to apply KUM HS 2(a) (GRI 2(a)) when goods are incomplete or unfinished, so that such goods are classified as if complete when they have the essential character of the complete article.

#### Acceptance Criteria

1. WHEN extracted facts indicate incomplete status, unfinished status, unassembled status, disassembled status, CKD designation, SKD designation, or blank designation, THE Rules_Engine SHALL trigger KUM HS 2(a)
2. THE Rules_Engine SHALL evaluate whether the goods as presented have the essential character of the complete or finished article by examining: missing components, remaining manufacturing processes, whether function is already available, and essential character evidence
3. THE Rules_Engine SHALL ask for clarification on: which components/processes are missing, whether identity of complete article can be recognized, whether primary function is already formed, extent of further work required, whether all components are presented together, and whether disassembly is only for transportation/assembly convenience
4. WHEN goods have essential character of complete article, THE Rules_Engine SHALL output TREAT_AS_COMPLETE and classify accordingly
5. WHEN goods do not have essential character, THE Rules_Engine SHALL output CLASSIFY_AS_PRESENTED
6. WHEN essential character cannot be determined from available facts, THE Rules_Engine SHALL output NEED_CLARIFICATION


### Requirement 10: KUM HS Rules Engine - GRI 2(b) Material Extension

**User Story:** As a Classifier, I want the system to apply KUM HS 2(b) (GRI 2(b)) when material reference in a heading extends to mixtures or combinations, so that composite material goods are classified correctly.

#### Acceptance Criteria

1. WHEN facts indicate mixture of materials, combination of materials, goods made wholly or partly from a material, or material extension reference in heading, THE Rules_Engine SHALL trigger KUM HS 2(b)
2. WHEN material extension creates prima facie classification in two or more headings, THE Rules_Engine SHALL proceed to KUM HS 3
3. THE Rules_Engine SHALL record which material-based headings are retained after KUM HS 2(b)


### Requirement 11: KUM HS Rules Engine - GRI 3(a) Specific vs General

**User Story:** As a Classifier, I want the system to apply KUM HS 3(a) (GRI 3(a)) when goods can prima facie be classified in two or more headings, so that the most specific heading is selected over general headings.

#### Acceptance Criteria

1. WHEN goods can prima facie be classified in two or more headings after KUM HS 1 and 2, THE Rules_Engine SHALL trigger KUM HS 3(a)
2. THE Rules_Engine SHALL compare headings for specificity by testing: specific heading description vs general heading description, complete article description vs residual heading, whether each heading refers only to part of materials/components/items in mixture, composite goods, or retail set
3. WHEN one heading is more specific, THE Rules_Engine SHALL select that heading and NOT proceed to KUM HS 3(b)
4. WHEN two or more headings are equally specific because each refers only to part of materials/components/items, THE Rules_Engine SHALL proceed to KUM HS 3(b)
5. THE Rules_Engine SHALL record specificity comparison evidence and source IDs for audit trail


### Requirement 12: KUM HS Rules Engine - GRI 3(b) Essential Character

**User Story:** As a Classifier, I want the system to apply KUM HS 3(b) (GRI 3(b)) for mixtures, composite goods, and retail sets, so that classification is based on the component conferring essential character.

#### Acceptance Criteria

1. WHEN KUM HS 3(a) fails and goods are mixture, composite goods, goods consisting of different components, or set put up for retail sale, THE Rules_Engine SHALL trigger KUM HS 3(b)
2. FOR retail set classification, THE Rules_Engine SHALL verify: at minimum two different articles prima facie classifiable in different headings, articles are put together to meet a particular need or carry out a specific activity, articles are put up for retail sale without repacking
3. THE Rules_Engine SHALL evaluate essential character by considering: function, role of components, necessity for use of goods, material, bulk, quantity, weight, value, durability, consumer perception, and contribution to identity
4. THE Rules_Engine SHALL NOT automatically select one factor as determinative without evidence
5. THE Rules_Engine SHALL record factors examined, operational weighting, evidence, and reasoning for essential character determination
6. WHEN essential character cannot be determined from available facts, THE Rules_Engine SHALL proceed to KUM HS 3(c)


### Requirement 13: KUM HS Rules Engine - GRI 3(c) Last in Numerical Order

**User Story:** As a Classifier, I want the system to apply KUM HS 3(c) (GRI 3(c)) as a last resort, so that when no other rule applies, classification defaults to the last heading numerically among equally applicable candidates.

#### Acceptance Criteria

1. WHEN KUM HS 3(a) and 3(b) both fail, THE Rules_Engine SHALL trigger KUM HS 3(c)
2. THE Rules_Engine SHALL select the heading that is last in numerical order among candidates that: are equally applicable, have passed KUM HS 1, and have truly equivalent merit
3. THE Rules_Engine SHALL NOT include weak candidates merely to make their code numerically last
4. THE Rules_Engine SHALL record which candidates were considered equivalent and source IDs for audit trail


### Requirement 14: KUM HS Rules Engine - GRI 4 Most Akin

**User Story:** As a Classifier, I want the system to apply KUM HS 4 (GRI 4) when goods cannot be classified by GRI 1-3, so that classification is based on the heading for goods most akin to the subject goods.

#### Acceptance Criteria

1. WHEN goods cannot be classified by KUM HS 1-3, THE Rules_Engine SHALL trigger KUM HS 4
2. THE Rules_Engine SHALL compare candidate headings based on: material, form, function, operating principle, use, technical characteristics, commercial identity, and relationship to comparator goods
3. THE Rules_Engine SHALL display comparator goods and reasoning for similarity
4. WHEN classification risk is high due to KUM HS 4 application, THE Rules_Engine SHALL flag for expert review
5. THE Rules_Engine SHALL record comparison factors, comparator goods, and source IDs for audit trail


### Requirement 15: KUM HS Rules Engine - GRI 5 Cases and Packing

**User Story:** As a Classifier, I want the system to apply KUM HS 5(a) and 5(b) (GRI 5(a) and 5(b)) for containers and packing materials, so that these are classified with the article or separately according to legal criteria.

#### Acceptance Criteria

1. WHEN facts indicate specially shaped case/container for specific goods, THE Rules_Engine SHALL trigger KUM HS 5(a)
2. THE Rules_Engine SHALL evaluate: whether container is specially shaped or fitted for specific article, suitable for long-term use, presented with article, normally sold with article, and does not give the whole its essential character
3. WHEN KUM HS 5(a) criteria are met, THE Rules_Engine SHALL output: classified with article
4. WHEN facts indicate packing material or packing container, THE Rules_Engine SHALL trigger KUM HS 5(b)
5. THE Rules_Engine SHALL evaluate: presented with goods, normally used for packing such goods, and not obviously suitable for repetitive use
6. WHEN KUM HS 5(b) criteria are met, THE Rules_Engine SHALL output: classified with goods


### Requirement 16: KUM HS Rules Engine - GRI 6 Subheading Classification

**User Story:** As a Classifier, I want the system to apply KUM HS 6 (GRI 6) after the 4-digit heading is determined, so that classification descends correctly to 6-digit subheading and 8-digit tariff line.

#### Acceptance Criteria

1. WHEN 4-digit heading is locked after KUM HS 1-5, THE Rules_Engine SHALL trigger KUM HS 6
2. THE Rules_Engine SHALL retrieve child subheadings from the locked heading
3. THE Rules_Engine SHALL compare only subheadings at the same indentation level
4. THE Rules_Engine SHALL apply KUM HS 1-5 mutatis mutandis at subheading level using: subheading description, Subheading Notes, Section Notes and Chapter Notes still relevant, and discriminator thresholds (weight, capacity, power, size, composition, concentration, retail/bulk, CKD/non-CKD, use)
5. THE Rules_Engine SHALL descend 4-digit → 6-digit → 8-digit following BTKI/AHTN structure
6. WHEN discriminator facts for subheading selection are not available, THE Rules_Engine SHALL return NEED_CLARIFICATION and NOT default to generic .00 code
7. THE Rules_Engine SHALL NOT compare headings from different 4-digit groups after heading is locked unless analysis is reset due to changed facts


### Requirement 17: Validation System - Exact Code and Description Validation

**User Story:** As a Classifier, I want every final HS code validated against the active BTKI database, so that no hallucinated or invalid codes are presented as results.

#### Acceptance Criteria

1. WHEN final HS code is determined, THE Validator SHALL verify the code exists in active nomenclature version database
2. THE Validator SHALL verify the code has exactly 8 digits in BTKI format
3. THE Validator SHALL retrieve official description from database and verify exact match with classification result
4. THE Validator SHALL verify parent hierarchy exists: section → chapter → heading → subheading → tariff line
5. THE Validator SHALL verify code is active on classification date
6. THE Validator SHALL verify all source IDs in reasoning chain reference valid database records
7. WHEN validation fails on any criterion, THE Validator SHALL reject the classification result with specific failure reason
8. THE Validator SHALL NOT allow presentation of results with validation failures to end users


### Requirement 18: Tariff Lookup Module - MFN Duty Retrieval

**User Story:** As a Classifier, I want to see the MFN (Most Favored Nation) import duty rate for the classified HS code, so that I know the standard tariff applicable.

#### Acceptance Criteria

1. WHEN HS code is validated, THE Tariff_Lookup_Module SHALL retrieve MFN_BM (Bea Masuk) rate from database for that code
2. THE Tariff_Lookup_Module SHALL verify rate is effective on classification date
3. THE Tariff_Lookup_Module SHALL retrieve rate value, rate unit (PERCENT, SPECIFIC, COMPOUND), currency if specific, quantity unit if specific, and formula if compound
4. THE Tariff_Lookup_Module SHALL include source_id referencing the BTKI record
5. WHEN no MFN rate is found for the code, THE Tariff_Lookup_Module SHALL return null with explanation


### Requirement 19: Tariff Lookup Module - FTA Rate Reference

**User Story:** As a Classifier, I want to see available FTA (Free Trade Agreement) preferential rates for the classified HS code, so that I can evaluate potential duty savings under trade agreements.

#### Acceptance Criteria

1. WHEN HS code is validated, THE Tariff_Lookup_Module SHALL retrieve all FTA_BM rates from database for that code organized by scheme_code and partner_country_code
2. THE Tariff_Lookup_Module SHALL retrieve rate value, rate unit, effective dates, and conditions summary for each FTA rate
3. THE Tariff_Lookup_Module SHALL clearly label rates as "Available FTA Rates" and NOT as "Applicable FTA Rates"
4. THE Tariff_Lookup_Module SHALL display disclaimer: "FTA rate availability does not guarantee eligibility. Actual entitlement depends on origin, Rules of Origin compliance, Certificate of Origin, direct consignment, and procedural requirements"
5. THE Tariff_Lookup_Module SHALL NOT automatically assert FTA rate applicability based solely on partner country selection
6. THE Tariff_Lookup_Module SHALL include source_id for each FTA rate record


### Requirement 20: Tariff Lookup Module - PPN and PPh 22 Import Rates

**User Story:** As a Classifier, I want to see standard PPN (VAT) and PPh 22 (Income Tax Article 22) import rates for the classified HS code, so that I can estimate total import duties and taxes.

#### Acceptance Criteria

1. WHEN HS code is validated, THE Tariff_Lookup_Module SHALL retrieve standard PPN_IMPORT rate from database
2. THE Tariff_Lookup_Module SHALL retrieve standard PPH22_IMPORT rate from database
3. THE Tariff_Lookup_Module SHALL retrieve rate value, rate unit, and effective dates for each tax
4. THE Tariff_Lookup_Module SHALL display disclaimer: "Rates shown are standard rates from BTKI. Special exemptions, facilities, or reductions under specific PMK regulations are not included in this phase"
5. THE Tariff_Lookup_Module SHALL include source_id for each tax rate record
6. WHEN no rate is found, THE Tariff_Lookup_Module SHALL return null with explanation


---

### Requirement 21: Admin Data Center - CSV/Excel Template Upload (Alternative Ingestion Path)

**User Story:** As a Data Administrator, I want to upload BTKI data via a standardized CSV/Excel template as an alternative to DOCX parsing, so that I can perform manual corrections, partial updates, or seed new data without needing the original Word document.

#### Acceptance Criteria

1. THE Admin_Data_Center SHALL provide a downloadable CSV template with the following mandatory columns: `code` (8-digit BTKI format XXXX.XX.XX), `description_id` (Indonesian), `description_en` (English, optional), `parent_code`, `indent_level`, `mfn_rate`, `mfn_rate_unit`, `ppn_rate`, `pph22_rate`, `effective_from`, `effective_to`, `status` (A=active, I=inactive)
2. THE Admin_Data_Center SHALL provide a downloadable Excel template (.xlsx) with the same columns plus column header tooltips explaining each field
3. WHEN a CSV/Excel file is uploaded, THE Admin_Data_Center SHALL validate each row for: code format (XXXX.XX.XX), non-empty description_id, valid parent_code reference, valid date format for effective dates, valid status value (A or I), valid rate unit (PERCENT, SPECIFIC, COMPOUND)
4. THE Admin_Data_Center SHALL generate a row-level validation report showing: row number, column name, error type, and suggested fix for each invalid row
5. THE Admin_Data_Center SHALL reject the entire upload if ERROR-level issues exist; WARNING-level issues shall require admin acknowledgement before proceeding
6. WHEN CSV/Excel upload is valid, THE Admin_Data_Center SHALL assign source_id format `template:upload:{filename}:{row_number}` for every imported row (to distinguish from DOCX-parsed entries)
7. THE Admin_Data_Center SHALL support partial update mode: rows with existing codes update the record; rows with new codes insert as new entries; no rows are silently skipped
8. THE Admin_Data_Center SHALL display a side-by-side preview showing: rows to INSERT (new), rows to UPDATE (changed fields highlighted), rows with no change (skipped)
9. THE Admin_Data_Center SHALL require admin confirmation of the preview before committing the import
10. WHEN template upload is committed, THE Admin_Data_Center SHALL apply the same Version Manager state machine as DOCX import: UPLOADED → VALIDATING → DRY_RUN_PASSED → COMMITTED → ACTIVE
11. THE Admin_Data_Center SHALL NOT allow template upload to bypass the dry run validation step
12. THE Admin_Data_Center SHALL track which rows originated from template upload vs DOCX parser in the `btki_tariff_lines` table via `ingestion_method` column (values: `DOCX_PARSER`, `CSV_TEMPLATE`, `XLSX_TEMPLATE`)
