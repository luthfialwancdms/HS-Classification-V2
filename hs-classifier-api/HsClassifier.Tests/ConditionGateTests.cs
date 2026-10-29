using HsClassifier.Application.Classification;
using HsClassifier.Application.Classification.Rules;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Tests;

/// <summary>KUM HS 2(a), 2(b)/3(b) mixture, 3(b) retail set, and Section XVI Note 2 parts — before candidate search.</summary>
public class ConditionGateTests
{
    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    [Fact]
    public void Complete_goods_trigger_nothing()
    {
        var o = ConditionGate.Evaluate(Facts(("generic_name", "laptop"), ("assembled_status", "complete")));
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Empty(o.Steps);
        Assert.Null(o.EssentialTerm);
    }

    // ─── KUM HS 2(a) ──────────────────────────────────────────────────────────

    [Fact]
    public void Ckd_asks_essential_character_and_same_shipment()
    {
        var o = ConditionGate.Evaluate(Facts(("generic_name", "sepeda motor"), ("assembled_status", "CKD")));
        Assert.Equal(ConditionStatus.NeedClarification, o.Status);
        Assert.Equal(["all_components_together", "essential_character_present"], o.Questions.Select(q => q.FactType).Order());
        Assert.All(o.Questions, q => Assert.NotNull(q.Choices));
    }

    [Fact]
    public void Ckd_with_essential_character_and_complete_kit_is_treated_as_complete()
    {
        var o = ConditionGate.Evaluate(Facts(("generic_name", "sepeda motor"), ("assembled_status", "CKD"),
            ("essential_character_present", "yes"), ("all_components_together", "yes")));
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        var step = Assert.Single(o.Steps);
        Assert.Equal("KUM_HS_2A", step.Rule);
        Assert.Contains("TREAT_AS_COMPLETE", step.Reason);
    }

    [Theory]
    [InlineData("no", "yes")]   // no essential character → classify as presented
    [InlineData("yes", "no")]   // split shipment
    [InlineData("unsure", "yes")]
    public void Ckd_failing_2a_goes_to_expert(string essential, string together)
    {
        var o = ConditionGate.Evaluate(Facts(("generic_name", "sepeda motor"), ("assembled_status", "CKD"),
            ("essential_character_present", essential), ("all_components_together", together)));
        Assert.Equal(ConditionStatus.NeedExpertReview, o.Status);
        Assert.Contains(o.Steps, s => s.Rule == "KUM_HS_2A" && s.Decision == CandidateDecision.Unresolved);
    }

    [Fact]
    public void Incomplete_only_needs_essential_character()
    {
        var o = ConditionGate.Evaluate(Facts(("generic_name", "mesin"), ("assembled_status", "incomplete")));
        Assert.Equal(["essential_character_present"], o.Questions.Select(q => q.FactType));
    }

    // ─── Retail set (KUM HS 3b) ───────────────────────────────────────────────

    [Fact]
    public void Retail_set_asks_three_tests()
    {
        var o = ConditionGate.Evaluate(Facts(("generic_name", "set alat cukur"), ("set_kit", "retail_set")));
        Assert.Equal(ConditionStatus.NeedClarification, o.Status);
        Assert.Equal(["set_essential_item", "set_retail_ready", "set_single_purpose"], o.Questions.Select(q => q.FactType).Order());
    }

    [Fact]
    public void Valid_retail_set_classifies_by_essential_item()
    {
        var o = ConditionGate.Evaluate(Facts(("generic_name", "set alat cukur"), ("set_kit", "retail_set"),
            ("set_single_purpose", "yes"), ("set_retail_ready", "yes"), ("set_essential_item", "alat cukur listrik")));
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Equal("alat cukur listrik", o.EssentialTerm);
        Assert.Contains(o.Steps, s => s.Rule == "KUM_HS_3B");
    }

    [Fact]
    public void Not_a_retail_set_goes_to_expert_for_item_by_item()
    {
        var o = ConditionGate.Evaluate(Facts(("generic_name", "paket"), ("set_kit", "retail_set"),
            ("set_single_purpose", "no"), ("set_retail_ready", "yes"), ("set_essential_item", "x")));
        Assert.Equal(ConditionStatus.NeedExpertReview, o.Status);
    }

    // ─── Mixture (KUM HS 2b → 3b) ─────────────────────────────────────────────

