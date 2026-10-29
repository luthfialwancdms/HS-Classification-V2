using System.Text.RegularExpressions;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Application.Classification;

public enum ConditionStatus { Proceed, NeedClarification, NeedExpertReview }

/// <summary>
/// Result of the goods-condition modules. When Proceed, <see cref="EssentialTerm"/> replaces the
/// search name (mixture / retail set: the component that gives essential character) and
/// <see cref="PartOfMachine"/> routes a dedicated part to its parent machine's parts heading.
/// </summary>
public record ConditionOutcome(
    ConditionStatus Status,
    List<ReasoningStep> Steps,
    List<ClassificationQuestion> Questions,
    string? Message = null,
    string? EssentialTerm = null,
    string? PartOfMachine = null,
    bool Accessory = false,
    string? ScrapMaterial = null);

/// <summary>
/// Goods-condition modules from the V2 product logic document ("LOGIC KONDISI BARANG"),
/// run before candidate search because they change WHAT is being classified:
/// KUM HS 2(a) incomplete/CKD/SKD, KUM HS 2(b)→3(b) mixtures, KUM HS 3(b) retail sets,
/// Section XVI Note 2 parts and accessories, Section XVI Notes 3/4 multifunction machines and
/// functional units, bulk goods, KUM HS 5 packaging, and used goods / waste and scrap.
/// Every test asks the user for evidence instead of assuming it;
/// a failed or uncertain test goes to expert review, never to a guess.
/// ponytail: Legal Note exclusions are not tested here (Catatan Bagian/Bab not loaded yet; confidence caps it),
///           and bulk concentration / retail-vs-bulk wording is left to the user's subheading choice.
/// </summary>
public static class ConditionGate
{
    public const string Yes = "yes", No = "no", Unsure = "unsure";

    private static readonly List<QuestionChoice> YesNoUnsure =
        [new(Yes, "Ya"), new(No, "Tidak"), new(Unsure, "Tidak yakin")];

    public static ConditionOutcome Evaluate(FactSet facts)
    {
        var steps = new List<ReasoningStep>();
        var questions = new List<ClassificationQuestion>();
        var expert = new List<string>();
        string? essential = null, partOf = null, scrap = null;
        var accessory = false;

        Incomplete(facts, steps, questions, expert);
        RetailSet(facts, steps, questions, expert, ref essential);
        Mixture(facts, steps, questions, ref essential);
        Part(facts, steps, questions, expert, ref partOf, ref accessory);
        Multifunction(facts, steps, questions, expert, ref essential);
        Bulk(facts, steps, questions);
        Packaging(facts, steps, questions, expert, ref essential);
        UsedOrScrap(facts, steps, questions, expert, ref essential, ref scrap);

        if (expert.Count > 0)
            return new(ConditionStatus.NeedExpertReview, steps, [], string.Join(" ", expert));
        if (questions.Count > 0)
            return new(ConditionStatus.NeedClarification, steps, questions,
                "Kondisi barang perlu dikonfirmasi sebelum pos dapat ditentukan.");
        return new(ConditionStatus.Proceed, steps, [], null, essential, partOf, accessory, scrap);
    }

    // ─── KUM HS 2(a): incomplete / unfinished / CKD / SKD ─────────────────────

