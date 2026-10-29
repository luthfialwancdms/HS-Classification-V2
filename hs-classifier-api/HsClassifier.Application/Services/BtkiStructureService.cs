using HsClassifier.Application.Interfaces;

namespace HsClassifier.Application.Services;

/// <summary>
/// Task 2.2: Build parent-child relationships from parsed nodes.
/// Task 2.3: Validate structure — duplicates, missing parents, orphans.
/// </summary>
public static class BtkiStructureService
{
    // ─── 2.2: Hierarchy Builder ───────────────────────────────────────────────

    /// <summary>
    /// Ensures every node has a valid ParentCode.
    /// Marks nodes STRUCTURE_AMBIGUITY when parent cannot be derived.
    /// </summary>
    public static void BuildHierarchy(List<ParsedTariffNode> nodes)
    {
        var codeSet = new HashSet<string>(nodes.Select(n => n.Code));

        foreach (var node in nodes)
        {
            if (node.ParentCode is null) continue;

            // A parent that is a structural prefix of the node's own code ("8471.30" for "8471.30.20")
            // is certain, even though the batch holds only 8-digit tariff lines and so never contains it.
            // Flagging those made every real DOCX node "ambiguous". Only conflicting signals are ambiguous.
            if (node.Code.StartsWith(node.ParentCode + ".", StringComparison.Ordinal)) continue;

            // Parent does not match the code's own hierarchy: accept it only if it exists in the batch
            if (!codeSet.Contains(node.ParentCode))
            {
                // Try one level up (6-digit → 4-digit)
                var parts = node.Code.Split('.');
                if (parts.Length == 3)
                {
                    var fourDigit = parts[0];
                    if (codeSet.Contains(fourDigit))
                    {
                        node.ParentCode = fourDigit;
                        continue;
                    }
                }

                // Parent not found at any level
                node.IsAmbiguous = true;
                node.AmbiguityReason = $"Parent '{node.ParentCode}' not found in dataset";
            }
        }
    }

    // ─── 2.3: Structure Validator ─────────────────────────────────────────────

    public static List<ParseIssue> ValidateStructure(List<ParsedTariffNode> nodes, int? expectedCount = null)
    {
        var issues = new List<ParseIssue>();
        var seen = new Dictionary<string, int>();

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            // Duplicate codes
            if (seen.TryGetValue(node.Code, out var firstRow))
            {
                issues.Add(new ParseIssue
                {
                    Severity = "ERROR", Type = "DUPLICATE_CODE",
                    Code = node.Code, RowNumber = i + 1,
                    Message = $"Code {node.Code} already seen at row {firstRow}"
                });
            }
            else seen[node.Code] = i + 1;

            // Invalid format: a tariff line (XXXX.XX.XX) or a hierarchy node (XXXX, XXXX.X, XXXX.XX)
            if (!System.Text.RegularExpressions.Regex.IsMatch(node.Code, @"^\d{4}(\.\d{1,2}(\.\d{2})?)?$"))
            {
                issues.Add(new ParseIssue
                {
                    Severity = "ERROR", Type = "INVALID_CODE_FORMAT",
                    Code = node.Code, RowNumber = i + 1,
                    Message = $"Code '{node.Code}' does not match XXXX, XXXX.X, XXXX.XX or XXXX.XX.XX format"
                });
            }

            // Empty description
            if (string.IsNullOrWhiteSpace(node.DescriptionId))
            {
                issues.Add(new ParseIssue
                {
                    Severity = "WARNING", Type = "EMPTY_DESCRIPTION",
                    Code = node.Code, RowNumber = i + 1,
                    Message = $"Indonesian description is empty for {node.Code}"
                });
            }

            // Ambiguous structure
            if (node.IsAmbiguous)
            {
                issues.Add(new ParseIssue
                {
                    Severity = "WARNING", Type = "STRUCTURE_AMBIGUITY",
                    Code = node.Code, RowNumber = i + 1,
                    Message = node.AmbiguityReason ?? "Parent hierarchy ambiguous"
                });
            }
        }

        // Total count check
        if (expectedCount.HasValue && nodes.Count != expectedCount.Value)
        {
            issues.Add(new ParseIssue
            {
                Severity = "INFO", Type = "COUNT_MISMATCH",
                Message = $"Parsed {nodes.Count} nodes; expected {expectedCount.Value}",
                RowNumber = 0
            });
        }

        return issues;
    }

    // ─── 2.4: Dry Run Validation ──────────────────────────────────────────────

    public static DryRunResult DryRun(List<ParsedTariffNode> nodes, int? expectedTotal = null)
    {
        BuildHierarchy(nodes);
        var issues = ValidateStructure(nodes, expectedTotal);

        return new DryRunResult
        {
            TotalNodes = nodes.Count,
            AmbiguousNodes = nodes.Count(n => n.IsAmbiguous),
            ErrorCount = issues.Count(i => i.Severity == "ERROR"),
            WarningCount = issues.Count(i => i.Severity == "WARNING"),
            Issues = issues,
            CanCommit = issues.All(i => i.Severity != "ERROR") && nodes.All(n => !n.IsAmbiguous),
        };
    }
}

/// <summary>Admin decision on one STRUCTURE_AMBIGUITY node.</summary>
public record AmbiguityResolution(string Code, HsClassifier.Domain.Enums.AmbiguityDecision Decision, string? CorrectedParent);

public static class AmbiguityGate
{
    /// <summary>
    /// Commit gate (Req 1.11): no structural ERRORs, and every ambiguous node has a decision.
    /// Approved nodes are kept (with the corrected parent if given), rejected nodes are dropped.
    /// Replaces the old "zero ambiguous nodes" rule, under which approving an ambiguity meant nothing.
    /// </summary>
    public static (List<ParsedTariffNode> Nodes, string? Blocker) ResolveForCommit(
        List<ParsedTariffNode> nodes, IEnumerable<AmbiguityResolution> decisions)
    {
        var dry = BtkiStructureService.DryRun(nodes);
        if (dry.ErrorCount > 0)
            return ([], $"Dry run masih memiliki {dry.ErrorCount} error struktur.");

        var byCode = decisions.GroupBy(d => d.Code).ToDictionary(g => g.Key, g => g.Last());
        var pending = nodes
            .Where(n => n.IsAmbiguous && (!byCode.TryGetValue(n.Code, out var d)
                                          || d.Decision == HsClassifier.Domain.Enums.AmbiguityDecision.Pending))
            .Select(n => n.Code).ToList();
        if (pending.Count > 0)
            return ([], $"{pending.Count} ambiguitas belum diputuskan: {string.Join(", ", pending.Take(10))}" +
                        (pending.Count > 10 ? ", …" : "") + ".");

        var result = new List<ParsedTariffNode>(nodes.Count);
        foreach (var n in nodes)
        {
            if (n.IsAmbiguous)
            {
                var d = byCode[n.Code];
                if (d.Decision == HsClassifier.Domain.Enums.AmbiguityDecision.Rejected) continue;
                if (!string.IsNullOrWhiteSpace(d.CorrectedParent)) n.ParentCode = d.CorrectedParent.Trim();
                n.IsAmbiguous = false;
            }
            result.Add(n);
        }
        return (result, null);
    }
}

public class DryRunResult
{
    public int TotalNodes { get; set; }
    public int AmbiguousNodes { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<ParseIssue> Issues { get; set; } = new();
    public bool CanCommit { get; set; }
}
