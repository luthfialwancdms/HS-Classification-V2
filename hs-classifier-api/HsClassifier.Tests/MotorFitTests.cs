using HsClassifier.Application.Classification;
using HsClassifier.Application.Classification.Rules;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Tests;

/// <summary>
/// Whether the goods have a motor decides between headings such as 8711 (motorcycles, bicycles with an auxiliary motor)
/// and 8712 (bicycles, not motorized). The "Cara kerja" answer must settle it before the user is asked about engines.
/// </summary>
public class MotorFitTests
{
    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    private static CandidateHeading Cycles(string code, string description, string child) => new()
    {
        Code = code, ChapterCode = "87", Description = description, HasHeadingText = true, SourceId = $"btki:{code}",
        ChildSubheadings = [child], ChildDescriptions = { [child] = description },
    };

    private static readonly CandidateHeading Motorcycles = Cycles("8711",
        "Sepeda motor (termasuk moped) dan sepeda yang dilengkapi dengan motor bantu, dengan atau tanpa kereta samping;", "8711.10.00");
    private static readonly CandidateHeading Bicycles = Cycles("8712",
        "Sepeda roda dua dan sepeda lainnya (termasuk sepeda roda tiga untuk pengantar), tidak bermotor.", "8712.00.10");

    private static string? Pick(params (string, string)[] facts) =>
        new RulesEngine().ApplyKumHs(Facts(facts), [Motorcycles, Bicycles]).FinalCode;

    [Fact]
    public void A_manual_bicycle_goes_to_the_non_motorized_heading()
    {
        Assert.StartsWith("8712", Pick(("generic_name", "Sepeda fixie"), ("power_source", "Manual / Tanpa tenaga")));
    }

    [Fact]
    public void An_electric_bicycle_goes_to_the_motorized_heading()
    {
        Assert.StartsWith("8711", Pick(("generic_name", "Sepeda listrik"), ("power_source", "Elektrik / Motor listrik")));
    }

    [Theory]
    [InlineData("Manual / Tanpa tenaga", "Sepeda roda dua dan sepeda lainnya, tidak bermotor.", 3)]
    [InlineData("Manual / Tanpa tenaga", "Sepeda motor dan sepeda dengan motor bantu.", -3)]
    [InlineData("Bensin / Motor bakar", "Sepeda roda dua dan sepeda lainnya, tidak bermotor.", -3)]
    [InlineData("Bensin / Motor bakar", "Sepeda motor dan sepeda dengan motor bantu.", 0)]
    [InlineData("Manual / Tanpa tenaga, Elektrik / Motor listrik", "Sepeda roda dua dan sepeda lainnya, tidak bermotor.", 0)]
    [InlineData("", "Sepeda roda dua dan sepeda lainnya, tidak bermotor.", 0)]
    [InlineData("Manual / Tanpa tenaga", "Pompa untuk cairan.", 0)]
    public void Motor_fit_is_only_a_preference_when_the_heading_says_something_about_motors(string power, string heading, double expected)
    {
        var facts = power.Length == 0 ? Facts(("generic_name", "x")) : Facts(("generic_name", "x"), ("power_source", power));

        Assert.Equal(expected, RulesEngine.MotorFit(facts, new CandidateHeading { Code = "8712", Description = heading }));
    }
}