    private static void Incomplete(FactSet f, List<ReasoningStep> steps, List<ClassificationQuestion> qs, List<string> expert)
    {
        var assembled = (f.GetValue("assembled_status") ?? "").ToLowerInvariant();
        var kit = assembled is "ckd" or "skd";
        if (!kit && assembled is not ("incomplete" or "unfinished")) return;

        var essential = Answer(f, "essential_character_present");
        var together = kit ? Answer(f, "all_components_together") : Yes;

        if (essential is null)
            qs.Add(Choice("essential_character_present",
                "Saat diimpor, apakah barang sudah memiliki karakter utama barang jadinya " +
                "(identitas dan fungsi utama sudah dapat dikenali, sisa pekerjaan hanya perakitan/penyelesaian ringan)?"));
        if (together is null)
            qs.Add(Choice("all_components_together",
                "Apakah seluruh komponen untuk merakit satu unit lengkap diimpor bersama dalam satu pengiriman?"));
        if (essential is null || together is null) return;

        if (essential == Yes && together == Yes)
        {
            steps.Add(Step("KUM_HS_2A", CandidateDecision.Retained,
                $"Barang {assembled.ToUpperInvariant()} telah memiliki karakter utama barang jadi" +
                (kit ? " dan seluruh komponennya diimpor bersama" : "") +
                " → TREAT_AS_COMPLETE: diklasifikasikan sebagai barang lengkap."));
            return;
        }

        var why = essential == No ? "belum memiliki karakter utama barang jadi"
            : together == No ? "komponennya tidak diimpor bersama dalam satu pengiriman"
            : "karakter utamanya belum dapat dipastikan";
        steps.Add(Step("KUM_HS_2A", CandidateDecision.Unresolved,
            $"Barang {why} → CLASSIFY_AS_PRESENTED: komponen diklasifikasikan menurut keadaannya, perlu penilaian ahli."));
        expert.Add($"KUM HS 2(a): barang {why}, sehingga tidak dapat diperlakukan sebagai barang lengkap.");
    }

    // ─── KUM HS 3(b): retail set ──────────────────────────────────────────────

    private static void RetailSet(FactSet f, List<ReasoningStep> steps, List<ClassificationQuestion> qs,
        List<string> expert, ref string? essential)
    {
        if (f.GetValue("set_kit") != "retail_set") return;

        var single = Answer(f, "set_single_purpose");
        var retail = Answer(f, "set_retail_ready");
        var item = Text(f, "set_essential_item");
        if (single is null)
            qs.Add(Choice("set_single_purpose",
                "Apakah barang-barang dalam set disatukan untuk satu kebutuhan atau kegiatan tertentu?"));
        if (retail is null)
            qs.Add(Choice("set_retail_ready",
                "Apakah set dikemas untuk langsung dijual ke pengguna akhir tanpa dikemas ulang?"));
        if (item is null)
            qs.Add(Free("set_essential_item",
                "Barang mana di dalam set yang memberi karakter utama set tersebut? (mis. alat cukur listrik)"));
        if (single is null || retail is null || item is null) return;

        if (single == Yes && retail == Yes)
        {
            essential = item;
            steps.Add(Step("KUM_HS_3B", CandidateDecision.Retained,
                $"Memenuhi syarat set untuk penjualan eceran (satu kebutuhan, siap jual tanpa pengemasan ulang). " +
                $"Diklasifikasikan menurut barang yang memberi karakter utama: \"{item}\"."));
            return;
        }
        steps.Add(Step("KUM_HS_3B", CandidateDecision.Unresolved,
            "Tidak memenuhi syarat set untuk penjualan eceran → setiap barang diklasifikasikan tersendiri."));
        expert.Add("KUM HS 3(b): bukan set untuk penjualan eceran, sehingga setiap barang perlu diklasifikasikan terpisah.");
    }

    // ─── KUM HS 2(b) → 3(b): mixture / composite ──────────────────────────────

    private static void Mixture(FactSet f, List<ReasoningStep> steps, List<ClassificationQuestion> qs, ref string? essential)
    {
        var mix = (f.GetValue("mixture_composite") ?? "").ToLowerInvariant();
        if (!mix.Contains("mixture") && !mix.Contains("campuran") && !mix.Contains("composite")) return;

        var component = Text(f, "mixture_major_component");
        var share = Text(f, "mixture_major_percentage");
        if (component is null)
            qs.Add(Free("mixture_major_component",
                "Bahan/komponen apa yang memberi karakter utama campuran ini (fungsi, peran, atau persepsi pengguna)?"));
        if (share is null)
            qs.Add(Free("mixture_major_percentage",
                "Berapa porsi bahan tersebut (persen berat/volume)? Tulis 'tidak diketahui' bila belum ada datanya."));
        if (component is null || share is null) return;

        // Essential character must come from the stated component, not from percentage alone (doc: no single factor)
        essential ??= component;
        steps.Add(Step("KUM_HS_2B", CandidateDecision.Retained,
            $"Barang campuran/komposit. Karakter utama dinyatakan berasal dari \"{component}\" (porsi: {share}); " +
            "pos dicari berdasarkan bahan tersebut (KUM HS 2(b) → 3(b))."));
    }

