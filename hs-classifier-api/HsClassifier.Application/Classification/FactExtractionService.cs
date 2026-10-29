using System.Text.Json;
using System.Text.RegularExpressions;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Application.Classification;

/// <summary>
/// Task 6.1: Extracts 35 fact types from user input.
/// Task 6.3: Detects conflicting facts and unknown fields.
/// LLM is called externally — this service processes structured input from the form.
/// </summary>
public class FactExtractionService
{
    public static readonly string[] AllFactTypes =
    [
        "commercial_name","generic_name","brand","model",
        "material","composition","percentage","concentration",
        "form","dimensions","weight","capacity","power_rating","voltage",
        "manufacturing_process","finishing_process",
        "primary_function","secondary_function","operating_principle","input_process_output",
        "power_source","fuel_type","energy_rating",
        "primary_use","operating_area","mobility","mounting",
        "complete_status","finished_status","assembled_status","ckd_skd_status",
        "bulk_unit","mixture_composite","set_kit","part_accessory",
        "used_condition","waste_scrap","packaging_container","supporting_documents",
        // Answer to a KUM HS 6 question: code prefix of the official subheading text the user confirmed
        SubheadingChoice,
        // Goods-condition evidence (ConditionGate): KUM HS 2(a), retail set, mixture, parts
        "essential_character_present","all_components_together",
        "set_single_purpose","set_retail_ready","set_essential_item",
        "mixture_major_component","mixture_major_percentage",
        "part_dedicated","part_parent_machine","part_parent_heading",
        "packaging_kind","packaging_contents","packaging_specially_shaped","packaging_long_term",
        "packaging_sold_with","packaging_essential","packaging_normally_used","packaging_reusable",
        "accessory_necessary","accessory_standalone",
        "multifunction","multi_structure","multi_principal_clear","functional_unit","multi_principal",
        "bulk_physical_state","bulk_net_quantity","bulk_presentation",
        "used_still_functions","used_repairable","scrap_material","scrap_contaminated"
    ];

    public const string SubheadingChoice = "subheading_choice";

    /// <summary>
    /// Task 6.1: Build FactSet from a dictionary of user-provided values.
    /// Missing keys → UNKNOWN (never defaulted).
    /// </summary>
    public FactSet ExtractFromInput(Dictionary<string, string?> userInput)
    {
        var factSet = new FactSet();

        foreach (var type in AllFactTypes)
        {
            if (userInput.TryGetValue(type, out var raw) && !string.IsNullOrWhiteSpace(raw))
            {
                var normalized = NormalizeValue(type, raw.Trim());
                factSet.Facts[type] = new Fact
                {
                    Type = type,
                    RawValue = raw,
                    NormalizedValue = normalized,
                    Source = FactSource.User,
                    Status = FactStatus.Verified,
                    EvidenceReference = "user_input"
                };
            }
            else
            {
                // Task 6.1: NEVER default — always UNKNOWN
                factSet.Facts[type] = new Fact
                {
                    Type = type,
                    Status = FactStatus.Unknown,
                    Source = FactSource.User
                };
            }
        }

        return factSet;
    }

    /// <summary>
    /// Merges LLM-extracted facts into existing FactSet.
    /// Task 6.3: Detects conflicts when same fact has different values from different sources.
    /// </summary>
    public void MergeLlmFacts(FactSet existing, Dictionary<string, string?> llmFacts, string evidenceRef)
    {
        foreach (var (type, llmValue) in llmFacts)
        {
            if (string.IsNullOrWhiteSpace(llmValue)) continue;
            var normalized = NormalizeValue(type, llmValue.Trim());

            if (existing.Facts.TryGetValue(type, out var current)
                && current.Status == FactStatus.Verified
                && current.NormalizedValue != normalized)
            {
                // Conflict — different values from user vs LLM
                existing.Facts[type] = new Fact
                {
                    Type = current.Type,
                    RawValue = current.RawValue,
                    NormalizedValue = current.NormalizedValue,
                    Source = current.Source,
                    Status = FactStatus.Conflicting,
                    EvidenceReference = $"user:{current.NormalizedValue} vs llm:{normalized}"
                };
            }
            else if (!existing.Facts.TryGetValue(type, out var f) || f.Status == FactStatus.Unknown)
            {
                existing.Facts[type] = new Fact
                {
                    Type = type,
                    RawValue = llmValue,
                    NormalizedValue = normalized,
                    Source = FactSource.Document,
                    Status = FactStatus.Verified,
                    EvidenceReference = evidenceRef
                };
            }
        }
    }

    // Unit normalization
    private static string NormalizeValue(string factType, string value)
    {
        if (factType == "weight")
        {
            var m = Regex.Match(value, @"([\d.,]+)\s*(ton|kg|gram|lb)", RegexOptions.IgnoreCase);
            if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
            {
                return m.Groups[2].Value.ToLower() switch
                {
                    "ton" => $"{num * 1000} kg",
                    "gram" => $"{num / 1000} kg",
                    "lb" => $"{num * 0.453592m} kg",
                    _ => $"{num} kg"
                };
            }
        }
        return value;
    }
}


