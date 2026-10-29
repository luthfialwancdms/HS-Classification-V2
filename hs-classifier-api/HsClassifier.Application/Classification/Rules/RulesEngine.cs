using HsClassifier.Domain.Classification;

namespace HsClassifier.Application.Classification.Rules;

/// <summary>
/// Tasks 8-12: KUM HS Rules Engine — applies GRI 1-6 sequentially.
/// INVARIANT: Final code always from DB (enforced by Validator downstream).
/// ponytail: sequential decision tree, short-circuits at earliest conclusive rule.
/// </summary>
public class RulesEngine
{
    public const string FallbackRule = "KUM_HS_1_NO_MATCH";

    // ─── Main Entry Point ──────────────────────────────────────────────────────

    public ClassificationResult ApplyKumHs(FactSet facts, List<CandidateHeading> candidates)
    {
        var result = new ClassificationResult();

        // Task 8.1: KUM HS 1 — apply to ALL candidates
        var (retained, eliminated) = ApplyKumHs1(facts, candidates, result.ReasoningChain);
        result.EliminatedCandidates.AddRange(eliminated);
        if (result.ReasoningChain.Any(s => s.Rule == FallbackRule))
            result.Status = ResultStatus.NeedExpertReview;

        if (retained.Count == 0)
        {
            result.Status = ResultStatus.NeedExpertReview;
            result.MissingFacts.Add("All candidates eliminated — expert review needed.");
            return result;
        }

        // Task 8.2: Early stopping — exactly 1 retained → go straight to KUM HS 6
        if (retained.Count == 1)
        {
            result.ReasoningChain.Add(new ReasoningStep
            {
                Rule = "KUM_HS_1_EARLY_STOP",
                CandidateCode = retained[0].Code,
                Decision = CandidateDecision.Retained,
                Reason = "Exactly one heading retained after KUM HS 1 — proceeding to KUM HS 6 directly.",
            });
            return ApplyKumHs6(facts, retained[0], result);
        }

        // Tasks 9.1-9.2: KUM HS 2
        retained = ApplyKumHs2(facts, retained, result.ReasoningChain);
        if (retained.Count == 1) return ApplyKumHs6(facts, retained[0], result);
        if (retained.Count == 0) { result.Status = ResultStatus.NeedExpertReview; return result; }

        // Tasks 10.1-10.3: KUM HS 3
        var selected = ApplyKumHs3(facts, retained, result.ReasoningChain);
        if (selected is not null) return ApplyKumHs6(facts, selected, result);

        // Task 11.1: KUM HS 4 — most akin (rare, flag for expert)
        var akin = retained.FirstOrDefault();
        if (akin is not null)
        {
            result.ReasoningChain.Add(new ReasoningStep
            {
                Rule = "KUM_HS_4", CandidateCode = akin.Code,
                Decision = CandidateDecision.Retained,
                Reason = "KUM HS 4 applied — most akin heading selected. Expert review recommended.",
            });
            result.Status = ResultStatus.NeedExpertReview;
            return ApplyKumHs6(facts, akin, result);
        }

        result.Status = ResultStatus.NeedExpertReview;
        return result;
    }

    // ─── KUM HS 1 ─────────────────────────────────────────────────────────────