    // ─── Section XVI Note 2: parts and accessories ────────────────────────────

    private static void Part(FactSet f, List<ReasoningStep> steps, List<ClassificationQuestion> qs,
        List<string> expert, ref string? partOf, ref bool accessory)
    {
        var kind = f.GetValue("part_accessory");
        if (kind is not ("part" or "accessory")) return;

        if (kind == "accessory")
        {
            // Accessory = adds to / improves the parent's function but is not needed for it, and cannot work alone
            var necessary = Answer(f, "accessory_necessary");
            var standalone = Answer(f, "accessory_standalone");
            if (necessary is null)
                qs.Add(Choice("accessory_necessary",
                    "Apakah barang ini diperlukan agar mesin/barang induknya dapat menjalankan fungsi utamanya?"));
            if (standalone is null)
                qs.Add(Choice("accessory_standalone",
                    "Apakah barang ini dapat dipakai/berfungsi sendiri tanpa mesin/barang induknya?"));
            if (necessary is null || standalone is null) return;

            if (necessary == Unsure || standalone == Unsure)
            {
                steps.Add(Step("ACCESSORY", CandidateDecision.Unresolved, "Sifat aksesori belum dapat dipastikan, perlu penilaian ahli."));
                expert.Add("Aksesori: belum dapat dipastikan apakah barang ini aksesori, bagian, atau barang yang berdiri sendiri.");
                return;
            }
            if (standalone == Yes)
            {
                steps.Add(Step("ACCESSORY", CandidateDecision.Retained,
                    "Barang dapat berfungsi sendiri → bukan aksesori/bagian; diklasifikasikan menurut barangnya sendiri."));
                return;
            }
            accessory = necessary == No;
            steps.Add(Step("ACCESSORY", CandidateDecision.Retained, accessory
                ? "Barang menambah atau meningkatkan fungsi mesin induk tetapi tidak diperlukan untuk fungsi utamanya → aksesori."
                : "Barang diperlukan untuk fungsi utama mesin induk → diperlakukan sebagai bagian (part)."));
        }

        var rule = accessory ? "ACCESSORY" : "SECTION_XVI_NOTE_2";
        var noun = accessory ? "Aksesori" : "Bagian";
        var dedicated = Answer(f, "part_dedicated");
        var parentQ = Free("part_parent_machine", $"{noun} dari mesin/barang apa? (mis. excavator, laptop, mobil)");
        if (dedicated is null)
        {
            qs.Add(new ClassificationQuestion
            {
                FactType = "part_dedicated",
                QuestionText = $"Apakah {noun.ToLowerInvariant()} ini cocok digunakan semata-mata atau terutama untuk satu jenis mesin/barang tertentu?",
                Choices = [new(Yes, "Ya, khusus/terutama untuk mesin tertentu"),
                           new(No, "Tidak, dipakai umum (mis. baut, mur, pegas, bantalan)")],
                InformationGain = 1.0,
            });
            if (Text(f, "part_parent_machine") is null) qs.Add(parentQ);
            return;
        }

        if (dedicated == No)
        {
            steps.Add(Step(rule, CandidateDecision.Retained,
                $"{noun} penggunaan umum: diklasifikasikan menurut barangnya sendiri, bukan sebagai {noun.ToLowerInvariant()} mesin tertentu."));
            return;
        }

        var parent = Text(f, "part_parent_machine");
        if (parent is null) { qs.Add(parentQ); return; }
        partOf = parent;
        steps.Add(Step(rule, CandidateDecision.Retained, accessory
            ? $"Aksesori yang khusus/terutama untuk \"{parent}\": diklasifikasikan pada pos \"bagian dan aksesori\" untuk mesin tersebut."
            : $"Bagian yang khusus/terutama untuk \"{parent}\": diklasifikasikan pada pos bagian untuk mesin tersebut."));
    }