    [Fact]
    public void Mixture_needs_component_and_percentage_then_uses_essential_component()
    {
        var ask = ConditionGate.Evaluate(Facts(("generic_name", "campuran"), ("mixture_composite", "mixture")));
        Assert.Equal(["mixture_major_component", "mixture_major_percentage"], ask.Questions.Select(q => q.FactType).Order());

        var o = ConditionGate.Evaluate(Facts(("generic_name", "campuran"), ("mixture_composite", "mixture"),
            ("mixture_major_component", "gula"), ("mixture_major_percentage", "85%")));
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Equal("gula", o.EssentialTerm);
        Assert.Contains(o.Steps, s => s.Rule == "KUM_HS_2B" && s.Reason.Contains("85%"));
    }

    // ─── Parts (Catatan 2 Bagian XVI) ─────────────────────────────────────────

    [Fact]
    public void Part_asks_whether_dedicated_and_parent_machine()
    {
        var o = ConditionGate.Evaluate(Facts(("generic_name", "bucket"), ("part_accessory", "part")));
        Assert.Equal(["part_dedicated", "part_parent_machine"], o.Questions.Select(q => q.FactType).Order());
    }

    [Fact]
    public void Dedicated_part_routes_to_parent_machine()
    {
        var o = ConditionGate.Evaluate(Facts(("generic_name", "bucket"), ("part_accessory", "part"),
            ("part_dedicated", "yes"), ("part_parent_machine", "excavator")));
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Equal("excavator", o.PartOfMachine);
    }

    [Fact]
    public void General_use_part_is_classified_as_its_own_article()
    {
        var o = ConditionGate.Evaluate(Facts(("generic_name", "baut"), ("part_accessory", "part"), ("part_dedicated", "no")));
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Null(o.PartOfMachine);
        Assert.Contains(o.Steps, s => s.Rule == "SECTION_XVI_NOTE_2");
    }

    // ─── Parts-heading reference parsing (real BTKI texts) ────────────────────

    [Theory]
    [InlineData("Bagian yang cocok ... dengan mesin dari pos 84.25 sampai dengan 84.30.", "8429", true)]
    [InlineData("Bagian yang cocok ... dengan mesin dari pos 84.25 sampai dengan 84.30.", "8431", false)]
    [InlineData("Bagian dan aksesori ... dengan mesin dari pos 84. 70 sampai dengan 84.72.", "8471", true)]
    [InlineData("Bagian yang cocok ... dengan mesin dari pos 84.07 atau 84.08.", "8408", true)]
    [InlineData("Bagian yang cocok ... dengan mesin dari pos 84.07 atau 84.08.", "8409", false)]
    [InlineData("Bagian ... dengan aparatus dari pos 85.35, 85.36 atau 85.37.", "8536", true)]
    [InlineData("Bagian dan aksesori kendaraan bermotor dari pos 87.01 sampai dengan 87.05.", "8703", true)]
    [InlineData("Sekrup, baut, mur ... dari besi atau baja.", "8429", false)]
    public void Parts_heading_reference(string text, string heading, bool covers) =>
        Assert.Equal(covers, PartsHeadingRef.Covers(text, heading));

    [Theory]
    [InlineData("Bagian yang cocok untuk digunakan semata-mata ...", true)]
    [InlineData("Bagian dan aksesori kendaraan ...", true)]
    [InlineData("Sekrup, baut, mur ...", false)]
    public void Recognises_parts_headings(string text, bool isParts) => Assert.Equal(isParts, PartsHeadingRef.IsPartsHeading(text));
}

/// <summary>Live findings: excavator bucket landed in 8431.1 (heading 84.25 parts); retail set searched a 3-word phrase that BTKI never uses.</summary>
public class ConditionRoutingTests
{
    [Fact]
    public void Parts_heading_branch_citing_parent_heading_is_kept()
    {
        var h = new CandidateHeading
        {
            Code = "8431", HasHeadingText = true,
            Description = "Bagian yang cocok … dengan mesin dari pos 84.25 sampai dengan 84.30.",
            ParentDescriptions =
            {
                ["8431.1"] = "- Dari mesin pada pos 84.25 :",
                ["8431.2"] = "- Dari mesin pada pos 84.27 :",
                ["8431.4"] = "- Dari mesin pada pos 84.26, 84.29 atau 84.30 :",
            },
            ChildSubheadings = ["8431.10.13", "8431.20.00", "8431.41.00", "8431.42.00"],
            ChildDescriptions =
            {
                ["8431.10.13"] = "- - - Dari barang pada subpos 8425.11.00", ["8431.20.00"] = "- Dari mesin pada pos 84.27",
                ["8431.41.00"] = "- - Ember, sekop, grab dan cakram", ["8431.42.00"] = "- - Pisau buldoser atau angledoser",
            },
        };
        var chain = new List<ReasoningStep>();
        var pool = RulesEngine.NarrowByParentHeading(h, h.ChildSubheadings, "8429", chain, "src");
        Assert.Equal(["8431.41.00", "8431.42.00"], pool);   // one-dash group 8431.4 only; inside it, the user still chooses
        Assert.Single(chain);
    }