    private (List<CandidateHeading> Retained, List<CandidateHeading> Eliminated) ApplyKumHs1(
        FactSet facts, List<CandidateHeading> candidates, List<ReasoningStep> chain)
    {
        var retained = new List<CandidateHeading>();
        var eliminated = new List<CandidateHeading>();
        // Same name evidence the candidate generator searched on (generic + commercial)
        var names = new[] { facts.GetValue("generic_name"), facts.GetValue("commercial_name") }
            .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToList();

        foreach (var c in candidates)
        {

            // Check EN EXCLUSION paragraphs (either name excluded counts)
            bool Mentions(string text) => names.Any(n => ContainsKeyword(text, n));
            var excluded = c.EnParagraphs.Any(p => p.StartsWith("[Exclusion]", StringComparison.OrdinalIgnoreCase)
                && Mentions(p));

            // Check Chapter Notes EXCLUSION
            var noteExcluded = c.ChapterNotesText.Any(n => n.StartsWith("[Exclusion]", StringComparison.OrdinalIgnoreCase)
                && Mentions(n));

            if (excluded || noteExcluded)
            {
                eliminated.Add(c);
                chain.Add(new ReasoningStep
                {
                    Rule = "KUM_HS_1", CandidateCode = c.Code,
                    Decision = CandidateDecision.Eliminated,
                    Reason = $"Excluded by {'E' + (excluded ? "N" : "chapter note")}",
                    SourceIds = [c.SourceId ?? c.Code]
                });
                continue;
            }

            // GRI 1: retain only if the heading's own terms cover the facts
            var score = HeadingScore(facts, c);
            if (score > 0 || c.EnParagraphs.Any(p => p.StartsWith("[Inclusion]", StringComparison.OrdinalIgnoreCase)))
            {
                retained.Add(c);
                chain.Add(new ReasoningStep
                {
                    Rule = "KUM_HS_1", CandidateCode = c.Code,
                    Decision = CandidateDecision.Retained,
                    Reason = $"Heading matches facts (score {score:F2}). No exclusions apply.",
                    SourceIds = [c.SourceId ?? c.Code]
                });
            }
            else
            {
                eliminated.Add(c);
                chain.Add(new ReasoningStep
                {
                    Rule = "KUM_HS_1", CandidateCode = c.Code,
                    Decision = CandidateDecision.Eliminated,
                    Reason = "Heading description does not match facts.",
                    SourceIds = [c.SourceId ?? c.Code]
                });
            }
        }

        // Nothing matched lexically: keep the top search candidate, but record it and force expert review
        if (retained.Count == 0 && candidates.Count > 0)
        {
            retained.Add(candidates[0]);
            eliminated.Remove(candidates[0]);
            chain.Add(new ReasoningStep
            {
                Rule = FallbackRule, CandidateCode = candidates[0].Code,
                Decision = CandidateDecision.Unresolved,
                Reason = "Tidak ada heading yang cocok dengan fakta; kandidat pencarian teratas dipertahankan sementara. Perlu review ahli.",
                SourceIds = [candidates[0].SourceId ?? candidates[0].Code]
            });
        }

        return (retained, eliminated);
    }

    // ─── KUM HS 2 ─────────────────────────────────────────────────────────────

    private List<CandidateHeading> ApplyKumHs2(
        FactSet facts, List<CandidateHeading> candidates, List<ReasoningStep> chain)
    {
        // KUM HS 2(a) (incomplete/CKD/SKD) is tested with evidence in ConditionGate before candidate search;
        // it is not re-asserted here.

        // 2(b) — material extension
        var mixture = facts.GetValue("mixture_composite") ?? "";
        if (mixture.Contains("mixture", StringComparison.OrdinalIgnoreCase)
            || mixture.Contains("campuran", StringComparison.OrdinalIgnoreCase))
        {
            chain.Add(new ReasoningStep
            {
                Rule = "KUM_HS_2B",
                Decision = CandidateDecision.Unresolved,
                Reason = "Material mixture detected — multiple headings possible. Proceeding to KUM HS 3.",
            });
        }

        return candidates; // KUM HS 2 doesn't eliminate, passes to KUM HS 3 if >1 retained
    }

    // ─── KUM HS 3 ─────────────────────────────────────────────────────────────