    // ─── Section XVI Notes 3 and 4: multifunction machines, functional units ──

    private static void Multifunction(FactSet f, List<ReasoningStep> steps, List<ClassificationQuestion> qs,
        List<string> expert, ref string? essential)
    {
        if (Answer(f, "multifunction") != Yes) return;

        var structure = f.GetValue("multi_structure")?.Trim().ToLowerInvariant();
        if (structure is not ("single" or "separate"))
        {
            qs.Add(new ClassificationQuestion
            {
                FactType = "multi_structure", QuestionText = "Bagaimana fungsi-fungsi tambahan itu disusun?", InformationGain = 1.0,
                Choices = [new("single", "Satu mesin/badan yang menjalankan beberapa fungsi (mis. printer-scanner-fotokopi)"),
                           new("separate", "Beberapa komponen terpisah yang dihubungkan untuk satu fungsi bersama (mis. sistem pendingin split)")],
            });
            return;
        }

        var single = structure == "single";
        var testType = single ? "multi_principal_clear" : "functional_unit";
        var test = Answer(f, testType);
        var principal = Text(f, "multi_principal");
        if (test is null)
            qs.Add(Choice(testType, single
                ? "Apakah salah satu fungsi jelas merupakan fungsi utama barang ini?"
                : "Apakah komponen-komponen itu bersama-sama menjalankan satu fungsi yang jelas (mis. pendinginan udara, pembangkitan listrik)?"));
        if (principal is null)
            qs.Add(Free("multi_principal", (single ? "Fungsi utama" : "Fungsi bersama") +
                " itu adalah fungsi mesin apa? Tulis sebagai nama barang (mis. mesin cetak, unit pendingin udara). Tulis '-' bila tidak ada."));
        if (test is null || principal is null) return;

        var rule = single ? "SECTION_XVI_NOTE_3" : "SECTION_XVI_NOTE_4";
        if (test == Yes && principal != "-")
        {
            essential ??= principal;
            steps.Add(Step(rule, CandidateDecision.Retained, single
                ? $"Mesin multifungsi diklasifikasikan seolah-olah hanya terdiri dari komponen yang menjalankan fungsi utama: \"{principal}\" (Catatan 3 Bagian XVI)."
                : $"Komponen terpisah membentuk satu unit fungsional → diklasifikasikan pada pos fungsi tersebut: \"{principal}\" (Catatan 4 Bagian XVI)."));
            return;
        }
        var why = test == Unsure || principal == "-" && test == Yes ? "belum dapat dipastikan"
            : single ? "fungsi utama tidak dapat ditentukan, sehingga KUM HS 3(c) berlaku"
            : "komponen tidak membentuk satu unit fungsional, sehingga diklasifikasikan masing-masing";
        steps.Add(Step(rule, CandidateDecision.Unresolved, $"Mesin multifungsi/unit fungsional: {why}. Perlu penilaian ahli."));
        expert.Add($"{(single ? "Catatan 3" : "Catatan 4")} Bagian XVI: {why}.");
    }

    // ─── Bulk: facts the subheading texts compare (net quantity, state, presentation) ─

    private static readonly Dictionary<string, string> BulkStates = new()
    {
        ["solid"] = "padat (butiran, bongkahan, lembaran)", ["powder"] = "serbuk/bubuk", ["liquid"] = "cair", ["gas"] = "gas",
    };
    private static readonly Dictionary<string, string> BulkPresentations = new()
    {
        ["industrial"] = "curah/kemasan industri, tidak untuk penjualan eceran", ["retail"] = "dikemas untuk penjualan eceran",
    };

