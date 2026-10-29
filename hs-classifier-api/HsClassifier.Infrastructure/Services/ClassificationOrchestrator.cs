using System.Text.Json;
using HsClassifier.Application.Classification;
using HsClassifier.Application.Classification.Rules;
using HsClassifier.Domain.Classification;
using HsClassifier.Domain.Entities;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainConf = HsClassifier.Domain.Classification.ConfidenceLevel;
using EntityConf = HsClassifier.Domain.Enums.ConfidenceLevel;
using EntityStatus = HsClassifier.Domain.Enums.CaseStatus;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// Fact_Engine → Candidate_Generator → Rules_Engine → Validator → Tariff_Lookup.
/// Every outcome (determined, clarification, expert review) is persisted with its reasoning chain,
/// so the audit trail covers all decision paths (Req 8.8). Final codes only come from the rules
/// engine + validator — never from a default pick (Req 16.7).
/// </summary>
public class ClassificationOrchestrator(
    HsClassifierDbContext db,
    CandidateGeneratorService candidateGenerator,
    ClassificationValidator validator,
    TariffLookupService tariffLookup,
    ITermSuggester termSuggester)
{
    private readonly FactExtractionService _facts = new();
    private readonly QuestionGeneratorService _questions = new();
    private readonly RulesEngine _rules = new();

    /// <param name="existingCase">When set, new facts are merged into the stored facts of this case.</param>
    public async Task<ClassificationSessionResult> ClassifyAsync(
        int userId, Dictionary<string, string?> userInput,
        ClassificationCase? existingCase = null, CancellationToken ct = default)
    {
        // ai_suggestion is server-owned (cached AI output); a client must not be able to forge it
        var input = MergeInput(existingCase, userInput.Where(kv => kv.Key != AiSuggestionKey)
            .ToDictionary(kv => kv.Key, kv => kv.Value));
        var facts = _facts.ExtractFromInput(input);

        // Goods condition first (KUM HS 2a, mixture, retail set, parts): it decides WHAT is classified
        var condition = ConditionGate.Evaluate(facts);
        if (condition.Status != ConditionStatus.Proceed)
        {
            var held = new ClassificationResult
            {
                Status = condition.Status == ConditionStatus.NeedExpertReview
                    ? ResultStatus.NeedExpertReview : ResultStatus.NeedClarification,
            };
            held.ReasoningChain.AddRange(condition.Steps);
            return await PersistAsync(held, input, null, userId, existingCase, ct,
                message: condition.Status == ConditionStatus.NeedExpertReview
                    ? condition.Message + " " + ExpertAdvice : condition.Message,
                questions: condition.Questions.Count > 0 ? condition.Questions : null);
        }
        // Mixture / retail set: search on the component that gives essential character
        if (condition.EssentialTerm is not null) facts.Set("generic_name", condition.EssentialTerm);

        List<CandidateHeading> candidates;
        ClassificationResult? result;
        var routed = false;
        if (condition.PartOfMachine is not null)
        {
            var parts = await PartsCandidatesAsync(condition.PartOfMachine, condition.Accessory,
                facts.GetValue(RulesEngine.PartParentHeadingFact), ct);
            if (parts.Candidates.Count == 0)
            {
                var noParts = new ClassificationResult
                {
                    Status = parts.Question is null ? ResultStatus.NeedExpertReview : ResultStatus.NeedClarification,
                };
                noParts.ReasoningChain.AddRange(condition.Steps);
                noParts.ReasoningChain.Add(parts.Step);
                if (parts.Question is not null)
                    return await PersistAsync(noParts, input, null, userId, existingCase, ct, questions: [parts.Question]);
                return await PersistAsync(noParts, input, null, userId, existingCase, ct,
                    message: (condition.Accessory ? "Pos bagian dan aksesori" : "Pos bagian") +
                             " untuk mesin tersebut tidak ditemukan di BTKI. " + ExpertAdvice);
            }
            condition.Steps.Add(parts.Step);
            candidates = parts.Candidates;
            facts.Set(RulesEngine.PartParentHeadingFact, parts.ParentHeading);
            routed = true; // the legal note chose the heading; lexical fallback/AI must not override it
        }
        else if (condition.ScrapMaterial is not null)
        {
            // Only "sisa dan skrap" headings naming the material; never a generic text match on "skrap"
            candidates = await candidateGenerator.ScrapHeadingsAsync(condition.ScrapMaterial, ct);
            var scrapStep = new ReasoningStep
            {
                Rule = "WASTE_SCRAP", CandidateCode = candidates.FirstOrDefault()?.Code ?? "",
                Decision = candidates.Count > 0 ? CandidateDecision.Retained : CandidateDecision.Unresolved,
                Reason = candidates.Count > 0
                    ? $"Pos sisa dan skrap yang menyebut bahan \"{condition.ScrapMaterial}\": {string.Join(", ", candidates.Select(c => c.Code))}."
                    : $"Tidak ada pos sisa dan skrap di BTKI aktif yang menyebut bahan \"{condition.ScrapMaterial}\".",
                SourceIds = candidates.Select(c => c.SourceId ?? c.Code).ToList(),
            };
            condition.Steps.Add(scrapStep);
            if (candidates.Count == 0)
            {
                var noScrap = new ClassificationResult { Status = ResultStatus.NeedExpertReview };
                noScrap.ReasoningChain.AddRange(condition.Steps);
                return await PersistAsync(noScrap, input, null, userId, existingCase, ct,
                    message: "Pos sisa dan skrap untuk bahan tersebut tidak ditemukan di BTKI. " + ExpertAdvice);
            }
            routed = true;
        }
        else candidates = await candidateGenerator.GenerateCandidatesAsync(facts, ct);
        result = candidates.Count > 0 ? _rules.ApplyKumHs(facts, candidates) : null;

        // AI fallback only when the DB search failed: no candidate, or KUM HS 1 matched nothing
        string? notice = null;
        if (!routed && (result is null || result.ReasoningChain.Any(s => s.Rule == RulesEngine.FallbackRule)))
        {
            var ai = await TryAiSuggestionAsync(input, facts, ct);
            if (ai.Unavailable) notice = AiUnavailableNotice;

            if (ai.Candidates.Count > 0)
            {
                candidates = ai.Candidates;
                result = _rules.ApplyKumHs(facts, candidates);
                result.ReasoningChain.Insert(0, ai.Step!);
                // AI only proposed the wording: without user confirmation of the subheading, cap confidence
                if (result.Confidence == DomainConf.High && facts.GetValue(FactExtractionService.SubheadingChoice) is null)
                    result.Confidence = DomainConf.Medium;
            }
            else
            {
                // Fallback 3c: ask the generic name once, then expert review
                var dead = new ClassificationResult();
                dead.ReasoningChain.AddRange(condition.Steps);
                if (ai.Step is not null) dead.ReasoningChain.Add(ai.Step);
                dead.ReasoningChain.Add(new ReasoningStep
                {
                    Rule = "CANDIDATE_GENERATION", Decision = CandidateDecision.Unresolved,
                    Reason = "Tidak ditemukan heading di database yang sesuai dengan fakta barang.",
                });
                var (status, message, question) = DecideNoCandidate(facts.IsKnown("generic_name"));
                dead.Status = status;
                return await PersistAsync(dead, input, null, userId, existingCase, ct,
                    message: notice + message, questions: question is null ? null : [question]);
            }
        }

        // Condition decisions come first in the audit trail: they framed everything after them
        result!.ReasoningChain.InsertRange(0, condition.Steps);

        if (result.Status == ResultStatus.NeedClarification)
        {
            // Ask only about the locked heading when one exists (KUM HS 6 context)
            var lockedCode = result.ReasoningChain.LastOrDefault(s => s.Rule == "KUM_HS_6")?.CandidateCode;
            var scope = candidates.Where(c => c.Code == lockedCode).ToList() is { Count: > 0 } l ? l : candidates;
            // KUM HS 6 questions come from the official texts being compared; generic ones are a fallback
            var questions = result.PendingQuestions.Count > 0
                ? result.PendingQuestions
                : _questions.GenerateQuestions(facts, scope);

            if (questions.Count > 0)
                return await PersistAsync(result, input, null, userId, existingCase, ct, message: notice,
                    questions: questions, candidateCodes: scope.Select(c => c.Code).ToList());

            // Engine needs facts we have no question for → a human must decide; never pick a default
            result.Status = ResultStatus.NeedExpertReview;
            result.ReasoningChain.Add(new ReasoningStep
            {
                Rule = "QUESTION_GENERATION", CandidateCode = lockedCode ?? "",
                Decision = CandidateDecision.Unresolved,
                Reason = $"Fakta pembeda {string.Join(", ", result.MissingFacts)} dibutuhkan tetapi tidak ada pertanyaan yang tersedia. Perlu review ahli.",
            });
        }

        if (result.FinalCode is null)
            return await PersistAsync(result, input, null, userId, existingCase, ct,
                message: notice + "Kode HS belum dapat ditentukan secara otomatis. " + ExpertAdvice,
                candidateCodes: result.UnresolvedCodes.Count > 0 ? result.UnresolvedCodes : candidates.Select(c => c.Code).ToList());

        var (isValid, failure, officialDesc) = await validator.ValidateAsync(result.FinalCode, ct);
        if (!isValid)
        {
            // Req 17.8: never present an invalid code; record why
            result.ReasoningChain.Add(new ReasoningStep
            {
                Rule = "VALIDATION", CandidateCode = result.FinalCode, Decision = CandidateDecision.Eliminated,
                Reason = failure ?? "Validation failed.",
            });
            result.FinalCode = null;
            result.Status = ResultStatus.NeedExpertReview;
            await PersistAsync(result, input, null, userId, existingCase, ct);
            return new ClassificationSessionResult { Status = "ERROR", Message = $"Validation: {failure}" };
        }

        result.FinalDescription = officialDesc;
        var tariffs = await tariffLookup.GetRatesAsync(result.FinalCode, ct);
        return await PersistAsync(result, input, tariffs, userId, existingCase, ct, report: Score(result, candidates));
    }

    /// <summary>Feeds the document's confidence model from what the engine actually did for this result.</summary>
    public static ConfidenceReport Score(ClassificationResult r, List<CandidateHeading> candidates)
    {
        var locked = candidates.FirstOrDefault(c => c.Code == r.LockedHeading);
        var chain = r.ReasoningChain;
        var others = Math.Max(0, candidates.Count - 1);
        return ConfidenceCalculator.Compute(new ConfidenceInput(
            DecisiveFactsComplete: r.MissingFacts.Count == 0,
            HeadingMatch: r.LockedHeadingScore,
            // Notes/EN count as "tested" only when the locked heading actually had them to test against
            LegalNotesTested: locked is not null && locked.ChapterNotesText.Count + locked.SectionNotesText.Count > 0,
            EnReviewed: locked is not null && locked.EnParagraphs.Count > 0,
            SubheadingCertainty: r.Confidence switch { DomainConf.High => 1.0, DomainConf.Medium => 0.7, _ => 0.4 },
            EliminationStrength: others == 0 ? 1.0 : Math.Min(1.0, (double)r.EliminatedCandidates.Count / others),
            CandidateAmbiguityUnresolved: false,
            Gri4Used: chain.Any(s => s.Rule == "KUM_HS_4"),
            Gri3cUsed: chain.Any(s => s.Rule == "KUM_HS_3C"),
            AiTermSuggestion: chain.Any(s => s.Rule == "AI_SUGGESTION")));
    }

    // ─── Parts (Section XVI Note 2b) ──────────────────────────────────────────

    /// <summary>
    /// Classify the parent machine first (same DB search + KUM HS 1–3), then take the BTKI parts
    /// heading whose text covers that machine's heading.
    /// </summary>
    private async Task<(List<CandidateHeading> Candidates, ReasoningStep Step, string? ParentHeading, ClassificationQuestion? Question)>
        PartsCandidatesAsync(string parentMachine, bool accessory, string? chosenParent, CancellationToken ct)
    {
        var rule = accessory ? "ACCESSORY" : "SECTION_XVI_NOTE_2";
        var kind = accessory ? "pos bagian dan aksesori" : "pos bagian";
        var parentFacts = _facts.ExtractFromInput(new() { ["generic_name"] = parentMachine });
        var parentCandidates = await candidateGenerator.GenerateCandidatesAsync(parentFacts, ct);
        var parentResult = parentCandidates.Count > 0 ? _rules.ApplyKumHs(parentFacts, parentCandidates) : null;
        var parentHeading = parentResult?.LockedHeading;

        if (parentHeading is null || parentResult!.ReasoningChain.Any(s => s.Rule == RulesEngine.FallbackRule))
            return ([], new ReasoningStep
            {
                Rule = rule, Decision = CandidateDecision.Unresolved,
                Reason = $"Pos mesin induk \"{parentMachine}\" tidak dapat ditentukan dari database.",
            }, null, null);

        // KUM HS 3(c) is a last resort for the goods themselves, not a way to guess the parent machine:
        // on a tie, the user picks the parent heading from the official texts
        var tied = parentResult.ReasoningChain
            .Where(s => s.Rule == "KUM_HS_1" && s.Decision == CandidateDecision.Retained)
            .Select(s => s.CandidateCode).Distinct().ToList();
        var chosen = chosenParent?.Trim();
        if (chosen is not null && tied.Contains(chosen)) parentHeading = chosen;
        else if (parentResult.ReasoningChain.Any(s => s.Rule == "KUM_HS_3C") && tied.Count > 1)
            return ([], new ReasoningStep
            {
                Rule = rule, Decision = CandidateDecision.Unresolved,
                Reason = $"Mesin induk \"{parentMachine}\" cocok dengan beberapa pos ({string.Join(", ", tied)}); meminta pengguna memilih.",
            }, null, new ClassificationQuestion
            {
                FactType = RulesEngine.PartParentHeadingFact, InformationGain = 1.0,
                QuestionText = $"\"{parentMachine}\" cocok dengan beberapa pos. Pos mana yang sesuai dengan mesin/barang induknya?",
                Choices = parentCandidates.Where(c => tied.Contains(c.Code))
                    .Select(c => new QuestionChoice(c.Code, $"{c.Code} — {c.Description}")).ToList(),
            });

        var parts = await candidateGenerator.PartsHeadingsForAsync(parentHeading, accessory, ct);
        return (parts, new ReasoningStep
        {
            Rule = rule, CandidateCode = parts.FirstOrDefault()?.Code ?? parentHeading,
            Decision = parts.Count > 0 ? CandidateDecision.Retained : CandidateDecision.Unresolved,
            Reason = parts.Count > 0
                ? $"Mesin induk \"{parentMachine}\" masuk pos {parentHeading}. {char.ToUpper(kind[0])}{kind[1..]} yang mencakupnya: " +
                  $"{string.Join(", ", parts.Select(p => p.Code))}."
                : $"Mesin induk \"{parentMachine}\" masuk pos {parentHeading}, tetapi tidak ada {kind} yang merujuk pos tersebut.",
            SourceIds = parts.Select(p => p.SourceId ?? p.Code).ToList(),
        }, parentHeading, null);
    }

    // ─── AI term fallback ─────────────────────────────────────────────────────

    public const string AiSuggestionKey = "ai_suggestion";
    public const string AiUnavailableNotice = "Asisten AI sedang tidak tersedia. ";
    public const string ExpertAdvice = "Silakan konsultasikan dengan ahli kepabeanan bila ragu.";

    /// <summary>Fallback 3c once the DB and AI found no heading: ask the generic name once, then expert review.</summary>
    public static (ResultStatus Status, string Message, ClassificationQuestion? Question) DecideNoCandidate(bool hasGenericName) =>
        hasGenericName
            ? (ResultStatus.NeedExpertReview,
               "Barang belum dapat dipetakan ke pos BTKI dan perlu review ahli. " + ExpertAdvice, null)
            : (ResultStatus.NeedClarification,
               "Barang belum ditemukan di BTKI dengan nama yang diberikan. " + ExpertAdvice,
               new ClassificationQuestion
               {
                   FactType = "generic_name",
                   QuestionText = "Barang ini secara umum disebut apa? (contoh: telepon seluler, mesin cuci, ban mobil)",
                   InformationGain = 1.0,
               });

    private async Task<(List<CandidateHeading> Candidates, ReasoningStep? Step, bool Unavailable)> TryAiSuggestionAsync(
        Dictionary<string, string?> input, FactSet facts, CancellationToken ct)
    {
        // Reuse the suggestion cached in the case so follow-up rounds don't call Groq again
        var suggestion = GroqTermSuggester.Parse(input.GetValueOrDefault(AiSuggestionKey));
        var unavailable = false;
        if (suggestion is null)
        {
            var res = await termSuggester.SuggestAsync(facts, ct);
            suggestion = res.Suggestion;
            unavailable = res.Unavailable;
        }
        if (suggestion is null) return ([], null, unavailable);

        var (candidates, accepted, rejected) = await candidateGenerator.FromSuggestionAsync(suggestion, ct);
        // Cache only a suggestion that led somewhere; a dead one would block a retry with a new generic name
        if (candidates.Count > 0) input[AiSuggestionKey] = JsonSerializer.Serialize(new { terms = suggestion.Terms, headings = suggestion.Headings });
        else input.Remove(AiSuggestionKey);

        var step = new ReasoningStep
        {
            Rule = "AI_SUGGESTION",
            Decision = candidates.Count > 0 ? CandidateDecision.Retained : CandidateDecision.Unresolved,
            Reason = $"Pencarian database tidak menemukan heading. Istilah usulan AI: {string.Join(", ", suggestion.Terms)}; " +
                     $"heading tervalidasi database: {(accepted.Count > 0 ? string.Join(", ", accepted) : "-")}" +
                     (rejected.Count > 0 ? $"; ditolak (tidak ada di BTKI aktif): {string.Join(", ", rejected)}" : "") +
                     ". Kode final tetap ditentukan KUM HS dari database.",
            SourceIds = [$"ai:groq:{termSuggester.Model}"],
        };
        return (candidates, step, false);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static Dictionary<string, string?> MergeInput(ClassificationCase? existing, Dictionary<string, string?> input)
    {
        var merged = existing is null
            ? new Dictionary<string, string?>()
            : JsonSerializer.Deserialize<Dictionary<string, string?>>(existing.FactsJson) ?? new();
        foreach (var (k, v) in input)
            if (!string.IsNullOrWhiteSpace(v)) merged[k] = v;
        return merged;
    }

    private async Task<ClassificationSessionResult> PersistAsync(
        ClassificationResult result, Dictionary<string, string?> input, TariffResult? tariffs,
        int userId, ClassificationCase? existing, CancellationToken ct,
        string? message = null, List<ClassificationQuestion>? questions = null, List<string>? candidateCodes = null,
        ConfidenceReport? report = null)
    {
        var status = result.Status switch
        {
            ResultStatus.Determined       => EntityStatus.Determined,
            ResultStatus.NeedExpertReview => EntityStatus.NeedExpertReview,
            _                             => EntityStatus.NeedClarification
        };
        // The weighted score decides the label; the engine's own level is only one of its inputs
        if (report is not null) result.Confidence = ConfidenceCalculator.Level(report.Score);
        EntityConf? confidence = result.FinalCode is null ? null : result.Confidence switch
        {
            DomainConf.High   => EntityConf.High,
            DomainConf.Medium => EntityConf.Medium,
            _                 => EntityConf.Low
        };

        var caseEntity = existing;
        if (caseEntity is null)
        {
            var version = await db.NomenclatureVersions.Where(v => v.IsActive).FirstOrDefaultAsync(ct);
            caseEntity = new ClassificationCase { UserId = userId, VersionId = version?.Id ?? Guid.Empty };
            db.ClassificationCases.Add(caseEntity);
        }
        // Raw values (not normalized) so a later merge re-extracts identically
        caseEntity.FactsJson = JsonSerializer.Serialize(input.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value));
        caseEntity.FinalCode = result.FinalCode;
        // Persist the open candidates so an expert reviewing the case later sees the same options
        caseEntity.CandidateCodesJson = candidateCodes is { Count: > 0 } ? JsonSerializer.Serialize(candidateCodes) : null;
        caseEntity.FinalDescription = result.FinalDescription;
        caseEntity.Confidence = confidence;
        caseEntity.ConfidenceScore = report?.Score;
        caseEntity.ConfidenceJson = report is null ? null : JsonSerializer.Serialize(ToDto(report));
        caseEntity.Status = status;
        caseEntity.ClassificationDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Reasoning events are append-only: a follow-up round continues numbering after prior events
        var offset = existing is null ? 0
            : await db.ReasoningEvents.Where(e => e.CaseId == caseEntity.Id).MaxAsync(e => (int?)e.EventOrder, ct) ?? 0;
        var factsUsed = caseEntity.FactsJson;
        // A follow-up round re-runs the whole chain; store only steps not recorded in earlier rounds
        var toStore = existing is null ? result.ReasoningChain
            : NewSteps((await db.ReasoningEvents.Where(e => e.CaseId == caseEntity.Id)
                    .Select(e => new { e.RuleApplied, e.Reasoning }).ToListAsync(ct))
                    .Select(e => (e.RuleApplied, e.Reasoning)), result.ReasoningChain);
        for (int i = 0; i < toStore.Count; i++)
        {
            var step = toStore[i];
            db.ReasoningEvents.Add(new ReasoningEvent
            {
                CaseId        = caseEntity.Id,
                EventOrder    = offset + i + 1,
                RuleApplied   = step.Rule,
                Decision      = step.Decision.ToString(),
                Reasoning     = Format(step),
                FactsUsedJson = i == 0 ? factsUsed : null,
                SourceIdsJson = step.SourceIds.Count > 0 ? JsonSerializer.Serialize(step.SourceIds) : null,
            });
        }
        await db.SaveChangesAsync(ct);

        return new ClassificationSessionResult
        {
            CaseId           = caseEntity.Id,
            Status           = status switch
            {
                EntityStatus.Determined       => "DETERMINED",
                EntityStatus.NeedExpertReview => "NEED_EXPERT_REVIEW",
                _                             => "NEED_CLARIFICATION"
            },
            Message          = message,
            FinalCode        = result.FinalCode,
            FinalDescription = result.FinalDescription,
            Confidence       = result.FinalCode is null ? null : result.Confidence.ToString(),
            ReasoningSteps   = result.ReasoningChain.Count,
            EliminatedCount  = result.EliminatedCandidates.Count,
            Tariffs          = tariffs,
            Questions        = questions,
            CandidateCodes   = candidateCodes,
            ConfidenceScore  = report?.Score,
            ConfidenceDetail = report is null ? null : ToDto(report),
        };
    }

    private static string Format(ReasoningStep s) =>
        string.IsNullOrEmpty(s.CandidateCode) ? s.Reason : $"[{s.CandidateCode}] {s.Reason}";

    /// <summary>Steps whose (rule, formatted reasoning) is not already stored for the case.</summary>
    public static List<ReasoningStep> NewSteps(IEnumerable<(string Rule, string Reasoning)> existing, List<ReasoningStep> steps)
    {
        var seen = existing.ToHashSet();
        return steps.Where(s => seen.Add((s.Rule, Format(s)))).ToList();
    }

    public static ConfidenceDetailDto ToDto(ConfidenceReport r) => new(
        r.Components.Select(c => new ConfidenceComponentDto(c.Name, c.Weight, Math.Round(c.Points, 1))).ToList(),
        r.CapsApplied.ToList(), r.RiskFlags.ToList());
}

public record ConfidenceComponentDto(string Name, double Weight, double Points);
public record ConfidenceDetailDto(List<ConfidenceComponentDto> Components, List<string> Caps, List<string> RiskFlags);

public class ClassificationSessionResult
{
    public long?   CaseId { get; set; }
    public string  Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? FinalCode { get; set; }
    public string? FinalDescription { get; set; }
    public string? Confidence { get; set; }
    public int     ReasoningSteps { get; set; }
    public int     EliminatedCount { get; set; }
    public TariffResult? Tariffs { get; set; }
    public List<ClassificationQuestion>? Questions { get; set; }
    public List<string>? CandidateCodes { get; set; }
    public int? ConfidenceScore { get; set; }
    public ConfidenceDetailDto? ConfidenceDetail { get; set; }
}