    [Fact]
    public void Branch_reserved_for_another_machine_is_dropped()
    {
        var h = new CandidateHeading
        {
            Code = "8431", HasHeadingText = true, Description = "Bagian … pos 84.25 sampai dengan 84.30.",
            ParentDescriptions = { ["8431.41"] = "- - Bucket, sekop, grab dan grip :" },
            ChildSubheadings = ["8431.41.10", "8431.41.90"],
            ChildDescriptions = { ["8431.41.10"] = "- - - Untuk mesin dari pos 84.26", ["8431.41.90"] = "- - - Lain-lain" },
        };
        var pool = RulesEngine.NarrowByParentHeading(h, h.ChildSubheadings, "8429", [], "src");
        Assert.Equal(["8431.41.90"], pool);
    }

    [Fact]
    public void Sub_phrases_longest_first_without_full_name()
    {
        Assert.Equal(["alat cukur", "cukur listrik"], CandidateGeneratorServiceProbe.SubPhrases("alat cukur listrik"));
        Assert.Empty(CandidateGeneratorServiceProbe.SubPhrases("alat cukur").Where(p => p.Split(' ').Length < 2));
    }
}

internal static class CandidateGeneratorServiceProbe
{
    public static List<string> SubPhrases(string s) =>
        HsClassifier.Infrastructure.Services.CandidateGeneratorService.SubPhrases(s).ToList();
}

/// <summary>KUM HS 5(a) cases/containers and 5(b) packing — classified with the goods, or separately.</summary>
public class PackagingTests
{
    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    private static readonly (string, string)[] Container =
        [("generic_name", "tas kamera"), ("packaging_container", "with_goods")];

    [Fact]
    public void Asks_kind_and_contents_first()
    {
        var o = ConditionGate.Evaluate(Facts(Container));
        Assert.Equal(ConditionStatus.NeedClarification, o.Status);
        Assert.Equal(["packaging_contents", "packaging_kind"], o.Questions.Select(q => q.FactType).Order());
    }

    [Fact]
    public void Case_5a_asks_its_four_tests()
    {
        var o = ConditionGate.Evaluate(Facts([.. Container, ("packaging_kind", "case"), ("packaging_contents", "kamera digital")]));
        Assert.Equal(["packaging_essential", "packaging_long_term", "packaging_sold_with", "packaging_specially_shaped"],
            o.Questions.Select(q => q.FactType).Order());
    }

    [Fact]
    public void Case_passing_5a_is_classified_with_the_article()
    {
        var o = ConditionGate.Evaluate(Facts([.. Container, ("packaging_kind", "case"), ("packaging_contents", "kamera digital"),
            ("packaging_specially_shaped", "yes"), ("packaging_long_term", "yes"), ("packaging_sold_with", "yes"), ("packaging_essential", "no")]));
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Equal("kamera digital", o.EssentialTerm);
        Assert.Contains(o.Steps, s => s.Rule == "KUM_HS_5A" && s.Reason.Contains("bersama barangnya"));
    }

    [Fact]
    public void Case_failing_5a_is_classified_separately()
    {
        var o = ConditionGate.Evaluate(Facts([.. Container, ("packaging_kind", "case"), ("packaging_contents", "kamera digital"),
            ("packaging_specially_shaped", "no"), ("packaging_long_term", "yes"), ("packaging_sold_with", "yes"), ("packaging_essential", "no")]));
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Null(o.EssentialTerm);                       // keeps searching for the container itself
        Assert.Contains(o.Steps, s => s.Rule == "KUM_HS_5A" && s.Reason.Contains("tersendiri"));
    }