    private static void Bulk(FactSet f, List<ReasoningStep> steps, List<ClassificationQuestion> qs)
    {
        if (f.GetValue("bulk_unit") != "bulk") return;

        var state = Text(f, "bulk_physical_state");
        var qty = Text(f, "bulk_net_quantity");
        var presentation = Text(f, "bulk_presentation");
        if (state is null)
            qs.Add(new ClassificationQuestion
            {
                FactType = "bulk_physical_state", QuestionText = "Apa wujud fisik barang curah ini?", InformationGain = 1.0,
                Choices = BulkStates.Select(kv => new QuestionChoice(kv.Key, kv.Value)).ToList(),
            });
        if (qty is null)
            qs.Add(Free("bulk_net_quantity", "Berapa berat/volume bersih per kemasan atau per kiriman? (mis. 50 kg, 1000 liter)"));
        if (presentation is null)
            qs.Add(new ClassificationQuestion
            {
                FactType = "bulk_presentation", QuestionText = "Bagaimana barang disajikan saat impor?", InformationGain = 1.0,
                Choices = BulkPresentations.Select(kv => new QuestionChoice(kv.Key, kv.Value)).ToList(),
            });
        if (state is null || qty is null || presentation is null) return;

        steps.Add(Step("BULK", CandidateDecision.Retained,
            $"Barang curah: wujud {BulkStates.GetValueOrDefault(state, state)}, berat/volume bersih {qty}, " +
            $"penyajian {BulkPresentations.GetValueOrDefault(presentation, presentation)}. " +
            "Fakta ini dipakai saat membandingkan uraian subpos (berat bersih, kemasan eceran atau bukan)."));
    }

    // ─── Used goods, waste and scrap ──────────────────────────────────────────

    private static void UsedOrScrap(FactSet f, List<ReasoningStep> steps, List<ClassificationQuestion> qs,
        List<string> expert, ref string? essential, ref string? scrap)
    {
        var used = f.GetValue("used_condition") == "used";
        var waste = f.GetValue("waste_scrap") == "waste_scrap";
        if (!used && !waste) return;

        if (!waste)
        {
            var works = Answer(f, "used_still_functions");
            if (works is null)
            {
                qs.Add(Choice("used_still_functions", "Apakah barang bekas ini masih dapat dipakai sesuai fungsi aslinya?"));
                return;
            }
            var repairable = works == No ? Answer(f, "used_repairable") : null;
            if (works == No && repairable is null)
            {
                qs.Add(Choice("used_repairable", "Apakah barang masih layak diperbaiki secara ekonomis agar berfungsi kembali?"));
                return;
            }
            if (works == Unsure || repairable == Unsure)
            {
                steps.Add(Step("USED_GOODS", CandidateDecision.Unresolved, "Kondisi barang bekas belum dapat dipastikan, perlu penilaian ahli."));
                expert.Add("Barang bekas: belum dapat dipastikan apakah masih berfungsi/dapat diperbaiki atau sudah menjadi skrap.");
                return;
            }
            if (works == Yes || repairable == Yes)
            {
                // BTKI does not separate new from used, except headings naming used goods (e.g. 40.12, 63.09)
                steps.Add(Step("USED_GOODS", CandidateDecision.Retained,
                    (works == Yes ? "Barang bekas masih berfungsi" : "Barang rusak tetapi masih layak diperbaiki") +
                    " → diklasifikasikan seperti barang barunya; perhatikan pos yang khusus menyebut barang bekas (mis. ban bekas 40.12, pakaian bekas 63.09)."));
                return;
            }
        }

        var material = Text(f, "scrap_material");
        var contaminated = Answer(f, "scrap_contaminated");
        if (material is null)
            qs.Add(Free("scrap_material", "Bahan utama sisa/skrap ini apa? (mis. besi/baja, tembaga, aluminium, plastik, kertas, elektronik)"));
        if (contaminated is null)
            qs.Add(Choice("scrap_contaminated",
                "Apakah sisa/skrap tercampur atau terkontaminasi bahan lain atau bahan berbahaya (oli, bahan kimia, limbah B3)?"));
        if (material is null || contaminated is null) return;

        if (contaminated != No)
        {
            steps.Add(Step("WASTE_SCRAP", CandidateDecision.Unresolved,
                "Sisa/skrap terkontaminasi atau mungkin berbahaya: dapat masuk pos lain (mis. limbah 38.25), perlu penilaian ahli."));
            expert.Add("Sisa/skrap yang terkontaminasi atau mungkin berbahaya perlu penilaian ahli.");
            return;
        }
        // Scrap changes WHAT is classified, so it overrides any earlier essential term
        essential = $"sisa dan skrap {material}";
        scrap = material;
        steps.Add(Step("WASTE_SCRAP", CandidateDecision.Retained,
            (used ? "Barang bekas tidak berfungsi dan tidak layak diperbaiki" : "Barang berupa sisa/skrap") +
            $" → diklasifikasikan sebagai sisa dan skrap menurut bahannya: \"{material}\" " +
            "(Catatan 8(a) Bagian XV untuk logam; pos 85.49 untuk skrap elektrik/elektronik). " +
            "Impor sisa/skrap tunduk pada ketentuan larangan/pembatasan."));
    }