    private CandidateHeading? ApplyKumHs3(
        FactSet facts, List<CandidateHeading> candidates, List<ReasoningStep> chain)
    {
        // Task 10.1: 3(a) — the heading whose description covers the most facts is the most specific;
        // structural specificity (residual "other", parts) only breaks ties. Without this, 3(c) ended up
        // choosing between weak matches purely by number (Req 13.3: no padding with weak candidates).
        var scores = candidates.Select(c => (c, Score: Math.Round(HeadingScore(facts, c), 3) * 10 + 2.0 * Focus(facts, c) + SpecificityScore(c))).ToList();
        var maxScore = scores.Max(x => x.Score);
        var mostSpecific = scores.Where(x => x.Score == maxScore).ToList();

        if (mostSpecific.Count == 1)
        {
            var winner = mostSpecific[0].c;
            chain.Add(new ReasoningStep
            {
                Rule = "KUM_HS_3A", CandidateCode = winner.Code,
                Decision = CandidateDecision.Retained,
                Reason = $"Most specific heading (score {maxScore:F1}). Others are less specific or residual.",
                SourceIds = [winner.SourceId ?? winner.Code]
            });
            return winner;
        }

        // Task 10.2: 3(b) — essential character (simplified weight-based)
        var partFact = facts.GetValue("part_accessory") ?? "";
        var materialFact = facts.GetValue("material") ?? "";

        foreach (var (c, _) in mostSpecific)
        {
            var desc = c.Description.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(materialFact) && desc.Contains(materialFact.ToLowerInvariant()))
            {
                chain.Add(new ReasoningStep
                {
                    Rule = "KUM_HS_3B", CandidateCode = c.Code,
                    Decision = CandidateDecision.Retained,
                    Reason = $"Essential character: material '{materialFact}' matches heading description.",
                    SourceIds = [c.SourceId ?? c.Code]
                });
                return c;
            }
        }