    [Fact]
    public void Packing_5b_with_goods_unless_reusable()
    {
        (string, string)[] packing = [.. Container, ("packaging_kind", "packing"), ("packaging_contents", "televisi")];
        Assert.Equal(["packaging_normally_used", "packaging_reusable"],
            ConditionGate.Evaluate(Facts(packing)).Questions.Select(q => q.FactType).Order());

        var with = ConditionGate.Evaluate(Facts([.. packing, ("packaging_normally_used", "yes"), ("packaging_reusable", "no")]));
        Assert.Equal("televisi", with.EssentialTerm);
        Assert.Contains(with.Steps, s => s.Rule == "KUM_HS_5B");

        var reusable = ConditionGate.Evaluate(Facts([.. packing, ("packaging_normally_used", "yes"), ("packaging_reusable", "yes")]));
        Assert.Null(reusable.EssentialTerm);
    }

    [Fact]
    public void Unsure_goes_to_expert()
    {
        var o = ConditionGate.Evaluate(Facts([.. Container, ("packaging_kind", "packing"), ("packaging_contents", "televisi"),
            ("packaging_normally_used", "unsure"), ("packaging_reusable", "no")]));
        Assert.Equal(ConditionStatus.NeedExpertReview, o.Status);
    }

    [Fact]
    public void Reasoning_already_recorded_is_not_repeated()
    {
        var earlier = new[] { ("KUM_HS_1", "[8501] match"), ("KUM_HS_6", "[8501] ask A") };
        var steps = new List<ReasoningStep>
        {
            new() { Rule = "KUM_HS_1", CandidateCode = "8501", Reason = "match" },
            new() { Rule = "KUM_HS_6", CandidateCode = "8501", Reason = "ask B" },
        };
        var fresh = HsClassifier.Infrastructure.Services.ClassificationOrchestrator.NewSteps(earlier, steps);
        Assert.Equal(["ask B"], fresh.Select(s => s.Reason));
    }
}