    // ─── KUM HS 5: cases/containers (5a) and packing (5b) ─────────────────────

    private static void Packaging(FactSet f, List<ReasoningStep> steps, List<ClassificationQuestion> qs,
        List<string> expert, ref string? essential)
    {
        if (string.IsNullOrWhiteSpace(f.GetValue("packaging_container"))) return;

        var kind = f.GetValue("packaging_kind")?.Trim().ToLowerInvariant();
        var contents = Text(f, "packaging_contents");
        if (kind is not ("case" or "packing"))
            qs.Add(new ClassificationQuestion
            {
                FactType = "packaging_kind", QuestionText = "Wadah/kemasan ini termasuk jenis apa?", InformationGain = 1.0,
                Choices = [new("case", "Wadah yang dibentuk khusus untuk barang tertentu (mis. tas kamera, kotak biola, kotak perhiasan)"),
                           new("packing", "Bahan/wadah pembungkus untuk pengiriman (mis. kardus, peti kayu, plastik pembungkus)")],
            });
        if (contents is null)
            qs.Add(Free("packaging_contents", "Barang apa yang diwadahi/dibungkus? (mis. kamera digital, televisi)"));
        if (kind is not ("case" or "packing") || contents is null) return;

        // 5(a): all four tests must hold; 5(b): normally used for these goods and not reusable
        var tests = kind == "case"
            ? new[]
            {
                ("packaging_specially_shaped", "Apakah wadah dibentuk khusus atau pas untuk barang tersebut?", Yes),
                ("packaging_long_term", "Apakah wadah cocok dipakai dalam jangka panjang bersama barangnya?", Yes),
                ("packaging_sold_with", "Apakah wadah diimpor dan lazim dijual bersama barangnya?", Yes),
                ("packaging_essential", "Apakah wadah itu sendiri yang memberi karakter utama keseluruhan (mis. kotak perak berisi teh)?", No),
            }
            : new[]
            {
                ("packaging_normally_used", "Apakah kemasan ini lazim dipakai untuk membungkus barang tersebut?", Yes),
                ("packaging_reusable", "Apakah kemasan secara nyata cocok untuk dipakai berulang kali (mis. tabung gas baja, palet khusus)?", No),
            };

        var answers = tests.Select(t => (t.Item1, Answer(f, t.Item1), t.Item3)).ToList();
        foreach (var (type, text, _) in tests)
            if (Answer(f, type) is null) qs.Add(Choice(type, text));
        if (answers.Any(a => a.Item2 is null)) return;

        var rule = kind == "case" ? "KUM_HS_5A" : "KUM_HS_5B";
        if (answers.Any(a => a.Item2 == Unsure))
        {
            steps.Add(Step(rule, CandidateDecision.Unresolved, "Syarat KUM HS 5 belum dapat dipastikan, perlu penilaian ahli."));
            expert.Add($"KUM HS 5: perlakuan wadah/kemasan untuk \"{contents}\" belum dapat dipastikan.");
            return;
        }
        if (answers.All(a => a.Item2 == a.Item3))
        {
            essential ??= contents;
            steps.Add(Step(rule, CandidateDecision.Retained,
                $"Wadah/kemasan memenuhi syarat {(kind == "case" ? "KUM HS 5(a)" : "KUM HS 5(b)")} → diklasifikasikan bersama barangnya: \"{contents}\"."));
            return;
        }
        steps.Add(Step(rule, CandidateDecision.Retained,
            "Wadah/kemasan tidak memenuhi syarat KUM HS 5 → diklasifikasikan tersendiri menurut wadahnya."));
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static string? Answer(FactSet f, string type) =>
        f.GetValue(type)?.Trim().ToLowerInvariant() switch
        {
            "yes" or "ya" => Yes,
            "no" or "tidak" => No,
            "unsure" or "tidak yakin" => Unsure,
            _ => null,
        };

    private static string? Text(FactSet f, string type) =>
        f.GetValue(type)?.Trim() is { Length: > 0 } v ? v : null;

    private static ClassificationQuestion Choice(string type, string text) =>
        new() { FactType = type, QuestionText = text, Choices = YesNoUnsure, InformationGain = 1.0 };

    private static ClassificationQuestion Free(string type, string text) =>
        new() { FactType = type, QuestionText = text, InformationGain = 1.0 };

    private static ReasoningStep Step(string rule, CandidateDecision d, string reason) =>
        new() { Rule = rule, Decision = d, Reason = reason };
}

/// <summary>Reads which machine headings a BTKI "parts" heading text refers to (e.g. "pos 84.25 sampai dengan 84.30").</summary>
public static class PartsHeadingRef
{
    // "84.25" or OCR-split "84. 70"
    private const string Code = @"(\d{2})\.\s?(\d{2})";
    private static readonly Regex Range = new(Code + @"\s*(?:sampai dengan|sampai|hingga|s\.?\s?d\.?|s/d|to|-|–)\s*" + Code, RegexOptions.IgnoreCase);
    private static readonly Regex Single = new(@"\b" + Code + @"\b");

    /// <summary>True when the text names any heading/subheading code, i.e. the branch is restricted to specific goods.</summary>
    public static bool CitesAnyCode(string text) => Single.IsMatch(text);

    public static bool IsPartsHeading(string text) =>
        Regex.IsMatch(text.TrimStart(), @"^(bagian|parts?)\b", RegexOptions.IgnoreCase);

    public static bool Covers(string text, string heading4) => IsPartsHeading(text) && References(text, heading4);

    /// <summary>True when the text cites the heading, alone or inside a range ("pos 84.26, 84.29 atau 84.30").</summary>
    public static bool References(string text, string heading4)
    {
        if (!int.TryParse(heading4, out var h)) return false;
        var rest = text;
        foreach (Match m in Range.Matches(text))
        {
            int a = int.Parse(m.Groups[1].Value + m.Groups[2].Value), b = int.Parse(m.Groups[3].Value + m.Groups[4].Value);
            if (h >= Math.Min(a, b) && h <= Math.Max(a, b)) return true;
            rest = rest.Replace(m.Value, " ");
        }
        foreach (Match m in Single.Matches(rest))
            if (int.Parse(m.Groups[1].Value + m.Groups[2].Value) == h) return true;
        return false;
    }
}