        // Task 10.3: 3(c) — last in numerical order
        var last = mostSpecific.OrderByDescending(x => x.c.Code).First().c;
        chain.Add(new ReasoningStep
        {
            Rule = "KUM_HS_3C", CandidateCode = last.Code,
            Decision = CandidateDecision.Retained,
            Reason = "KUM HS 3(c): heading last in numerical order among equally applicable headings.",
            SourceIds = [last.SourceId ?? last.Code]
        });
        return last;
    }

    // ─── KUM HS 6 ─────────────────────────────────────────────────────────────

    private ClassificationResult ApplyKumHs6(FactSet facts, CandidateHeading heading, ClassificationResult result)
    {
        // Task 12.1: Descend from 4-digit to 8-digit. Final code MUST be an existing tariff line.
        var children = heading.ChildSubheadings;
        var headingSource = heading.SourceId ?? heading.Code;
        result.LockedHeading = heading.Code;
        result.LockedHeadingScore = Math.Min(1, HeadingScore(facts, heading));

        if (children.Count == 0)
        {
            // No 8-digit tariff line in the active version — never fabricate "XXXX.00.00" (Req 16.7, 17.1)
            result.Status = ResultStatus.NeedExpertReview;
            result.ReasoningChain.Add(new ReasoningStep
            {
                Rule = "KUM_HS_6", CandidateCode = heading.Code,
                Decision = CandidateDecision.Unresolved,
                Reason = $"Heading {heading.Code} terkunci, tetapi tidak ada pos tarif 8 digit aktif di database. Perlu review ahli.",
                SourceIds = [headingSource]
            });
            return result;
        }

        if (children.Count == 1)
        {
            // Only one tariff line exists under the heading — descent is deterministic
            return Determine(result, children[0], ConfidenceLevel.High,
                "Hanya ada satu pos tarif 8 digit di bawah heading ini.", headingSource);
        }

        // User's answer to an earlier KUM HS 6 question: a code prefix whose official text they confirmed
        var choice = facts.GetValue(FactExtractionService.SubheadingChoice);
        if (choice == NoneChoice)
            return ExpertReview(result, heading, children,
                $"Pengguna menyatakan tidak ada uraian subpos di bawah {heading.Code} yang sesuai. Perlu review ahli.");

        var pool = choice is not null && choice.StartsWith(heading.Code + ".")
            ? children.Where(c => c.StartsWith(choice)).ToList() : [];
        var confirmed = pool.Count > 0;
        if (!confirmed) pool = children; // no answer yet, or answer belongs to another heading

        // Parts routed via Section XVI Note 2: a branch whose official text cites the parent machine's
        // heading ("Dari mesin pada pos 84.26, 84.29 atau 84.30") is the only legally possible branch.
        if (facts.GetValue(PartParentHeadingFact) is { } parentHeading)
            pool = NarrowByParentHeading(heading, pool, parentHeading, result.ReasoningChain, headingSource);

        // Step 9 of KUM HS 6: numeric bounds in the official texts ("tidak melebihi 37,5 kW") vs. the goods' values
        var before = pool.Count;
        var numeric = Thresholds.Narrow(facts, heading, pool, out var numericQuestion, code => Label(heading, code));
        result.ReasoningChain.AddRange(numeric.Steps);
        pool = numeric.Pool;
        var byNumber = pool.Count < before;

        if (pool.Count == 1)
            return Determine(result, pool[0], ConfidenceLevel.High,
                byNumber && !confirmed
                    ? $"Satu-satunya pos tarif yang memenuhi batas angka barang: \"{Label(heading, pool[0])}\"."
                    : $"Dikonfirmasi pengguna berdasarkan uraian resmi: \"{Label(heading, pool[0])}\".", headingSource);

        // A level split by a number the user has not given: ask for the value, not for a text choice
        if (numericQuestion is not null)
        {
            result.Status = ResultStatus.NeedClarification;
            result.PendingQuestions.Add(numericQuestion);
            result.MissingFacts.Add(numericQuestion.FactType);
            result.ReasoningChain.Add(new ReasoningStep
            {
                Rule = "KUM_HS_6", CandidateCode = heading.Code, Decision = CandidateDecision.Unresolved,
                Reason = $"Subpos di bawah {heading.Code} dibedakan oleh batas angka; meminta nilai {numericQuestion.FactType} dari pengguna.",
                SourceIds = [headingSource]
            });
            return result;
        }

        // Facts alone may already single out one line (Req 16.6) — only a UNIQUE positive match counts
        var (best, _) = SelectBestSubheading(facts, heading, pool);
        if (best is not null)
            return Determine(result, best, confirmed ? ConfidenceLevel.High : ConfidenceLevel.Medium,
                $"Uraian pos tarif paling sesuai dengan fakta barang: \"{heading.ChildDescriptions.GetValueOrDefault(best)}\".",
                headingSource);

        // Otherwise ask, using the official texts of the subheadings being compared — never default (Req 16.7)
        var question = BuildSubheadingQuestion(facts, heading, pool);
        result.Status = ResultStatus.NeedClarification;
        result.PendingQuestions.Add(question);
        result.MissingFacts.Add(FactExtractionService.SubheadingChoice);
        result.ReasoningChain.Add(new ReasoningStep
        {
            Rule = "KUM_HS_6", CandidateCode = heading.Code,
            Decision = CandidateDecision.Unresolved,
            Reason = $"Fakta belum cukup untuk membedakan subpos setingkat di bawah {heading.Code}: " +
                     $"{string.Join(", ", question.Choices!.Where(c => c.Value != NoneChoice).Select(c => c.Value))}. " +
                     "Meminta konfirmasi pengguna berdasarkan uraian resmi BTKI.",
            SourceIds = [headingSource]
        });
        return result;
    }

    public const string NoneChoice = "NONE";
    /// <summary>Set by the orchestrator for a dedicated part: heading of the parent machine (e.g. "8429").</summary>
    public const string PartParentHeadingFact = "part_parent_heading";

    /// <summary>
    /// Level by level: keep the single branch whose own text cites the parent heading; otherwise drop the
    /// branches restricted to OTHER machines ("Untuk mesin dari pos 84.26" when the parent is 84.29).
    /// Stops as soon as a level leaves more than one legally possible branch.
    /// </summary>
    public static List<string> NarrowByParentHeading(CandidateHeading h, List<string> pool, string parentHeading,
        List<ReasoningStep> chain, string sourceId)
    {
        var pos = $"{parentHeading[..2]}.{parentHeading[2..]}";
        while (pool.Count > 1)
        {
            var level = new[] { 6, 7, 10 }.First(l => pool.Select(c => c[..l]).Distinct().Count() > 1);
            string Text(string p) => level < 10 ? h.ParentDescriptions.GetValueOrDefault(p) ?? "" : Label(h, p);
            string Shown(string p) => level < 10 ? Clean(h.ParentDescriptions.GetValueOrDefault(p)) : Label(h, p);

            var branches = pool.Select(c => c[..level]).Distinct().ToList();
            var citing = branches.Where(p => PartsHeadingRef.References(Text(p), parentHeading)).ToList();
            List<string> keep;
            string reason;
            if (citing.Count == 1)
            {
                keep = citing;
                reason = $"Uraian resmi subpos {citing[0]} menyebut pos mesin induk {pos}: \"{Shown(citing[0])}\".";
            }
            else
            {
                var other = branches.Where(p => !citing.Contains(p) && PartsHeadingRef.CitesAnyCode(Text(p))).ToList();
                keep = branches.Except(other).ToList();
                if (other.Count == 0 || keep.Count == 0) break;
                reason = $"Subpos {string.Join(", ", other)} hanya untuk mesin pos lain (bukan {pos}), sehingga tidak dapat dipakai.";
            }
            pool = pool.Where(c => keep.Any(k => c.StartsWith(k))).ToList();
            chain.Add(new ReasoningStep
            {
                Rule = "KUM_HS_6", CandidateCode = keep.Count == 1 ? keep[0] : h.Code,
                Decision = CandidateDecision.Retained, Reason = reason, SourceIds = [sourceId]
            });
        }
        return pool;
    }

    private static ClassificationResult ExpertReview(
        ClassificationResult result, CandidateHeading heading, List<string> codes, string reason)
    {
        result.Status = ResultStatus.NeedExpertReview;
        result.UnresolvedCodes = codes.Take(10).ToList();
        result.ReasoningChain.Add(new ReasoningStep
        {
            Rule = "KUM_HS_6", CandidateCode = heading.Code,
            Decision = CandidateDecision.Unresolved, Reason = reason,
            SourceIds = [heading.SourceId ?? heading.Code]
        });
        return result;
    }

    /// <summary>Official Indonesian text of one node without leading dashes / trailing colon.</summary>
    private static string Clean(string? text) =>
        (text ?? "").Split(" / ")[0].Trim().TrimStart('-', ' ', '•').TrimEnd(':', ' ');

    private static string Label(CandidateHeading h, string code) =>
        Clean(h.ChildDescriptions.GetValueOrDefault(code)?.Split(" | ").Last());

    /// <summary>
    /// GRI 6: compares only subheadings at the SAME level (Req 16.3). Picks the first level where the
    /// remaining tariff lines diverge — one-dash group (8429.5), 6-digit (8429.52), then 8-digit — and
    /// offers each branch with its official BTKI text, plus "none of these" for expert review.
    /// </summary>
    public static ClassificationQuestion BuildSubheadingQuestion(FactSet facts, CandidateHeading h, List<string> pool)
    {
        var level = new[] { 6, 7, 10 }.First(l => pool.Select(c => c[..l]).Distinct().Count() > 1);

        var prefixes = pool.Select(c => c[..level]).Distinct().ToList();
        string BranchLabel(string prefix)
        {
            // Own text only if it actually distinguishes: source data sometimes repeats the heading
            // text on every 6-digit row (8517.13 and 8517.14 looked identical)
            if (level < 10 && h.ParentDescriptions.TryGetValue(prefix, out var own)
                && prefixes.Count(p => h.ParentDescriptions.GetValueOrDefault(p) == own) == 1)
                return Clean(own);
            var lines = pool.Where(c => c.StartsWith(prefix)).ToList();
            if (lines.Count == 1) return Label(h, lines[0]);
            // No text for this branch in the data: describe it by its tariff lines
            var specific = lines.Select(c => Label(h, c)).Where(l => !l.StartsWith("Lain-lain", StringComparison.OrdinalIgnoreCase)).Distinct();
            var joined = string.Join("; ", specific);
            return joined.Length == 0 ? "Lain-lain" : (joined.Length > 160 ? joined[..160] + "…" : joined) + " (dan lainnya)";
        }

        // Context = deepest official text shared by all branches, so a bare "Lain-lain" option still reads correctly
        var shared = new[] { 7, 6 }.Where(l => l < level)
            .Select(l => pool[0][..l]).FirstOrDefault(p => pool.All(c => c.StartsWith(p)) && h.ParentDescriptions.ContainsKey(p));
        var context = shared is not null ? Clean(h.ParentDescriptions[shared]) : Clean(h.Description);

        // Best fact match first; ordering is only a hint, every branch stays selectable
        var factTerms = Terms($"{facts.GetValue("generic_name")} {facts.GetValue("commercial_name")} {facts.GetValue("primary_function")}");
        var branches = prefixes
            .Select(p => new QuestionChoice(p, $"{p} — {BranchLabel(p)}"))
            .OrderByDescending(b => Terms(b.Label).Count(factTerms.Contains)).ThenBy(b => b.Value)
            .ToList();

        return new ClassificationQuestion
        {
            FactType = FactExtractionService.SubheadingChoice,
            QuestionText = $"Pos {h.Code} ({context}). Uraian subpos mana yang paling sesuai dengan barang Anda?",
            Choices = [.. branches, new QuestionChoice(NoneChoice, "Tidak ada yang sesuai / tidak yakin — minta review ahli")],
            InformationGain = 1.0,
            Impact = new CandidateImpact { CandidatesAffected = pool.Count, RetainedCodes = pool },
        };
    }

    private static ClassificationResult Determine(
        ClassificationResult result, string code, ConfidenceLevel confidence, string reason, string sourceId)
    {
        result.FinalCode = code;
        result.Confidence = confidence;
        // A conclusive GRI 6 descent clears a NO_MATCH flag (weak lexical search is expected on the AI
        // path), but keeps a deliberate expert flag from KUM HS 4 ("most akin").
        var expertFromRule4 = result.ReasoningChain.Any(s => s.Rule == "KUM_HS_4");
        if (result.Status != ResultStatus.NeedExpertReview || !expertFromRule4)
            result.Status = ResultStatus.Determined;
        result.ReasoningChain.Add(new ReasoningStep
        {
            Rule = "KUM_HS_6", CandidateCode = code,
            Decision = CandidateDecision.Retained,
            Reason = reason,
            SourceIds = [sourceId]
        });
        return result;
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static double SpecificityScore(CandidateHeading c)
    {
        var desc = c.Description.ToLowerInvariant();
        double score = 0;
        if (desc.Contains("lain-lain") || desc.Contains("other") || desc.Contains("nesoi")) score -= 1.0;
        if (desc.Contains("bagian") || desc.Contains("parts")) score -= 0.5;
        if (c.ChildSubheadings.Count == 0) score += 0.5; // leaf node = more specific
        return score;
    }

    private static double Overlap(FactSet facts, string text)
    {
        var terms = Terms($"{facts.GetValue("generic_name")} {facts.GetValue("commercial_name")} " +
                          $"{facts.GetValue("primary_function")} {facts.GetValue("material")}");
        if (terms.Count == 0) return 0;
        var textTerms = Terms(text);
        return (double)terms.Count(textTerms.Contains) / terms.Count;
    }

    /// <summary>
    /// GRI 1 relevance. With official heading text, only the heading's own terms decide
    /// (subheading text belongs to GRI 6); child lines merely break ties at 10% weight.
    /// Without heading text (e.g. DOCX import missing it) all child lines stand in for it.
    /// </summary>
    private static double HeadingScore(FactSet facts, CandidateHeading c)
    {
        var children = string.Join(' ', c.ChildDescriptions.Values);
        return c.HasHeadingText
            ? Overlap(facts, c.Description) + (Overlap(facts, c.Description) > 0 ? 0.1 * Overlap(facts, children) : 0)
            : Overlap(facts, $"{c.Description} {children}");
    }

    /// <summary>
    /// How much of the heading's own wording (Indonesian, exclusion clauses removed) the goods' name accounts for.
    /// "Motor dan generator listrik" is about electric motors; "Perkakas tangan ... dengan motor listrik terpasang"
    /// only mentions them. Used to break ties between headings that cover the name equally (KUM HS 3a), instead of
    /// leaving the choice to which child lines happen to share a word. Not part of the confidence score.
    /// </summary>
    private static double Focus(FactSet facts, CandidateHeading c)
    {
        var name = Terms($"{facts.GetValue("generic_name")} {facts.GetValue("commercial_name")} " +
                         $"{facts.GetValue("primary_function")} {facts.GetValue("material")}");
        var own = Terms(WithoutExclusions(c.Description.Split(" / ")[0]));
        return own.Count == 0 ? 0 : (double)own.Count(name.Contains) / own.Count;
    }

    private static bool ContainsKeyword(string text, string keyword)
        => !string.IsNullOrWhiteSpace(keyword)
        && text.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static readonly HashSet<string> StopWords =
    [
        "lain", "other", "others", "dengan", "untuk", "atau", "yang", "dari", "dalam", "tidak",
        "with", "without", "for", "from", "the", "and", "not", "exceeding", "melebihi", "jenis", "kind",
    ];

    private static HashSet<string> Terms(string? text) =>
        System.Text.RegularExpressions.Regex.Split((text ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(t => t.Length > 3 && !StopWords.Contains(t))
            // ponytail: English plural "-s" only (excavators → excavator); no real stemmer.
            .Select(t => t.Length > 4 && t.EndsWith('s') && !t.EndsWith("ss") ? t[..^1] : t)
            .ToHashSet();

    private static readonly System.Text.RegularExpressions.Regex ExclusionClause = new(
        @"\b(tidak termasuk|selain|kecuali|other than|excluding|not including)\b[^|/;]*",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>"Komputer personal tidak termasuk komputer portabel" must not match "portabel".</summary>
    internal static string WithoutExclusions(string? text) => ExclusionClause.Replace(text ?? "", " ");

    /// <summary>
    /// Scores each 8-digit child by overlap between fact terms and its official description.
    /// Returns the child only when exactly one has the top positive score.
    /// ponytail: lexical overlap only — numeric thresholds ("not exceeding 20 t") are not parsed,
    ///           so threshold-split headings go to expert review. Upgrade: parse thresholds per child.
    /// </summary>
    internal static (string? Best, List<string> Ranked) SelectBestSubheading(
        FactSet facts, CandidateHeading heading, List<string>? pool = null)
    {
        var factTerms = Terms(string.Join(' ', new[]
        {
            "generic_name", "commercial_name", "primary_function", "material", "form",
            "primary_use", "mobility", "operating_area", "power_source", "mounting",
        }.Select(facts.GetValue)));
        // GRI 6 compares subheadings among themselves: words already in the locked heading text
        // are shared by every child and cannot discriminate.
        if (heading.HasHeadingText) factTerms.ExceptWith(Terms(heading.Description));

        var scored = (pool ?? heading.ChildSubheadings)
            .Select(code => (code, score: Terms(WithoutExclusions(heading.ChildDescriptions.GetValueOrDefault(code))).Count(factTerms.Contains)))
            .OrderByDescending(x => x.score).ThenBy(x => x.code)
            .ToList();

        var ranked = scored.Where(x => x.score > 0).Take(5).Select(x => x.code).ToList();
        var top = scored[0].score;
        var isUnique = top > 0 && scored.Count(x => x.score == top) == 1;
        return (isUnique ? scored[0].code : null, ranked.Count > 0 ? ranked : scored.Take(5).Select(x => x.code).ToList());
    }
}