/// <summary>22.4 remainder: accessories, multifunction machines (Section XVI Notes 3/4), bulk, used goods and waste/scrap.</summary>
public class ConditionModuleTests
{
    private static ConditionOutcome Eval(params (string, string)[] kv) =>
        ConditionGate.Evaluate(new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2)));

    private static List<string> Asked(ConditionOutcome o) => o.Questions.Select(q => q.FactType).Order().ToList();

    // ─── accessory ───
    private static readonly (string, string)[] Acc = [("generic_name", "rak atap"), ("part_accessory", "accessory")];

    [Fact]
    public void Accessory_asks_necessity_and_standalone_first()
        => Assert.Equal(["accessory_necessary", "accessory_standalone"], Asked(Eval(Acc)));

    [Fact]
    public void Standalone_accessory_is_classified_by_itself()
    {
        var o = Eval([.. Acc, ("accessory_necessary", "no"), ("accessory_standalone", "yes")]);
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Null(o.PartOfMachine);
        Assert.Contains(o.Steps, s => s.Rule == "ACCESSORY");
    }

    [Fact]
    public void Dedicated_accessory_routes_to_parts_and_accessories_heading()
    {
        var o = Eval([.. Acc, ("accessory_necessary", "no"), ("accessory_standalone", "no"),
            ("part_dedicated", "yes"), ("part_parent_machine", "mobil")]);
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Equal("mobil", o.PartOfMachine);
        Assert.True(o.Accessory);
    }

    [Fact]
    public void Necessary_accessory_is_a_part()
    {
        var o = Eval([.. Acc, ("accessory_necessary", "yes"), ("accessory_standalone", "no"),
            ("part_dedicated", "yes"), ("part_parent_machine", "excavator")]);
        Assert.Equal("excavator", o.PartOfMachine);
        Assert.False(o.Accessory);
    }

    [Fact]
    public void Unsure_accessory_goes_to_expert()
        => Assert.Equal(ConditionStatus.NeedExpertReview,
            Eval([.. Acc, ("accessory_necessary", "unsure"), ("accessory_standalone", "no")]).Status);

    // ─── multifunction ───
    private static readonly (string, string)[] Multi = [("generic_name", "mesin fotokopi printer scanner"), ("multifunction", "yes")];

    [Fact]
    public void Multifunction_asks_structure_first()
        => Assert.Equal(["multi_structure"], Asked(Eval(Multi)));

    [Fact]
    public void Note3_principal_function_decides()
    {
        (string, string)[] single = [.. Multi, ("multi_structure", "single")];
        Assert.Equal(["multi_principal", "multi_principal_clear"], Asked(Eval(single)));

        var o = Eval([.. single, ("multi_principal_clear", "yes"), ("multi_principal", "mesin cetak")]);
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Equal("mesin cetak", o.EssentialTerm);
        Assert.Contains(o.Steps, s => s.Rule == "SECTION_XVI_NOTE_3");
    }

    [Fact]
    public void Note3_without_principal_function_goes_to_expert()
        => Assert.Equal(ConditionStatus.NeedExpertReview,
            Eval([.. Multi, ("multi_structure", "single"), ("multi_principal_clear", "no"), ("multi_principal", "-")]).Status);

    [Fact]
    public void Note4_functional_unit()
    {
        var o = Eval([.. Multi, ("multi_structure", "separate"), ("functional_unit", "yes"), ("multi_principal", "unit pendingin udara")]);
        Assert.Equal("unit pendingin udara", o.EssentialTerm);
        Assert.Contains(o.Steps, s => s.Rule == "SECTION_XVI_NOTE_4");

        Assert.Equal(ConditionStatus.NeedExpertReview,
            Eval([.. Multi, ("multi_structure", "separate"), ("functional_unit", "no"), ("multi_principal", "-")]).Status);
    }

    // ─── bulk ───
    [Fact]
    public void Bulk_records_state_quantity_and_presentation()
    {
        (string, string)[] bulk = [("generic_name", "pupuk urea"), ("bulk_unit", "bulk")];
        Assert.Equal(["bulk_net_quantity", "bulk_physical_state", "bulk_presentation"], Asked(Eval(bulk)));

        var o = Eval([.. bulk, ("bulk_physical_state", "solid"), ("bulk_net_quantity", "50 kg"), ("bulk_presentation", "industrial")]);
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Null(o.EssentialTerm);
        Assert.Contains(o.Steps, s => s.Rule == "BULK" && s.Reason.Contains("50 kg"));
    }

    [Fact]
    public void Bulk_net_quantity_feeds_numeric_thresholds()
    {
        var facts = new FactExtractionService().ExtractFromInput(new() { ["bulk_net_quantity"] = "25 kg" });
        Assert.Equal(25.0, Thresholds.ValueOf(facts, Dimension.Mass)?.Value);
    }

    // ─── used / waste / scrap ───
    [Fact]
    public void Used_goods_that_still_work_are_classified_as_new()
    {
        (string, string)[] used = [("generic_name", "excavator"), ("used_condition", "used")];
        Assert.Equal(["used_still_functions"], Asked(Eval(used)));
        var o = Eval([.. used, ("used_still_functions", "yes")]);
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Null(o.EssentialTerm);
        Assert.Contains(o.Steps, s => s.Rule == "USED_GOODS");
    }

    [Fact]
    public void Unusable_unrepairable_used_goods_become_scrap()
    {
        (string, string)[] broken = [("generic_name", "monitor rusak"), ("used_condition", "used"), ("used_still_functions", "no")];
        Assert.Equal(["used_repairable"], Asked(Eval(broken)));
        Assert.Equal(["scrap_contaminated", "scrap_material"], Asked(Eval([.. broken, ("used_repairable", "no")])));

        var o = Eval([.. broken, ("used_repairable", "no"), ("scrap_material", "elektronik"), ("scrap_contaminated", "no")]);
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Equal("sisa dan skrap elektronik", o.EssentialTerm);
        Assert.Contains(o.Steps, s => s.Rule == "WASTE_SCRAP");
    }

    [Fact]
    public void Repairable_used_goods_keep_their_heading()
    {
        var o = Eval(("generic_name", "monitor"), ("used_condition", "used"), ("used_still_functions", "no"), ("used_repairable", "yes"));
        Assert.Equal(ConditionStatus.Proceed, o.Status);
        Assert.Null(o.EssentialTerm);
    }

    [Fact]
    public void Contaminated_scrap_goes_to_expert()
        => Assert.Equal(ConditionStatus.NeedExpertReview,
            Eval(("generic_name", "skrap"), ("waste_scrap", "waste_scrap"), ("scrap_material", "besi"), ("scrap_contaminated", "unsure")).Status);
}
